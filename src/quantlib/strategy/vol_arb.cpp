// src/quantlib/strategy/vol_arb.cpp
#include "vol_arb.h"
#include <algorithm>
#include <cmath>
#include <numeric>

namespace Alaris::Strategy {

VolatilityArbitrageStrategy::VolatilityArbitrageStrategy(
    Pricing::QuantLibALOEngine& pricer,
    Core::PerCycleAllocator& allocator,
    Core::EventLogger& event_logger,
    Core::MemoryPool& mem_pool)
    : pricer_(pricer), allocator_(allocator), event_logger_(event_logger),
      gjr_garch_model_(mem_pool), standard_garch_model_(mem_pool),
      active_model_(VolatilityModel::ENSEMBLE),
      signals_generated_(0), successful_signals_(0), total_pnl_(0.0) {
    
    // Initialize model performance tracking
    for (auto& perf : model_performance_) {
        perf.accuracy = 0.5; // Start with neutral accuracy
        perf.avg_error = 0.0;
        perf.prediction_count = 0;
        perf.sharpe_ratio = 0.0;
    }
}

void VolatilityArbitrageStrategy::on_market_data(const IPC::MarketDataMessage& data) {
    // Update latest market data
    latest_market_data_[data.symbol_id] = data;
    
    // Update volatility models with return data
    auto it = latest_market_data_.find(data.symbol_id);
    if (it != latest_market_data_.end()) {
        const auto& prev_data = it->second;
        if (prev_data.underlying_price > 0 && data.underlying_price > 0) {
            double return_val = std::log(data.underlying_price / prev_data.underlying_price);
            
            gjr_garch_model_.update(return_val);
            standard_garch_model_.update(return_val);
            
            event_logger_.log_volatility_update(
                data.symbol_id,
                get_volatility_forecast(data.symbol_id),
                (data.bid_iv + data.ask_iv) / 2.0,
                calculate_confidence(std::abs(get_volatility_forecast(data.symbol_id) - 
                                            (data.bid_iv + data.ask_iv) / 2.0), 0.7)
            );
        }
    }
    
    // Update existing positions
    auto pos_it = current_positions_.find(data.symbol_id);
    if (pos_it != current_positions_.end()) {
        pos_it->second.current_price = (data.bid + data.ask) / 2.0;
        pos_it->second.current_implied_vol = (data.bid_iv + data.ask_iv) / 2.0;
        pos_it->second.unrealized_pnl = pos_it->second.quantity * 
                                       (pos_it->second.current_price - pos_it->second.entry_price);
        
        // Check exit conditions
        if (should_exit_position(pos_it->second, data)) {
            event_logger_.log_system_status("Closing position for symbol " + 
                                          std::to_string(data.symbol_id));
            close_position(data.symbol_id);
        }
    }
}

void VolatilityArbitrageStrategy::scan_options(
    const std::vector<Pricing::OptionData>& options,
    std::vector<IPC::TradingSignalMessage>& signals) {
    
    signals.clear();
    
    for (const auto& option : options) {
        auto market_it = latest_market_data_.find(option.symbol_id);
        if (market_it == latest_market_data_.end()) {
            continue; // No market data available
        }
        
        const auto& market_data = market_it->second;
        
        // Check if we should enter a position
        if (should_enter_position(market_data, option.strike_price, option.option_type)) {
            
            double forecast_vol = get_volatility_forecast(option.symbol_id);
            double implied_vol = (market_data.bid_iv + market_data.ask_iv) / 2.0;
            double vol_diff = forecast_vol - implied_vol;
            
            double confidence = calculate_confidence(std::abs(vol_diff), 
                                                   model_performance_[static_cast<int>(active_model_)].accuracy);
            
            if (confidence > params_.confidence_threshold) {
                // Generate trading signal
                IPC::TradingSignalMessage signal;
                signal.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
                    std::chrono::high_resolution_clock::now().time_since_epoch()).count();
                signal.symbol_id = option.symbol_id;
                signal.theoretical_price = calculate_theoretical_price(
                    market_data, option.strike_price, option.option_type, forecast_vol);
                signal.market_price = (market_data.bid + market_data.ask) / 2.0;
                signal.implied_volatility = implied_vol;
                signal.forecast_volatility = forecast_vol;
                signal.confidence = confidence;
                signal.quantity = static_cast<int32_t>(calculate_position_size(market_data, confidence));
                signal.side = vol_diff > 0 ? 0 : 1; // Buy if forecast > implied, sell otherwise
                signal.urgency = static_cast<uint8_t>(std::min(255.0, confidence * 255.0));
                signal.signal_type = 0; // Entry signal
                
                signals.push_back(signal);
                signals_generated_++;
                
                event_logger_.log_trading_signal(signal);
            }
        }
    }
}

double VolatilityArbitrageStrategy::calculate_theoretical_price(
    const IPC::MarketDataMessage& market_data,
    double strike, QuantLib::Option::Type option_type,
    double forecast_volatility) {
    
    Pricing::OptionData option_data;
    option_data.underlying_price = market_data.underlying_price;
    option_data.strike_price = strike;
    option_data.risk_free_rate = 0.05; // Could be made configurable
    option_data.dividend_yield = 0.0;  // Could be made configurable
    option_data.volatility = forecast_volatility;
    option_data.time_to_expiry = 30.0 / 365.0; // Assume 30 days, could be actual
    option_data.option_type = option_type;
    option_data.symbol_id = market_data.symbol_id;
    
    return pricer_.calculate_option_price(option_data);
}

double VolatilityArbitrageStrategy::get_volatility_forecast(uint32_t symbol_id, size_t horizon) {
    switch (active_model_) {
        case VolatilityModel::GJR_GARCH:
            return gjr_garch_model_.forecast_volatility(horizon);
        case VolatilityModel::STANDARD_GARCH:
            return standard_garch_model_.forecast_volatility(horizon);
        case VolatilityModel::ENSEMBLE:
            return get_ensemble_forecast(symbol_id, horizon);
        default:
            return gjr_garch_model_.forecast_volatility(horizon);
    }
}

double VolatilityArbitrageStrategy::get_ensemble_forecast(uint32_t symbol_id, size_t horizon) {
    double gjr_forecast = gjr_garch_model_.forecast_volatility(horizon);
    double garch_forecast = standard_garch_model_.forecast_volatility(horizon);
    
    // Weight forecasts by model performance
    double gjr_weight = model_performance_[0].accuracy;
    double garch_weight = model_performance_[1].accuracy;
    double total_weight = gjr_weight + garch_weight;
    
    if (total_weight > 0) {
        return (gjr_forecast * gjr_weight + garch_forecast * garch_weight) / total_weight;
    } else {
        return (gjr_forecast + garch_forecast) / 2.0; // Equal weights if no performance data
    }
}

bool VolatilityArbitrageStrategy::should_enter_position(
    const IPC::MarketDataMessage& market_data,
    double strike, QuantLib::Option::Type option_type) {
    
    // Don't enter if we already have a position
    if (current_positions_.find(market_data.symbol_id) != current_positions_.end()) {
        return false;
    }
    
    double forecast_vol = get_volatility_forecast(market_data.symbol_id);
    double implied_vol = (market_data.bid_iv + market_data.ask_iv) / 2.0;
    double vol_diff = std::abs(forecast_vol - implied_vol);
    
    return vol_diff > params_.entry_threshold;
}

bool VolatilityArbitrageStrategy::should_exit_position(
    const PositionInfo& position,
    const IPC::MarketDataMessage& market_data) {
    
    double forecast_vol = get_volatility_forecast(position.symbol_id);
    double current_implied_vol = (market_data.bid_iv + market_data.ask_iv) / 2.0;
    double vol_diff = std::abs(forecast_vol - current_implied_vol);
    
    // Exit if volatility difference has narrowed
    if (vol_diff < params_.exit_threshold) {
        return true;
    }
    
    // Exit if we've hit risk limit
    if (std::abs(position.unrealized_pnl) > params_.risk_limit * std::abs(position.entry_price)) {
        return true;
    }
    
    return false;
}

double VolatilityArbitrageStrategy::calculate_position_size(
    const IPC::MarketDataMessage& market_data, double confidence) {
    
    double base_size = params_.max_position_size;
    double confidence_adjustment = confidence; // Scale by confidence
    
    return base_size * confidence_adjustment;
}

double VolatilityArbitrageStrategy::calculate_confidence(double vol_diff, double historical_accuracy) {
    // Simple confidence calculation - could be enhanced
    double vol_confidence = std::min(1.0, vol_diff / params_.entry_threshold);
    double model_confidence = historical_accuracy;
    
    return std::sqrt(vol_confidence * model_confidence);
}

void VolatilityArbitrageStrategy::update_position(uint32_t symbol_id, double quantity, double price) {
    auto it = current_positions_.find(symbol_id);
    if (it != current_positions_.end()) {
        // Update existing position
        it->second.quantity += quantity;
        it->second.current_price = price;
    } else {
        // Create new position
        PositionInfo pos;
        pos.symbol_id = symbol_id;
        pos.quantity = quantity;
        pos.entry_price = price;
        pos.current_price = price;
        pos.entry_timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::high_resolution_clock::now().time_since_epoch()).count();
        
        current_positions_[symbol_id] = pos;
    }
}

void VolatilityArbitrageStrategy::close_position(uint32_t symbol_id) {
    auto it = current_positions_.find(symbol_id);
    if (it != current_positions_.end()) {
        total_pnl_ += it->second.unrealized_pnl;
        if (it->second.unrealized_pnl > 0) {
            successful_signals_++;
        }
        current_positions_.erase(it);
    }
}

VolatilityArbitrageStrategy::PerformanceMetrics 
VolatilityArbitrageStrategy::get_performance_metrics() const {
    PerformanceMetrics metrics;
    metrics.total_pnl = total_pnl_;
    metrics.signals_generated = signals_generated_;
    metrics.successful_signals = successful_signals_;
    metrics.win_rate = signals_generated_ > 0 ? 
                      static_cast<double>(successful_signals_) / signals_generated_ : 0.0;
    metrics.model_performance = model_performance_;
    
    // Calculate Sharpe ratio (simplified)
    metrics.sharpe_ratio = total_pnl_ / std::max(1.0, std::sqrt(static_cast<double>(signals_generated_)));
    
    return metrics;
}

void VolatilityArbitrageStrategy::set_parameters(const StrategyParameters& params) {
    params_ = params;
    event_logger_.log_system_status("Strategy parameters updated");
}

double VolatilityArbitrageStrategy::total_unrealized_pnl() const {
    double total = 0.0;
    for (const auto& pair : current_positions_) {
        total += pair.second.unrealized_pnl;
    }
    return total;
}

} // namespace Alaris::Strategy
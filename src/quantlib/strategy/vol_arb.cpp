#include "vol_arb.h"
#include "../ipc/message_types.h"
#include "../core/event_log.h"
#include "../volatility/vol_forecast.h"

#include <chrono>
#include <cmath>
#include <numeric>
#include <algorithm>

namespace Alaris::Strategy {

VolatilityArbitrageStrategy::VolatilityArbitrageStrategy(
    Pricing::QuantLibALOEngine& pricer,
    Core::PerCycleAllocator& allocator,
    Core::EventLogger& event_logger,
    Core::MemoryPool& mem_pool)
    : pricer_(pricer),
      allocator_(allocator),
      event_logger_(event_logger),
      mem_pool_(mem_pool),
      gjr_garch_model_(mem_pool_),
      active_model_type_(VolatilityModelType::ENSEMBLE_GJR_HISTORICAL) {
    
    // Initialize the global volatility forecaster
    Alaris::Volatility::initialize_volatility_forecaster(gjr_garch_model_, mem_pool_);
    
    // Create the global forecaster wrapper
    global_forecaster_ = std::make_unique<Volatility::GlobalVolatilityForecaster>(gjr_garch_model_, mem_pool_);

    event_logger_.log_system_status("VolatilityArbitrageStrategy initialized. Default model: ENSEMBLE_GJR_HISTORICAL.");
    
    // Initialize model performance tracking
    model_performance_tracking_[0] = ModelPerformance();
    model_performance_tracking_[1] = ModelPerformance();
}

void VolatilityArbitrageStrategy::on_market_data(const IPC::MarketDataMessage& data) {
    latest_market_data_[data.symbol_id] = data;

    // Track previous prices for return calculation
    static std::unordered_map<uint32_t, double> previous_underlying_prices;
    
    if (data.underlying_price > 0) {
        if (previous_underlying_prices.count(data.symbol_id) && previous_underlying_prices[data.symbol_id] > 0) {
            double previous_price = previous_underlying_prices[data.symbol_id];
            double current_price = data.underlying_price;
            if (previous_price > 0 && current_price > 0) {
                QuantLib::Real return_val = std::log(current_price / previous_price);
                gjr_garch_model_.update(return_val);
            }
        }
        previous_underlying_prices[data.symbol_id] = data.underlying_price;
    }

    // Update active positions based on new market data
    for (auto& pair : current_positions_) {
        PositionInfo& position = pair.second;
        
        // Check if this market data is for the option itself
        if (data.symbol_id == position.symbol_id) {
            position.current_price = (data.bid + data.ask) / 2.0;
            position.current_implied_vol = (data.bid_iv + data.ask_iv) / 2.0;
            position.unrealized_pnl = position.quantity * (position.current_price - position.entry_price);
        }
    }
}

double VolatilityArbitrageStrategy::get_volatility_forecast(uint32_t underlying_symbol_id, size_t horizon) {
    std::vector<double> historical_returns;

    switch (active_model_type_) {
        case VolatilityModelType::GJR_GARCH_DIRECT:
            return gjr_garch_model_.forecast_volatility(horizon);
            
        case VolatilityModelType::ENSEMBLE_GJR_HISTORICAL: {
            // Get returns from the GJR model for ensemble
            const auto& internal_returns_deque = gjr_garch_model_.get_returns();
            std::vector<double> model_internal_returns(internal_returns_deque.begin(), internal_returns_deque.end());
            return Alaris::Volatility::forecast_volatility_ensemble(horizon, model_internal_returns);
        }
        
        default:
            event_logger_.log_error("Unknown active_model_type_ in get_volatility_forecast.");
            return gjr_garch_model_.forecast_volatility(horizon);
    }
}

double VolatilityArbitrageStrategy::calculate_theoretical_price(
    const IPC::MarketDataMessage& underlying_market_data,
    const Pricing::OptionData& option_to_price,
    double forecast_volatility) {

    Pricing::OptionData pricing_data = option_to_price;
    pricing_data.underlying_price = underlying_market_data.underlying_price;
    pricing_data.volatility = forecast_volatility;
    
    return pricer_.calculate_option_price(pricing_data);
}

void VolatilityArbitrageStrategy::scan_option_chain(
    uint32_t underlying_symbol_id,
    const std::vector<Pricing::OptionData>& options_in_chain,
    const std::vector<IPC::MarketDataMessage>& option_market_data,
    std::vector<IPC::TradingSignalMessage>& out_signals) {
    
    out_signals.clear();

    auto underlying_md_it = latest_market_data_.find(underlying_symbol_id);
    if (underlying_md_it == latest_market_data_.end()) {
        event_logger_.log_error("No market data for underlying symbol_id: " + std::to_string(underlying_symbol_id) + " in scan_option_chain.");
        return;
    }
    const IPC::MarketDataMessage& underlying_md = underlying_md_it->second;

    if (options_in_chain.size() != option_market_data.size()) {
        event_logger_.log_error("Mismatch between options_in_chain and option_market_data sizes.");
        return;
    }

    for (size_t i = 0; i < options_in_chain.size(); ++i) {
        const Pricing::OptionData& option_details = options_in_chain[i];
        const IPC::MarketDataMessage& current_option_md = option_market_data[i];

        // Validate option market data
        if (current_option_md.ask <= 0 || current_option_md.bid <= 0 || current_option_md.ask <= current_option_md.bid) {
            continue;
        }
        
        double option_market_mid_price = (current_option_md.bid + current_option_md.ask) / 2.0;
        double option_market_iv = (current_option_md.bid_iv + current_option_md.ask_iv) / 2.0;
        if (option_market_iv <= 0) option_market_iv = 0.2;

        // Check if we should enter a new position
        if (current_positions_.find(option_details.symbol_id) == current_positions_.end()) {
            if (should_enter_position(underlying_md, option_details, option_market_mid_price, option_market_iv)) {
                double forecast_vol = get_volatility_forecast(underlying_symbol_id);
                double vol_diff = forecast_vol - option_market_iv;
                double confidence = calculate_signal_confidence(std::abs(vol_diff), active_model_type_);

                if (confidence >= params_.confidence_threshold) {
                    IPC::TradingSignalMessage signal;
                    signal.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
                        std::chrono::high_resolution_clock::now().time_since_epoch()).count();
                    
                    signal.symbol_id = option_details.symbol_id;
                    signal.theoretical_price = calculate_theoretical_price(underlying_md, option_details, forecast_vol);
                    signal.market_price = option_market_mid_price;
                    signal.implied_volatility = option_market_iv;
                    signal.forecast_volatility = forecast_vol;
                    signal.confidence = confidence;
                    
                    // Position sizing
                    signal.quantity = static_cast<int32_t>(
                        calculate_position_size(underlying_md.underlying_price, signal.theoretical_price, confidence)
                    );
                    if (signal.quantity == 0) continue;

                    // Determine side based on theoretical vs market price
                    if (signal.theoretical_price > signal.market_price) {
                        signal.side = 0; // BUY
                    } else {
                        signal.side = 1; // SELL
                        signal.quantity = -signal.quantity;
                    }

                    signal.urgency = static_cast<uint8_t>(std::min(255.0, confidence * 2.55 * 100.0));
                    signal.signal_type = 0; // Entry signal

                    out_signals.push_back(signal);
                    signals_generated_total_++;
                    event_logger_.log_trading_signal(signal);
                }
            }
        } else {
            // Check exit conditions for existing positions
            PositionInfo& existing_pos = current_positions_[option_details.symbol_id];
            existing_pos.current_price = option_market_mid_price;
            existing_pos.current_implied_vol = option_market_iv;
            existing_pos.unrealized_pnl = existing_pos.quantity * (existing_pos.current_price - existing_pos.entry_price);

            if (should_exit_position(existing_pos, underlying_md, option_market_mid_price, option_market_iv)) {
                IPC::TradingSignalMessage signal;
                signal.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
                    std::chrono::high_resolution_clock::now().time_since_epoch()).count();
                signal.symbol_id = option_details.symbol_id;
                signal.market_price = option_market_mid_price;
                signal.quantity = static_cast<int32_t>(-existing_pos.quantity);
                signal.side = (existing_pos.quantity > 0) ? 1 : 0;
                signal.signal_type = 1; // Exit signal
                signal.urgency = 255;

                out_signals.push_back(signal);
                signals_generated_total_++;
                event_logger_.log_trading_signal(signal);
            }
        }
    }
}

bool VolatilityArbitrageStrategy::should_enter_position(
    const IPC::MarketDataMessage& underlying_md,
    const Pricing::OptionData& option_details,
    double current_option_market_price,
    double current_option_implied_vol) {

    double forecast_vol = get_volatility_forecast(underlying_md.symbol_id);
    double vol_diff = std::abs(forecast_vol - current_option_implied_vol);

    return vol_diff >= params_.entry_threshold;
}

bool VolatilityArbitrageStrategy::should_exit_position(
    const PositionInfo& position,
    const IPC::MarketDataMessage& underlying_md,
    double current_option_market_price,
    double current_option_implied_vol) {

    double forecast_vol = get_volatility_forecast(underlying_md.symbol_id);
    double vol_diff = std::abs(forecast_vol - current_option_implied_vol);

    // Exit if volatility difference has narrowed
    if (vol_diff < params_.exit_threshold) {
        event_logger_.log_system_status("Exit reason: Volatility difference narrowed for option " + std::to_string(position.symbol_id));
        return true;
    }

    // Exit if unrealized P&L hits risk limit (stop loss)
    double initial_investment = std::abs(position.quantity * position.entry_price);
    if (initial_investment > 0 && position.unrealized_pnl < 0) {
        if (std::abs(position.unrealized_pnl) / initial_investment > params_.risk_limit) {
            event_logger_.log_system_status("Exit reason: Stop loss triggered for option " + std::to_string(position.symbol_id));
            return true;
        }
    }
    
    return false;
}

double VolatilityArbitrageStrategy::calculate_position_size(
    double underlying_price, double option_price, double confidence) {
    
    if (option_price <= 0) return 0;
    
    double max_capital_per_trade = 10000; // Example: $10,000
    double capital_to_allocate = max_capital_per_trade * confidence * params_.max_position_size;
    
    int num_contracts = static_cast<int>(capital_to_allocate / (option_price * 100));
    return std::max(1, num_contracts);
}

double VolatilityArbitrageStrategy::calculate_signal_confidence(double vol_difference, VolatilityModelType model_used) {
    double base_confidence = std::min(1.0, std::abs(vol_difference) / params_.entry_threshold);
    
    size_t model_idx = static_cast<size_t>(model_used);
    double model_accuracy_factor = (model_idx < model_performance_tracking_.size()) ? 
                                   model_performance_tracking_[model_idx].accuracy : 0.5;

    return base_confidence * model_accuracy_factor;
}

void VolatilityArbitrageStrategy::update_model_performance_tracking(VolatilityModelType model_type_used, double prediction_error, bool trade_successful) {
    size_t idx = static_cast<size_t>(model_type_used);
    if (idx < model_performance_tracking_.size()) {
        auto& perf = model_performance_tracking_[idx];
        perf.avg_error = (perf.avg_error * perf.prediction_count + std::abs(prediction_error)) / (perf.prediction_count + 1);
        perf.prediction_count++;
        
        if (trade_successful) {
            perf.accuracy = std::min(1.0, perf.accuracy + 0.05);
        } else {
            perf.accuracy = std::max(0.1, perf.accuracy - 0.05);
        }
    }
}

VolatilityModelType VolatilityArbitrageStrategy::select_active_model_type() {
    if (model_performance_tracking_[0].accuracy > model_performance_tracking_[1].accuracy + 0.05) {
        return VolatilityModelType::GJR_GARCH_DIRECT;
    } else if (model_performance_tracking_[1].accuracy > model_performance_tracking_[0].accuracy + 0.05) {
        return VolatilityModelType::ENSEMBLE_GJR_HISTORICAL;
    }
    return active_model_type_;
}

void VolatilityArbitrageStrategy::set_parameters(const StrategyParameters& params) {
    params_ = params;
    event_logger_.log_system_status("Strategy parameters updated.");
}

void VolatilityArbitrageStrategy::set_active_volatility_model_type(VolatilityModelType model_type) {
    active_model_type_ = model_type;
    event_logger_.log_system_status("Active volatility model type set to: " + std::to_string(static_cast<int>(model_type)));
}

void VolatilityArbitrageStrategy::on_fill(const IPC::TradingSignalMessage& original_signal, double fill_price, int fill_quantity_signed) {
    if (original_signal.signal_type == 0) { // Entry signal
        PositionInfo pos;
        pos.symbol_id = original_signal.symbol_id;
        pos.quantity = static_cast<double>(fill_quantity_signed);
        pos.entry_price = fill_price;
        pos.current_price = fill_price;
        pos.entry_implied_vol = original_signal.implied_volatility;
        pos.current_implied_vol = original_signal.implied_volatility;
        pos.entry_timestamp = original_signal.timestamp;
        
        current_positions_[pos.symbol_id] = pos;
        trades_entered_++;
        
        event_logger_.log_system_status("Position opened for option " + std::to_string(pos.symbol_id) +
                                     ", Qty: " + std::to_string(pos.quantity) +
                                     ", Price: " + std::to_string(fill_price));
    } else if (original_signal.signal_type == 1) { // Exit signal
        auto it = current_positions_.find(original_signal.symbol_id);
        if (it != current_positions_.end()) {
            double pnl = it->second.quantity * (fill_price - it->second.entry_price);
            if (it->second.quantity < 0) {
                pnl = it->second.quantity * (it->second.entry_price - fill_price);
            }
            total_realized_pnl_ += pnl;
            
            event_logger_.log_system_status("Position closed for option " + std::to_string(original_signal.symbol_id) +
                                         ", P&L: " + std::to_string(pnl));
            current_positions_.erase(it);
        }
    }
}

void VolatilityArbitrageStrategy::on_position_closed(uint32_t symbol_id, double pnl) {
    total_realized_pnl_ += pnl;
    current_positions_.erase(symbol_id);
}

void VolatilityArbitrageStrategy::close_all_positions(std::vector<IPC::TradingSignalMessage>& out_exit_signals) {
    out_exit_signals.clear();
    for (const auto& pair : current_positions_) {
        const PositionInfo& pos = pair.second;
        IPC::TradingSignalMessage signal;
        signal.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::high_resolution_clock::now().time_since_epoch()).count();
        signal.symbol_id = pos.symbol_id;
        signal.quantity = static_cast<int32_t>(-pos.quantity);
        signal.side = (pos.quantity > 0) ? 1 : 0;
        signal.signal_type = 1;
        signal.urgency = 255;
        signal.market_price = pos.current_price;
        out_exit_signals.push_back(signal);
    }
    event_logger_.log_system_status("Generated exit signals for all open positions.");
}

bool VolatilityArbitrageStrategy::calibrate_gjr_model(const std::vector<QuantLib::Real>& historical_returns) {
    if (gjr_garch_model_.calibrate(historical_returns)) {
        event_logger_.log_system_status("GJR-GARCH model calibrated successfully.");
        Alaris::Volatility::initialize_volatility_forecaster(gjr_garch_model_, mem_pool_);
        return true;
    } else {
        event_logger_.log_error("GJR-GARCH model calibration failed or insufficient data.");
        return false;
    }
}

void VolatilityArbitrageStrategy::calibrate_gjr_model(const std::vector<QuantLib::Real>& returns_data) {
    calibrate_gjr_model(returns_data);
}

VolatilityArbitrageStrategy::StrategyPerformanceMetrics VolatilityArbitrageStrategy::get_performance_metrics() const {
    StrategyPerformanceMetrics metrics{};
    metrics.total_pnl = total_realized_pnl_ + total_unrealized_pnl();
    metrics.total_signals_generated = signals_generated_total_;
    metrics.total_trades_entered = trades_entered_;
    metrics.model_performance_stats = model_performance_tracking_;
    return metrics;
}

void VolatilityArbitrageStrategy::reset_performance_metrics() {
    total_realized_pnl_ = 0.0;
    signals_generated_total_ = 0;
    trades_entered_ = 0;
    for (auto& perf : model_performance_tracking_) {
        perf = ModelPerformance();
    }
    event_logger_.log_system_status("Strategy performance metrics reset.");
}

double VolatilityArbitrageStrategy::total_unrealized_pnl() const {
    double unrealized = 0.0;
    for (const auto& pair : current_positions_) {
        unrealized += pair.second.unrealized_pnl;
    }
    return unrealized;
}

} // namespace Alaris::Strategy
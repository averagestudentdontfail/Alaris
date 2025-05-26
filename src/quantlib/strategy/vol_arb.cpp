#include "vol_arb.h"
#include "../ipc/message_types.h" // Already included via vol_arb.h potentially, but good for clarity
#include "../core/event_log.h"   // Already included via vol_arb.h
#include "../volatility/vol_forecast.h" // For global ensemble functions and calculate_forecast_confidence

#include <chrono>  // For timestamps
#include <cmath>   // For std::abs, std::sqrt, std::log, std::max
#include <numeric> // For std::accumulate (if used in performance metrics)
#include <algorithm> // For std::min, std::max

namespace Alaris::Strategy {

VolatilityArbitrageStrategy::VolatilityArbitrageStrategy(
    Pricing::QuantLibALOEngine& pricer,
    Core::PerCycleAllocator& allocator,
    Core::EventLogger& event_logger,
    Core::MemoryPool& mem_pool)
    : mem_pool_(mem_pool),
      pricer_(pricer),
      allocator_(allocator),
      event_logger_(event_logger),
      gjr_garch_model_(mem_pool_), // Initialize owned GJR-GARCH model
      active_model_type_(VolatilityModelType::ENSEMBLE_GJR_HISTORICAL) // Default to ensemble
{
    // Initialize the global volatility forecaster with this strategy's GJR model and memory pool.
    // This makes the ensemble functions in Alaris::Volatility namespace operational.
    Alaris::Volatility::initialize_volatility_forecaster(gjr_garch_model_, mem_pool_);

    event_logger_.log_system_status("VolatilityArbitrageStrategy initialized. Default model: ENSEMBLE_GJR_HISTORICAL.");
    // Initialize model performance tracking for 2 models
    model_performance_tracking_[0] = ModelPerformance(); // For GJR_GARCH_DIRECT
    model_performance_tracking_[1] = ModelPerformance(); // For ENSEMBLE_GJR_HISTORICAL
}

void VolatilityArbitrageStrategy::on_market_data(const IPC::MarketDataMessage& data) {
    latest_market_data_[data.symbol_id] = data; // Assuming data.symbol_id is for the underlying

    // If this market data is for an underlying that our GJR model tracks (e.g., SPY, QQQ)
    // We need a way to map symbol_id to a list of returns or have per-symbol GJR models.
    // For simplicity, assuming one GJR model for a primary underlying, or data.symbol_id IS that primary.
    // In a real system, you'd have GJR models per underlying.

    // Example: Update GJR model if the data is for its specific underlying
    // This requires knowing which symbol_id the gjr_garch_model_ is for, or updating it based on any underlying.
    // For now, let's assume we update it with any underlying's return if we only have one model.
    auto prev_data_it = latest_market_data_.find(data.symbol_id); // This will always find 'data' itself. Need previous.
                                                                  // This logic is a bit flawed as is. Let's refine.
                                                                  // We need to store previous underlying price to calculate return.
    static std::unordered_map<uint32_t, double> previous_underlying_prices;
    if (data.underlying_price > 0) { // Ensure valid current price
        if (previous_underlying_prices.count(data.symbol_id) && previous_underlying_prices[data.symbol_id] > 0) {
            double previous_price = previous_underlying_prices[data.symbol_id];
            double current_price = data.underlying_price; // Assuming this field is populated for underlyings
            if (previous_price > 0 && current_price > 0) { // Ensure valid prices for return calc
                 QuantLib::Real return_val = std::log(current_price / previous_price);
                 gjr_garch_model_.update(return_val); // Update our GJR model
                 // Note: The global VolatilityForecaster uses a *reference* to this gjr_garch_model_,
                 // so it's updated automatically for ensemble calculations.
            }
        }
        previous_underlying_prices[data.symbol_id] = data.underlying_price;
    }


    // Update active positions based on new market data (both underlying and option prices)
    for (auto& pair : current_positions_) {
        PositionInfo& position = pair.second;
        // Find underlying data for this position's option
        auto underlying_it = latest_market_data_.find(position.symbol_id); // Assuming position.symbol_id is the *option's underlying*
        // We also need the option's own market data if available
        // For simplicity, let's assume the incoming 'data' could be for the option itself or its underlying.
        // This part needs careful handling of how option prices are updated.
        // If 'data' is for the option itself:
        // position.current_price = (data.bid + data.ask) / 2.0;
        // position.current_implied_vol = (data.bid_iv + data.ask_iv) / 2.0; (if data is option MD)
        // position.unrealized_pnl = position.quantity * (position.current_price - position.entry_price);

        // if (underlying_it != latest_market_data_.end()) {
        //    if (should_exit_position(position, underlying_it->second, position.current_price, position.current_implied_vol)) {
        //        // Generate exit signal - This requires a mechanism to send signals
        //        // For now, log and mark for closure. Actual signal generation might be in scan_option_chain.
        //        event_logger_.log_system_status("Exit condition met for position on symbol_id (option): " + std::to_string(pair.first));
        //        // Simplified: current_positions_.erase(pair.first); // Proper exit handling is more complex
        //    }
        // }
    }
     // Actual position updates and exit logic would be more involved, likely triggered
     // by specific option market data updates or during a periodic scan.
}


double VolatilityArbitrageStrategy::get_volatility_forecast(uint32_t underlying_symbol_id, size_t horizon) {
    // Note: `underlying_symbol_id` might be used if we have per-symbol models.
    // For now, assuming one primary GJR model and ensemble.
    // The `returns` for the ensemble are typically historical returns of the specific underlying.
    // This needs to be fetched or managed. For simplicity, let's assume `gjr_garch_model_.returns_` can be used
    // or the VolatilityForecaster handles this internally.
    // The global `forecast_volatility_ensemble` takes returns as a parameter.
    // We need to get the relevant returns for `underlying_symbol_id`.

    std::vector<double> historical_returns; // Placeholder - this needs to be populated for the specific underlying
    // Example: if latest_market_data_ stores a history or we have a dedicated return series provider.
    // For now, we pass an empty vector, relying on gjr_garch_model_'s internal returns for its part.
    // The historical component of the ensemble will be weak without proper returns history here.


    switch (active_model_type_) {
        case VolatilityModelType::GJR_GARCH_DIRECT:
            return gjr_garch_model_.forecast_volatility(horizon);
        case VolatilityModelType::ENSEMBLE_GJR_HISTORICAL:
            // The Volatility::forecast_volatility_ensemble function needs the returns for the historical part.
            // We need to provide the relevant returns for `underlying_symbol_id`.
            // gjr_garch_model_.returns_ (deque) needs to be converted to vector<double> if used.
            // This is a simplification; a proper return series for the specific underlying should be used.
            {
                const auto& internal_returns_deque = gjr_garch_model_.sample_size() > 0 ? gjr_garch_model_.returns_ : std::deque<QuantLib::Real>();
                std::vector<double> model_internal_returns(internal_returns_deque.begin(), internal_returns_deque.end());
                return Alaris::Volatility::forecast_volatility_ensemble(horizon, model_internal_returns);
            }
        default:
            event_logger_.log_error("Unknown active_model_type_ in get_volatility_forecast.");
            return gjr_garch_model_.forecast_volatility(horizon); // Fallback
    }
}

double VolatilityArbitrageStrategy::calculate_theoretical_price(
    const IPC::MarketDataMessage& underlying_market_data,
    const Pricing::OptionData& option_to_price, // Contains strike, expiry, type
    double forecast_volatility) {

    Pricing::OptionData pricing_data = option_to_price; // Copy relevant fields
    pricing_data.underlying_price = underlying_market_data.underlying_price;
    pricing_data.volatility = forecast_volatility;
    // Assuming default risk_free_rate and dividend_yield from OptionData's constructor or pricer's setup
    // pricing_data.risk_free_rate = ...; // Get from config or market data if dynamic
    // pricing_data.dividend_yield = ...; // Get from config or market data if dynamic
    
    return pricer_.calculate_option_price(pricing_data);
}


void VolatilityArbitrageStrategy::scan_option_chain(
    uint32_t underlying_symbol_id,
    const std::vector<Pricing::OptionData>& options_in_chain,
    const std::vector<IPC::MarketDataMessage>& option_market_data, // Parallel array to options_in_chain
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
        const Pricing::OptionData& option_details = options_in_chain[i]; // strike, type, expiry, option_symbol_id
        const IPC::MarketDataMessage& current_option_md = option_market_data[i]; // bid, ask, iv for this option

        // Ensure the option market data is valid
        if (current_option_md.ask <= 0 || current_option_md.bid <= 0 || current_option_md.ask <= current_option_md.bid) {
            continue; // Skip if market data for option is invalid
        }
        double option_market_mid_price = (current_option_md.bid + current_option_md.ask) / 2.0;
        double option_market_iv = (current_option_md.bid_iv + current_option_md.ask_iv) / 2.0;
         if (option_market_iv <= 0) option_market_iv = 0.2; // Fallback IV if market IV is zero/invalid


        // Check if we should enter a new position
        // A position is identified by its unique option symbol_id (from option_details.symbol_id)
        if (current_positions_.find(option_details.symbol_id) == current_positions_.end()) { // Not already in this specific option
            if (should_enter_position(underlying_md, option_details, option_market_mid_price, option_market_iv)) {
                double forecast_vol = get_volatility_forecast(underlying_symbol_id); // Forecast for the underlying
                double vol_diff = forecast_vol - option_market_iv; // Key arbitrage signal

                // Use global confidence calculator for forecast quality
                // This needs historical forecasts and realized values, which strategy doesn't directly track for the global func.
                // For now, use a simplified confidence or one based on vol_diff.
                // double confidence = Alaris::Volatility::calculate_forecast_confidence(...);
                double confidence = calculate_signal_confidence(std::abs(vol_diff), active_model_type_);


                if (confidence >= params_.confidence_threshold) {
                    IPC::TradingSignalMessage signal;
                    signal.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
                        std::chrono::high_resolution_clock::now().time_since_epoch()).count();
                    
                    signal.symbol_id = option_details.symbol_id; // The ID of the specific option contract
                    signal.theoretical_price = calculate_theoretical_price(underlying_md, option_details, forecast_vol);
                    signal.market_price = option_market_mid_price;
                    signal.implied_volatility = option_market_iv;
                    signal.forecast_volatility = forecast_vol;
                    signal.confidence = confidence;
                    
                    // Position sizing
                    signal.quantity = static_cast<int32_t>(
                        calculate_position_size(underlying_md.underlying_price, signal.theoretical_price, confidence)
                    );
                    if (signal.quantity == 0) continue; // Do not trade if quantity is zero

                    signal.side = (vol_diff > 0) ? 0 : 1; // 0 for Buy (e.g., buy option if forecast_vol > implied_vol)
                                                          // 1 for Sell (e.g., sell option if forecast_vol < implied_vol)
                                                          // This logic depends on whether we are buying/selling vol (e.g. straddles) or options.
                                                          // If buying option: theory > market. If selling: theory < market.
                                                          // If vol_diff > 0 (forecast > implied), option might be underpriced by market if priced with implied.
                                                          // So, if theoretical (using forecast_vol) > market_price, buy.
                    if (signal.theoretical_price > signal.market_price) { // Buy option
                        signal.side = 0; // BUY
                    } else { // Sell option
                        signal.side = 1; // SELL
                        signal.quantity = -signal.quantity; // Negative for sell orders
                    }

                    signal.urgency = static_cast<uint8_t>(std::min(255.0, confidence * 2.55 * 100.0)); // Scale 0-1 confidence to 0-255
                    signal.signal_type = 0; // Entry signal

                    out_signals.push_back(signal);
                    signals_generated_total_++;
                    event_logger_.log_trading_signal(signal);
                }
            }
        } else { // Check exit conditions for existing positions
            PositionInfo& existing_pos = current_positions_[option_details.symbol_id];
            // Update current market values for the position
            existing_pos.current_price = option_market_mid_price;
            existing_pos.current_implied_vol = option_market_iv;
            existing_pos.unrealized_pnl = existing_pos.quantity * (existing_pos.current_price - existing_pos.entry_price);


            if (should_exit_position(existing_pos, underlying_md, option_market_mid_price, option_market_iv)) {
                IPC::TradingSignalMessage signal;
                 signal.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
                        std::chrono::high_resolution_clock::now().time_since_epoch()).count();
                signal.symbol_id = option_details.symbol_id;
                signal.market_price = option_market_mid_price; // Exit at market.
                signal.quantity = static_cast<int32_t>(-existing_pos.quantity); // Close out full quantity
                signal.side = (existing_pos.quantity > 0) ? 1 : 0; // Opposite of entry
                signal.signal_type = 1; // Exit signal
                signal.urgency = 255; // High urgency for exits

                out_signals.push_back(signal);
                signals_generated_total_++; // Count exit signals too
                event_logger_.log_trading_signal(signal);
                // Position will be removed in on_fill or on_position_closed after confirmation.
            }
        }
    }
}

bool VolatilityArbitrageStrategy::should_enter_position(
    const IPC::MarketDataMessage& underlying_md,
    const Pricing::OptionData& option_details,
    double current_option_market_price,
    double current_option_implied_vol) {

    double forecast_vol = get_volatility_forecast(underlying_md.symbol_id); // For the underlying
    double vol_diff = std::abs(forecast_vol - current_option_implied_vol);

    if (vol_diff < params_.entry_threshold) {
        return false;
    }

    // Additional checks: e.g., ensure market is liquid enough, spread isn't too wide.
    // This example focuses on the volatility difference.
    return true;
}

bool VolatilityArbitrageStrategy::should_exit_position(
    const PositionInfo& position,
    const IPC::MarketDataMessage& underlying_md,
    double current_option_market_price,
    double current_option_implied_vol) {

    double forecast_vol = get_volatility_forecast(underlying_md.symbol_id);
    double vol_diff = std::abs(forecast_vol - current_option_implied_vol);

    // Exit if volatility difference has narrowed significantly (profit taking)
    if (vol_diff < params_.exit_threshold) {
        event_logger_.log_system_status("Exit reason: Volatility difference narrowed for option " + std::to_string(position.symbol_id));
        return true;
    }

    // Exit if unrealized P&L hits risk limit (stop loss)
    // Risk limit could be a percentage of entry price or portfolio, or fixed amount.
    // Assuming risk_limit is a fraction of initial investment in this position.
    double initial_investment = std::abs(position.quantity * position.entry_price);
    if (initial_investment > 0 && std::abs(position.unrealized_pnl) / initial_investment > params_.risk_limit) {
         if (position.unrealized_pnl < 0) { // Only trigger stop-loss on actual losses exceeding risk_limit
            event_logger_.log_system_status("Exit reason: Stop loss triggered for option " + std::to_string(position.symbol_id));
            return true;
         }
    }
    
    // Could add time-based exits, etc.
    return false;
}


double VolatilityArbitrageStrategy::calculate_position_size(
    double underlying_price, double option_price, double confidence) {
    // Simplified position sizing. A real version would consider portfolio risk, margin, etc.
    // Example: Fixed fraction of a conceptual max capital per trade, scaled by confidence.
    if (option_price <= 0) return 0;
    double max_capital_per_trade = 10000; // Example: $10,000
    double capital_to_allocate = max_capital_per_trade * confidence * params_.max_position_size;
    
    int num_contracts = static_cast<int>(capital_to_allocate / (option_price * 100)); // 100 shares per contract
    return std::max(1, num_contracts); // Trade at least 1 contract if conditions met
}

double VolatilityArbitrageStrategy::calculate_signal_confidence(double vol_difference, VolatilityModelType model_used) {
    // Base confidence on how much the vol_difference exceeds a minimum threshold,
    // scaled by the historical accuracy of the model type used.
    double base_confidence = std::min(1.0, std::abs(vol_difference) / params_.entry_threshold); // Normalize by entry threshold
    
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
        // A simple way to update accuracy: if a trade based on this model was successful, increase accuracy.
        // This is a very rough heuristic for accuracy.
        // A better 'accuracy' would be 1 - MAPE of its vol forecasts vs realized/implied vol.
        if (trade_successful) {
            perf.accuracy = std::min(1.0, perf.accuracy + 0.05);
        } else {
            perf.accuracy = std::max(0.1, perf.accuracy - 0.05);
        }
    }
}

VolatilityModelType VolatilityArbitrageStrategy::select_active_model_type() {
    // Simple selection: pick the one with better historical accuracy.
    // More sophisticated: consider Sharpe, stability, recent performance, etc.
    if (model_performance_tracking_[0].accuracy > model_performance_tracking_[1].accuracy + 0.05) { // Threshold to prevent rapid switching
        return VolatilityModelType::GJR_GARCH_DIRECT;
    } else if (model_performance_tracking_[1].accuracy > model_performance_tracking_[0].accuracy + 0.05) {
        return VolatilityModelType::ENSEMBLE_GJR_HISTORICAL;
    }
    return active_model_type_; // Keep current if no clear winner
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
        pos.symbol_id = original_signal.symbol_id; // This is the option's symbol ID
        pos.quantity = static_cast<double>(fill_quantity_signed);
        pos.entry_price = fill_price;
        pos.current_price = fill_price;
        pos.entry_implied_vol = original_signal.implied_volatility; // Store IV at entry
        pos.current_implied_vol = original_signal.implied_volatility;
        pos.entry_timestamp = original_signal.timestamp; // Or use current time
        current_positions_[pos.symbol_id] = pos;
        trades_entered_++;
        event_logger_.log_system_status("Position opened for option " + std::to_string(pos.symbol_id) +
                                     ", Qty: " + std::to_string(pos.quantity) +
                                     ", Price: " + std::to_string(fill_price));
    } else if (original_signal.signal_type == 1) { // Exit signal
        auto it = current_positions_.find(original_signal.symbol_id);
        if (it != current_positions_.end()) {
            double pnl = it->second.quantity * (fill_price - it->second.entry_price); // Simple P&L
             if (it->second.quantity < 0) { // If it was a short position
                pnl = it->second.quantity * (it->second.entry_price - fill_price); // Correct P&L for shorts
            }
            total_realized_pnl_ += pnl;
            event_logger_.log_system_status("Position closed for option " + std::to_string(original_signal.symbol_id) +
                                         ", P&L: " + std::to_string(pnl));
            current_positions_.erase(it);
        }
    }
}

void VolatilityArbitrageStrategy::on_position_closed(uint32_t symbol_id, double pnl) {
    // This might be an alternative way to get P&L feedback if not calculated from on_fill.
    total_realized_pnl_ += pnl;
    current_positions_.erase(symbol_id); // Ensure it's removed
}


void VolatilityArbitrageStrategy::close_all_positions(std::vector<IPC::TradingSignalMessage>& out_exit_signals) {
    out_exit_signals.clear();
    for (const auto& pair : current_positions_) {
        const PositionInfo& pos = pair.second;
        IPC::TradingSignalMessage signal;
        signal.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::high_resolution_clock::now().time_since_epoch()).count();
        signal.symbol_id = pos.symbol_id;
        signal.quantity = static_cast<int32_t>(-pos.quantity); // Opposite quantity to close
        signal.side = (pos.quantity > 0) ? 1 : 0; // Sell if long, Buy if short
        signal.signal_type = 1; // Exit signal
        signal.urgency = 255;   // Max urgency
        signal.market_price = pos.current_price; // Exit at current market price (indicative)
        out_exit_signals.push_back(signal);
    }
    event_logger_.log_system_status("Generated exit signals for all open positions.");
    // Actual removal from current_positions_ happens upon fill confirmation via on_fill/on_position_closed
}


void VolatilityArbitrageStrategy::calibrate_gjr_model(const std::vector<QuantLib::Real>& returns_data) {
    if (gjr_garch_model_.calibrate(returns_data)) {
        event_logger_.log_system_status("GJR-GARCH model calibrated successfully.");
    } else {
        event_logger_.log_error("GJR-GARCH model calibration failed or insufficient data.");
    }
    // After calibration, re-initialize the global forecaster to use the updated model parameters
    Alaris::Volatility::initialize_volatility_forecaster(gjr_garch_model_, mem_pool_);
}

VolatilityArbitrageStrategy::StrategyPerformanceMetrics VolatilityArbitrageStrategy::get_performance_metrics() const {
    StrategyPerformanceMetrics metrics{};
    metrics.total_pnl = total_realized_pnl_ + total_unrealized_pnl(); // Include unrealized P&L
    metrics.total_signals_generated = signals_generated_total_;
    metrics.total_trades_entered = trades_entered_;

    // Simplified win rate: number of positive P&L trades / total trades.
    // This needs more detailed trade tracking. For now, placeholder.
    // metrics.win_rate = ...

    double sum_confidence = 0;
    // This needs actual signal data storage to average confidence. Placeholder.
    // if (signals_generated_total_ > 0) metrics.avg_signal_confidence = sum_confidence / signals_generated_total_;
    
    metrics.model_performance_stats = model_performance_tracking_;
    return metrics;
}

void VolatilityArbitrageStrategy::reset_performance_metrics() {
    total_realized_pnl_ = 0.0;
    signals_generated_total_ = 0;
    trades_entered_ = 0;
    for (auto& perf : model_performance_tracking_) {
        perf = ModelPerformance(); // Reset to defaults
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
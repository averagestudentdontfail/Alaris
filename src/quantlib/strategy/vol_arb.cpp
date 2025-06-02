// src/quantlib/strategy/vol_arb.cpp
#include "vol_arb.h"
#include "../volatility/garch_wrapper.h"  // Changed from gjrgarch_wrapper.h
#include <algorithm>
#include <cmath>
#include <numeric>
#include <random>
#include <chrono>

namespace Alaris::Strategy {

VolatilityArbitrageStrategy::VolatilityArbitrageStrategy(
    Pricing::QuantLibALOEngine& pricer,
    Core::PerCycleAllocator& allocator,
    Core::EventLogger& event_logger,
    Core::MemoryPool& mem_pool)
    : pricer_(pricer),
      allocator_(allocator),
      event_logger_(event_logger),
      mem_pool_(mem_pool) {
    
    // Initialize volatility models - changed to standard GARCH
    garch_model_ = std::make_unique<Volatility::QuantLibGARCHModel>(mem_pool_);
    vol_forecaster_ = std::make_unique<Volatility::VolatilityForecaster>(*garch_model_, mem_pool_);
    
    // Initialize default parameters
    params_ = StrategyParameters{};
    
    // Initialize portfolio metrics
    portfolio_metrics_ = PortfolioRiskMetrics{};
    
    // Initialize market regime
    current_regime_.vol_regime = MarketRegime::VolRegime::MEDIUM;
    current_regime_.trend_regime = MarketRegime::TrendRegime::SIDEWAYS;
    current_regime_.liquidity_regime = MarketRegime::LiquidityRegime::NORMAL;
    current_regime_.regime_confidence = 0.5;
    
    event_logger_.log_system_status("Advanced VolatilityArbitrageStrategy initialized with standard GARCH");
}

void VolatilityArbitrageStrategy::set_parameters(const StrategyParameters& params) {
    params_ = params;
    event_logger_.log_system_status("Strategy parameters updated - Mode: " + 
                                   std::to_string(static_cast<int>(params_.strategy_mode)));
}

void VolatilityArbitrageStrategy::set_strategy_mode(StrategyParameters::Mode mode) {
    params_.strategy_mode = mode;
    event_logger_.log_system_status("Strategy mode changed to: " + std::to_string(static_cast<int>(mode)));
}

void VolatilityArbitrageStrategy::on_market_data(const IPC::MarketDataMessage& market_data) {
    latest_market_data_[market_data.symbol_id] = market_data;
    
    // Update price history for volatility calculations
    if (market_data.underlying_price > 0) {
        auto& price_hist = price_history_[market_data.symbol_id];
        price_hist.push_back(market_data.underlying_price);
        
        // Maintain history length
        if (price_hist.size() > 252) {
            price_hist.pop_front();
        }
        
        // Calculate returns and update GARCH model
        if (price_hist.size() > 1) {
            double current_price = price_hist.back();
            double previous_price = *(price_hist.rbegin() + 1);
            double return_val = std::log(current_price / previous_price);
            garch_model_->update(return_val);
        }
    }
    
    // Update volatility history
    if (market_data.bid_iv > 0 && market_data.ask_iv > 0) {
        double avg_iv = (market_data.bid_iv + market_data.ask_iv) / 2.0;
        auto& vol_hist = vol_history_[market_data.symbol_id];
        vol_hist.push_back(avg_iv);
        
        if (vol_hist.size() > 252) {
            vol_hist.pop_front();
        }
    }
    
    // Update existing positions with new market data
    auto pos_it = positions_.find(market_data.symbol_id);
    if (pos_it != positions_.end()) {
        EnhancedPosition& position = pos_it->second;
        position.current_price = (market_data.bid + market_data.ask) / 2.0;
        position.current_implied_vol = (market_data.bid_iv + market_data.ask_iv) / 2.0;
        position.unrealized_pnl = position.quantity * (position.current_price - position.entry_price);
        position.last_update_timestamp = market_data.timestamp_ns;
        
        // Update max unrealized PnL for trailing stops
        if (position.unrealized_pnl > position.max_unrealized_pnl) {
            position.max_unrealized_pnl = position.unrealized_pnl;
        }
    }
    
    // Periodically update market regime and portfolio metrics
    uint64_t current_time = std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::high_resolution_clock::now().time_since_epoch()).count();
    
    if (current_time - last_regime_update_ > 300000000000ULL) {  // 5 minutes
        update_market_regime(market_data.symbol_id);
        last_regime_update_ = current_time;
    }
    
    if (current_time - last_portfolio_rebalance_ > 900000000000ULL) {  // 15 minutes
        update_portfolio_metrics();
        last_portfolio_rebalance_ = current_time;
    }
}

void VolatilityArbitrageStrategy::update_market_regime(uint32_t underlying_symbol) {
    auto price_it = price_history_.find(underlying_symbol);
    auto vol_it = vol_history_.find(underlying_symbol);
    
    if (price_it == price_history_.end() || vol_it == vol_history_.end()) {
        return;
    }
    
    const auto& prices = price_it->second;
    const auto& vols = vol_it->second;
    
    if (prices.size() < params_.regime_lookback_days || vols.size() < params_.regime_lookback_days) {
        return;
    }
    
    // Calculate realized volatility
    std::vector<double> returns;
    for (size_t i = 1; i < prices.size(); ++i) {
        returns.push_back(std::log(prices[i] / prices[i-1]));
    }
    
    double realized_vol = 0.0;
    if (!returns.empty()) {
        double mean_return = std::accumulate(returns.begin(), returns.end(), 0.0) / returns.size();
        double variance = 0.0;
        for (double ret : returns) {
            variance += std::pow(ret - mean_return, 2);
        }
        variance /= (returns.size() - 1);
        realized_vol = std::sqrt(variance * 252.0);
    }
    
    // Calculate average implied volatility
    double avg_implied_vol = std::accumulate(vols.begin(), vols.end(), 0.0) / vols.size();
    
    // Update regime
    current_regime_.current_realized_vol = realized_vol;
    current_regime_.current_implied_vol = avg_implied_vol;
    current_regime_.vol_risk_premium = avg_implied_vol - realized_vol;
    
    // Determine volatility regime
    if (realized_vol < params_.low_vol_threshold) {
        current_regime_.vol_regime = MarketRegime::VolRegime::LOW;
    } else if (realized_vol > params_.high_vol_threshold) {
        current_regime_.vol_regime = MarketRegime::VolRegime::HIGH;
    } else {
        current_regime_.vol_regime = MarketRegime::VolRegime::MEDIUM;
    }
    
    // Determine trend regime based on price momentum
    if (prices.size() >= 20) {
        double recent_return = std::log(prices.back() / prices[prices.size() - 20]);
        if (recent_return > 0.05) {
            current_regime_.trend_regime = MarketRegime::TrendRegime::TRENDING_UP;
        } else if (recent_return < -0.05) {
            current_regime_.trend_regime = MarketRegime::TrendRegime::TRENDING_DOWN;
        } else {
            current_regime_.trend_regime = MarketRegime::TrendRegime::SIDEWAYS;
        }
    }
    
    // Update forward-looking indicators
    if (garch_model_->is_calibrated()) {
        current_regime_.expected_vol_next_week = garch_model_->forecast_volatility(5);
    }
}

void VolatilityArbitrageStrategy::scan_and_generate_signals(
    uint32_t underlying_symbol,
    const std::vector<Pricing::OptionData>& option_chain,
    const std::vector<IPC::MarketDataMessage>& option_market_data,
    std::vector<IPC::TradingSignalMessage>& out_signals) {
    
    out_signals.clear();
    
    if (option_chain.size() != option_market_data.size()) {
        event_logger_.log_error("Option chain and market data size mismatch");
        return;
    }
    
    // Analyze volatility surface first
    analyze_volatility_surface(underlying_symbol, option_chain, option_market_data);
    
    // Generate signals based on strategy mode
    std::vector<IPC::TradingSignalMessage> mode_signals;
    
    switch (params_.strategy_mode) {
        case StrategyParameters::Mode::DELTA_NEUTRAL:
            mode_signals = generate_delta_neutral_signals(underlying_symbol, option_chain, option_market_data);
            break;
        case StrategyParameters::Mode::GAMMA_SCALPING:
            mode_signals = generate_gamma_scalping_signals(underlying_symbol, option_chain, option_market_data);
            break;
        case StrategyParameters::Mode::VOLATILITY_TIMING:
            mode_signals = generate_volatility_timing_signals(underlying_symbol, option_chain, option_market_data);
            break;
        case StrategyParameters::Mode::RELATIVE_VALUE:
            // Implement relative value strategy
            break;
    }
    
    // Filter signals through risk management
    for (const auto& signal : mode_signals) {
        if (check_position_limits(signal) && check_correlation_limits(signal.symbol_id, signal.quantity)) {
            out_signals.push_back(signal);
            signals_generated_.fetch_add(1, std::memory_order_relaxed);
        }
    }
    
    // Generate hedge signals if needed
    if (params_.auto_hedge_enabled) {
        auto hedge_signals = generate_hedge_signals();
        out_signals.insert(out_signals.end(), hedge_signals.begin(), hedge_signals.end());
    }
    
    // Log signal generation
    if (!out_signals.empty()) {
        event_logger_.log_system_status("Generated " + std::to_string(out_signals.size()) + 
                                       " signals for underlying " + std::to_string(underlying_symbol));
    }
}

std::vector<IPC::TradingSignalMessage> VolatilityArbitrageStrategy::generate_delta_neutral_signals(
    uint32_t underlying_symbol,
    const std::vector<Pricing::OptionData>& option_chain,
    const std::vector<IPC::MarketDataMessage>& option_market_data) {
    
    std::vector<IPC::TradingSignalMessage> signals;
    
    auto underlying_md_it = latest_market_data_.find(underlying_symbol);
    if (underlying_md_it == latest_market_data_.end()) {
        return signals;
    }
    
    const auto& underlying_md = underlying_md_it->second;
    
    for (size_t i = 0; i < option_chain.size(); ++i) {
        const auto& option = option_chain[i];
        const auto& market_data = option_market_data[i];
        
        if (market_data.ask <= market_data.bid || market_data.ask <= 0) {
            continue;
        }
        
        double market_mid = (market_data.bid + market_data.ask) / 2.0;
        double market_iv = (market_data.bid_iv + market_data.ask_iv) / 2.0;
        
        // Get volatility forecast using standard GARCH
        std::vector<double> returns;
        auto price_it = price_history_.find(underlying_symbol);
        if (price_it != price_history_.end() && price_it->second.size() > 1) {
            const auto& prices = price_it->second;
            for (size_t j = 1; j < prices.size(); ++j) {
                returns.push_back(std::log(prices[j] / prices[j-1]));
            }
        }
        
        double forecast_vol = vol_forecaster_->generate_ensemble_forecast(1, returns);
        double vol_diff = std::abs(forecast_vol - market_iv);
        
        // Calculate confidence based on model accuracy and market regime
        double base_confidence = std::min(1.0, vol_diff / params_.vol_difference_threshold);
        double regime_adjustment = (current_regime_.regime_confidence - 0.5) * 0.4;
        double confidence = std::max(0.0, std::min(1.0, base_confidence + regime_adjustment));
        
        if (vol_diff >= params_.vol_difference_threshold && confidence >= params_.confidence_threshold) {
            // Calculate Greeks for position sizing
            Pricing::OptionGreeks greeks = pricer_.calculate_greeks(option);
            
            // Calculate Kelly position size
            double edge = vol_diff / market_iv;
            double win_prob = confidence;
            double avg_win_loss = 2.0;
            double kelly_size = calculate_kelly_position_size(edge, forecast_vol, win_prob, avg_win_loss);
            
            // Apply VAR adjustment
            double var_adjusted_size = calculate_var_adjusted_size(option, kelly_size);
            
            if (var_adjusted_size > 0) {
                IPC::TradingSignalMessage signal;
                signal.timestamp_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
                    std::chrono::high_resolution_clock::now().time_since_epoch()).count();
                signal.symbol_id = option.symbol_id;
                signal.theoretical_price = pricer_.calculate_option_price(option);
                signal.market_price = market_mid;
                signal.implied_volatility = market_iv;
                signal.forecast_volatility = forecast_vol;
                signal.confidence = confidence;
                signal.quantity = static_cast<int32_t>(var_adjusted_size);
                
                // Determine direction based on vol forecast vs market
                if (forecast_vol > market_iv) {
                    signal.side = 0;  // Buy (long volatility)
                } else {
                    signal.side = 1;  // Sell (short volatility)
                    signal.quantity = -signal.quantity;
                }
                
                signal.urgency = static_cast<uint8_t>(std::min(255.0, confidence * 255.0));
                signal.signal_type = 0;  // Entry signal
                
                signals.push_back(signal);
            }
        }
    }
    
    return signals;
}

std::vector<IPC::TradingSignalMessage> VolatilityArbitrageStrategy::generate_gamma_scalping_signals(
    uint32_t underlying_symbol,
    const std::vector<Pricing::OptionData>& option_chain,
    const std::vector<IPC::MarketDataMessage>& option_market_data) {
    
    std::vector<IPC::TradingSignalMessage> signals;
    
    // Gamma scalping focuses on options with high gamma and manageable theta decay
    for (size_t i = 0; i < option_chain.size(); ++i) {
        const auto& option = option_chain[i];
        const auto& market_data = option_market_data[i];
        
        if (market_data.ask <= market_data.bid) continue;
        
        // Calculate Greeks
        Pricing::OptionGreeks greeks = pricer_.calculate_greeks(option);
        
        // Look for high gamma, reasonable theta ratio
        double gamma_theta_ratio = std::abs(greeks.gamma / greeks.theta);
        
        // Focus on ATM options with 2-8 weeks to expiry
        double moneyness = option.underlying_price / option.strike_price;
        bool is_atm = (moneyness > 0.95 && moneyness < 1.05);
        bool good_expiry = (option.time_to_expiry > 0.04 && option.time_to_expiry < 0.15);
        
        if (is_atm && good_expiry && gamma_theta_ratio > 10.0 && greeks.gamma > 0.01) {
            // Check if underlying has been moving enough to justify gamma scalping
            auto price_it = price_history_.find(underlying_symbol);
            if (price_it != price_history_.end() && price_it->second.size() >= 10) {
                const auto& prices = price_it->second;
                
                // Calculate recent volatility
                std::vector<double> recent_returns;
                for (size_t j = prices.size() - 10; j < prices.size(); ++j) {
                    if (j > 0) {
                        recent_returns.push_back(std::log(prices[j] / prices[j-1]));
                    }
                }
                
                if (!recent_returns.empty()) {
                    double recent_vol = 0.0;
                    double mean_return = std::accumulate(recent_returns.begin(), recent_returns.end(), 0.0) / recent_returns.size();
                    for (double ret : recent_returns) {
                        recent_vol += std::pow(ret - mean_return, 2);
                    }
                    recent_vol = std::sqrt(recent_vol / (recent_returns.size() - 1) * 252.0);
                    
                    // Enter gamma scalping if recent vol is high enough
                    if (recent_vol > 0.15) {
                        double market_mid = (market_data.bid + market_data.ask) / 2.0;
                        double position_size = std::min(100.0, 10000.0 / market_mid);
                        
                        IPC::TradingSignalMessage signal;
                        signal.timestamp_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
                            std::chrono::high_resolution_clock::now().time_since_epoch()).count();
                        signal.symbol_id = option.symbol_id;
                        signal.theoretical_price = greeks.price;
                        signal.market_price = market_mid;
                        signal.implied_volatility = (market_data.bid_iv + market_data.ask_iv) / 2.0;
                        signal.forecast_volatility = recent_vol;
                        signal.confidence = std::min(1.0, recent_vol / 0.30);
                        signal.quantity = static_cast<int32_t>(position_size);
                        signal.side = 0;  // Long gamma
                        signal.urgency = 128;  // Medium urgency
                        signal.signal_type = 0;
                        
                        signals.push_back(signal);
                    }
                }
            }
        }
    }
    
    return signals;
}

std::vector<IPC::TradingSignalMessage> VolatilityArbitrageStrategy::generate_volatility_timing_signals(
    uint32_t underlying_symbol,
    const std::vector<Pricing::OptionData>& option_chain,
    const std::vector<IPC::MarketDataMessage>& option_market_data) {
    
    std::vector<IPC::TradingSignalMessage> signals;
    
    // Volatility timing based on regime changes and mean reversion
    double vol_risk_premium = current_regime_.vol_risk_premium;
    
    // Look for regime transitions or extreme vol risk premium
    bool regime_transition = (current_regime_.regime_confidence < 0.7);
    bool extreme_premium = (std::abs(vol_risk_premium) > 0.05);
    
    if (!regime_transition && !extreme_premium) {
        return signals;
    }
    
    for (size_t i = 0; i < option_chain.size(); ++i) {
        const auto& option = option_chain[i];
        const auto& market_data = option_market_data[i];
        
        if (market_data.ask <= market_data.bid) continue;
        
        // Focus on liquid options with good volume
        if (market_data.bid_size < 10 || market_data.ask_size < 10) continue;
        
        double market_iv = (market_data.bid_iv + market_data.ask_iv) / 2.0;
        double forecast_vol = current_regime_.expected_vol_next_week;
        
        // Mean reversion signal
        if (extreme_premium) {
            double signal_strength = std::min(1.0, std::abs(vol_risk_premium) / 0.10);
            
            if (vol_risk_premium > 0.05) {
                // Implied vol too high, sell volatility
                double position_size = signal_strength * 50.0;
                
                IPC::TradingSignalMessage signal;
                signal.symbol_id = option.symbol_id;
                signal.market_price = (market_data.bid + market_data.ask) / 2.0;
                signal.implied_volatility = market_iv;
                signal.forecast_volatility = forecast_vol;
                signal.confidence = signal_strength;
                signal.quantity = -static_cast<int32_t>(position_size);
                signal.side = 1;
                signal.signal_type = 0;
                signal.urgency = static_cast<uint8_t>(signal_strength * 200);
                
                signals.push_back(signal);
                
            } else if (vol_risk_premium < -0.05) {
                // Implied vol too low, buy volatility
                double position_size = signal_strength * 50.0;
                
                IPC::TradingSignalMessage signal;
                signal.symbol_id = option.symbol_id;
                signal.market_price = (market_data.bid + market_data.ask) / 2.0;
                signal.implied_volatility = market_iv;
                signal.forecast_volatility = forecast_vol;
                signal.confidence = signal_strength;
                signal.quantity = static_cast<int32_t>(position_size);
                signal.side = 0;
                signal.signal_type = 0;
                signal.urgency = static_cast<uint8_t>(signal_strength * 200);
                
                signals.push_back(signal);
            }
        }
    }
    
    return signals;
}

double VolatilityArbitrageStrategy::calculate_kelly_position_size(
    double edge, double volatility, double win_probability, double avg_win_loss_ratio) {
    
    double q = 1.0 - win_probability;
    double b = avg_win_loss_ratio;
    
    double kelly_fraction = (win_probability * b - q) / b;
    kelly_fraction = std::max(0.0, kelly_fraction);
    
    // Apply conservative scaling
    kelly_fraction *= params_.kelly_fraction;
    kelly_fraction = std::min(kelly_fraction, params_.max_kelly_position);
    
    double base_position_size = 100.0;
    return kelly_fraction * base_position_size;
}

double VolatilityArbitrageStrategy::calculate_var_adjusted_size(
    const Pricing::OptionData& option, double base_size) {
    
    // Calculate position VaR and adjust size accordingly
    Pricing::OptionGreeks greeks = pricer_.calculate_greeks(option);
    
    // Simplified VaR calculation based on delta and gamma
    double underlying_vol = current_regime_.current_realized_vol;
    double daily_underlying_move = underlying_vol / std::sqrt(252.0);
    
    // 1-day 95% VaR approximation
    double delta_var = std::abs(greeks.delta * base_size * option.underlying_price * daily_underlying_move * 1.645);
    double gamma_var = 0.5 * greeks.gamma * base_size * std::pow(option.underlying_price * daily_underlying_move * 1.645, 2);
    double total_var = delta_var + gamma_var;
    
    // Limit position size based on VaR
    double max_var_per_position = 1000.0;
    if (total_var > max_var_per_position) {
        base_size *= (max_var_per_position / total_var);
    }
    
    return std::max(1.0, base_size);
}

bool VolatilityArbitrageStrategy::check_position_limits(const IPC::TradingSignalMessage& signal) {
    // Check individual position size limits
    if (std::abs(signal.quantity) * signal.market_price > 10000.0) {
        return false;
    }
    
    // Check total number of positions
    if (positions_.size() >= 20) {
        return false;
    }
    
    // Check if we already have a position in this symbol
    if (positions_.find(signal.symbol_id) != positions_.end()) {
        return false;
    }
    
    return true;
}

bool VolatilityArbitrageStrategy::check_correlation_limits(uint32_t symbol_id, double position_size) {
    // Simplified correlation check
    uint32_t underlying_base = symbol_id / 1000;
    size_t same_underlying_positions = 0;
    
    for (const auto& pos_pair : positions_) {
        uint32_t pos_underlying = pos_pair.first / 1000;
        if (pos_underlying == underlying_base) {
            same_underlying_positions++;
        }
    }
    
    return same_underlying_positions < 5;
}

std::vector<IPC::TradingSignalMessage> VolatilityArbitrageStrategy::generate_hedge_signals() {
    std::vector<IPC::TradingSignalMessage> hedge_signals;
    
    // Calculate portfolio Greeks
    update_portfolio_metrics();
    
    // Check if hedging is needed
    bool need_delta_hedge = std::abs(portfolio_metrics_.total_delta) > params_.hedge_threshold_delta;
    bool need_gamma_hedge = std::abs(portfolio_metrics_.total_gamma) > params_.hedge_threshold_gamma;
    
    if (!need_delta_hedge && !need_gamma_hedge) {
        return hedge_signals;
    }
    
    // For simplicity, hedge with underlying (delta hedge) or ATM options (gamma hedge)
    if (need_delta_hedge) {
        for (const auto& md_pair : latest_market_data_) {
            if (md_pair.second.underlying_price > 0) {
                double hedge_quantity = -portfolio_metrics_.total_delta / 100.0;
                
                if (std::abs(hedge_quantity) > 0.1) {
                    IPC::TradingSignalMessage hedge_signal;
                    hedge_signal.symbol_id = md_pair.first;
                    hedge_signal.quantity = static_cast<int32_t>(hedge_quantity);
                    hedge_signal.side = (hedge_quantity > 0) ? 0 : 1;
                    hedge_signal.signal_type = 2;  // Hedge signal
                    hedge_signal.urgency = 255;
                    hedge_signal.market_price = md_pair.second.underlying_price;
                    
                    hedge_signals.push_back(hedge_signal);
                    hedge_trades_.fetch_add(1, std::memory_order_relaxed);
                    break;
                }
            }
        }
    }
    
    return hedge_signals;
}

void VolatilityArbitrageStrategy::update_portfolio_metrics() {
    portfolio_metrics_ = PortfolioRiskMetrics{};
    
    double total_notional = 0.0;
    
    for (const auto& pos_pair : positions_) {
        const auto& position = pos_pair.second;
        
        portfolio_metrics_.total_delta += position.current_greeks.delta * position.quantity;
        portfolio_metrics_.total_gamma += position.current_greeks.gamma * position.quantity;
        portfolio_metrics_.total_vega += position.current_greeks.vega * position.quantity;
        portfolio_metrics_.total_theta += position.current_greeks.theta * position.quantity;
        portfolio_metrics_.total_rho += position.current_greeks.rho * position.quantity;
        
        total_notional += std::abs(position.quantity * position.current_price);
    }
    
    portfolio_metrics_.total_notional = total_notional;
    portfolio_metrics_.active_positions = positions_.size();
    
    // Calculate portfolio VaR (simplified)
    portfolio_metrics_.portfolio_var_1day = calculate_portfolio_var(0.05, 1);
    portfolio_metrics_.portfolio_var_10day = calculate_portfolio_var(0.05, 10);
    
    // Update total P&L
    total_unrealized_pnl_ = 0.0;
    for (const auto& pos_pair : positions_) {
        total_unrealized_pnl_ += pos_pair.second.unrealized_pnl;
    }
}

void VolatilityArbitrageStrategy::emergency_liquidation(std::vector<IPC::TradingSignalMessage>& out_signals) {
    uint64_t current_time = std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::high_resolution_clock::now().time_since_epoch()).count();
    
    out_signals.clear();
    
    // Create exit signals for all active positions
    for (auto& pos_pair : positions_) {
        EnhancedPosition& position = pos_pair.second;
        
        if (position.state == EnhancedPosition::State::ACTIVE) {
            IPC::TradingSignalMessage exit_signal;
            exit_signal.timestamp_ns = current_time;
            exit_signal.symbol_id = position.symbol_id;
            exit_signal.quantity = static_cast<int32_t>(-position.quantity);  // Opposite of current position
            exit_signal.side = (position.quantity > 0) ? 1 : 0;  // Sell if long, buy if short
            exit_signal.signal_type = 1;  // Exit signal
            exit_signal.urgency = 255;    // Maximum urgency
            exit_signal.market_price = position.current_price;
            exit_signal.implied_volatility = position.current_implied_vol;
            exit_signal.forecast_volatility = 0.0;  // Not relevant for emergency exit
            exit_signal.confidence = 1.0;  // Maximum confidence for emergency liquidation
            
            out_signals.push_back(exit_signal);
            
            // Mark position as being liquidated
            position.state = EnhancedPosition::State::STOP_LOSS_HIT;  // Use existing state
            
            event_logger_.log_system_status("Emergency liquidation signal created for position " + 
                                           std::to_string(position.symbol_id) + 
                                           ", quantity: " + std::to_string(exit_signal.quantity));
        }
    }
    
    // Log the emergency liquidation event
    event_logger_.log_system_status("Emergency liquidation initiated - " + 
                                   std::to_string(out_signals.size()) + " positions to be liquidated");
    
    // Reset portfolio metrics since we're liquidating everything
    portfolio_metrics_ = PortfolioRiskMetrics{};
}

double VolatilityArbitrageStrategy::calculate_portfolio_var(double confidence_level, size_t horizon_days) {
    if (positions_.empty()) return 0.0;
    
    // Simplified VaR calculation using delta-normal method
    double portfolio_value = 0.0;
    double portfolio_volatility = 0.0;
    
    for (const auto& pos_pair : positions_) {
        const auto& position = pos_pair.second;
        double position_value = position.quantity * position.current_price;
        portfolio_value += position_value;
        
        double position_vol = 0.20;  // Default 20% volatility
        portfolio_volatility += std::pow(position_value * position_vol, 2);
    }
    
    portfolio_volatility = std::sqrt(portfolio_volatility);
    
    // Scale for horizon and confidence level
    double z_score = (confidence_level == 0.05) ? 1.645 : 2.326;
    double horizon_scaling = std::sqrt(static_cast<double>(horizon_days));
    
    return portfolio_volatility * z_score * horizon_scaling;
}

void VolatilityArbitrageStrategy::analyze_volatility_surface(
    uint32_t underlying_symbol,
    const std::vector<Pricing::OptionData>& options,
    const std::vector<IPC::MarketDataMessage>& market_data) {
    
    vol_surface_analysis_.clear();
    
    auto underlying_md_it = latest_market_data_.find(underlying_symbol);
    if (underlying_md_it == latest_market_data_.end()) return;
    
    double spot = underlying_md_it->second.underlying_price;
    if (spot <= 0) return;
    
    for (size_t i = 0; i < options.size() && i < market_data.size(); ++i) {
        const auto& option = options[i];
        const auto& md = market_data[i];
        
        if (md.ask <= md.bid || md.ask <= 0) continue;
        
        VolSurfacePoint point;
        point.strike_ratio = option.strike_price / spot;
        point.time_to_expiry = option.time_to_expiry;
        point.implied_vol = (md.bid_iv + md.ask_iv) / 2.0;
        
        // Get model volatility forecast using standard GARCH
        std::vector<double> returns;
        auto price_it = price_history_.find(underlying_symbol);
        if (price_it != price_history_.end()) {
            const auto& prices = price_it->second;
            for (size_t j = 1; j < prices.size(); ++j) {
                returns.push_back(std::log(prices[j] / prices[j-1]));
            }
        }
        
        point.model_vol = vol_forecaster_->generate_ensemble_forecast(1, returns);
        point.arbitrage_score = std::abs(point.implied_vol - point.model_vol) / point.implied_vol;
        point.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::high_resolution_clock::now().time_since_epoch()).count();
        
        vol_surface_analysis_.push_back(point);
    }
    
    // Sort by arbitrage score for prioritization
    std::sort(vol_surface_analysis_.begin(), vol_surface_analysis_.end(),
              [](const VolSurfacePoint& a, const VolSurfacePoint& b) {
                  return a.arbitrage_score > b.arbitrage_score;
              });
}

void VolatilityArbitrageStrategy::on_fill(const IPC::TradingSignalMessage& signal, 
                                         double fill_price, int fill_quantity, 
                                         uint64_t fill_timestamp) {
    
    if (signal.signal_type == 0) {  // Entry signal
        EnhancedPosition position;
        position.symbol_id = signal.symbol_id;
        position.quantity = static_cast<double>(fill_quantity);
        position.entry_price = fill_price;
        position.current_price = fill_price;
        position.entry_implied_vol = signal.implied_volatility;
        position.current_implied_vol = signal.implied_volatility;
        position.entry_timestamp = fill_timestamp;
        position.last_update_timestamp = fill_timestamp;
        position.vol_forecast_at_entry = signal.forecast_volatility;
        position.confidence_at_entry = signal.confidence;
        position.kelly_size_at_entry = std::abs(static_cast<double>(fill_quantity));
        position.state = EnhancedPosition::State::ACTIVE;
        
        // Calculate initial Greeks (approximation)
        position.entry_greeks.price = fill_price;
        position.entry_greeks.delta = (signal.side == 0) ? 0.5 : -0.5;
        position.current_greeks = position.entry_greeks;
        
        positions_[signal.symbol_id] = position;
        trades_executed_.fetch_add(1, std::memory_order_relaxed);
        
        event_logger_.log_system_status("Position opened - Symbol: " + std::to_string(signal.symbol_id) +
                                       ", Qty: " + std::to_string(fill_quantity) +
                                       ", Price: " + std::to_string(fill_price));
        
    } else if (signal.signal_type == 1) {  // Exit signal
        auto pos_it = positions_.find(signal.symbol_id);
        if (pos_it != positions_.end()) {
            EnhancedPosition& position = pos_it->second;
            
            // Calculate realized P&L
            double pnl = position.quantity * (fill_price - position.entry_price);
            position.realized_pnl = pnl;
            total_realized_pnl_ += pnl;
            
            event_logger_.log_system_status("Position closed - Symbol: " + std::to_string(signal.symbol_id) +
                                           ", P&L: " + std::to_string(pnl));
            
            positions_.erase(pos_it);
        }
    }
}

void VolatilityArbitrageStrategy::apply_stop_losses(std::vector<IPC::TradingSignalMessage>& out_signals) {
    uint64_t current_time = std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::high_resolution_clock::now().time_since_epoch()).count();
    
    for (auto& pos_pair : positions_) {
        EnhancedPosition& position = pos_pair.second;
        
        if (position.state != EnhancedPosition::State::ACTIVE) continue;
        
        double initial_value = std::abs(position.quantity * position.entry_price);
        double loss_threshold = initial_value * params_.stop_loss_percent;
        
        // Check stop loss
        if (position.unrealized_pnl < -loss_threshold) {
            IPC::TradingSignalMessage exit_signal;
            exit_signal.timestamp_ns = current_time;
            exit_signal.symbol_id = position.symbol_id;
            exit_signal.quantity = static_cast<int32_t>(-position.quantity);
            exit_signal.side = (position.quantity > 0) ? 1 : 0;
            exit_signal.signal_type = 1;  // Exit signal
            exit_signal.urgency = 255;
            exit_signal.market_price = position.current_price;
            
            out_signals.push_back(exit_signal);
            position.state = EnhancedPosition::State::STOP_LOSS_HIT;
            
            event_logger_.log_system_status("Stop loss triggered for position " + std::to_string(position.symbol_id));
        }
        
        // Check trailing stop
        double trailing_threshold = position.max_unrealized_pnl * params_.trailing_stop_percent;
        if (position.max_unrealized_pnl > 0 && 
            position.unrealized_pnl < (position.max_unrealized_pnl - trailing_threshold)) {
            
            IPC::TradingSignalMessage exit_signal;
            exit_signal.timestamp_ns = current_time;
            exit_signal.symbol_id = position.symbol_id;
            exit_signal.quantity = static_cast<int32_t>(-position.quantity);
            exit_signal.side = (position.quantity > 0) ? 1 : 0;
            exit_signal.signal_type = 1;
            exit_signal.urgency = 200;
            exit_signal.market_price = position.current_price;
            
            out_signals.push_back(exit_signal);
            position.state = EnhancedPosition::State::TRAILING_STOP_HIT;
            
            event_logger_.log_system_status("Trailing stop triggered for position " + std::to_string(position.symbol_id));
        }
    }
}

void VolatilityArbitrageStrategy::apply_profit_targets(std::vector<IPC::TradingSignalMessage>& out_signals) {
    uint64_t current_time = std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::high_resolution_clock::now().time_since_epoch()).count();
    
    for (auto& pos_pair : positions_) {
        EnhancedPosition& position = pos_pair.second;
        
        if (position.state != EnhancedPosition::State::ACTIVE) continue;
        
        double initial_value = std::abs(position.quantity * position.entry_price);
        double profit_threshold = initial_value * params_.profit_target_percent;
        
        if (position.unrealized_pnl > profit_threshold) {
            IPC::TradingSignalMessage exit_signal;
            exit_signal.timestamp_ns = current_time;
            exit_signal.symbol_id = position.symbol_id;
            exit_signal.quantity = static_cast<int32_t>(-position.quantity);
            exit_signal.side = (position.quantity > 0) ? 1 : 0;
            exit_signal.signal_type = 1;
            exit_signal.urgency = 150;
            exit_signal.market_price = position.current_price;
            
            out_signals.push_back(exit_signal);
            position.state = EnhancedPosition::State::PROFIT_TARGET_HIT;
            
            event_logger_.log_system_status("Profit target hit for position " + std::to_string(position.symbol_id));
        }
    }
}

bool VolatilityArbitrageStrategy::calibrate_volatility_models(
    const std::unordered_map<uint32_t, std::vector<double>>& returns_by_asset) {
    
    bool overall_success = true;
    
    for (const auto& asset_returns : returns_by_asset) {
        if (asset_returns.second.size() < 50) continue;
        
        try {
            if (!garch_model_->calibrate(asset_returns.second)) {
                event_logger_.log_warning("GARCH calibration failed for asset " + 
                                        std::to_string(asset_returns.first));
                overall_success = false;
            }
        } catch (const std::exception& e) {
            event_logger_.log_error("Exception during GARCH calibration: " + std::string(e.what()));
            overall_success = false;
        }
    }
    
    if (overall_success) {
        event_logger_.log_system_status("Volatility models calibrated successfully");
    }
    
    return overall_success;
}

VolatilityArbitrageStrategy::PerformanceMetrics 
VolatilityArbitrageStrategy::get_performance_metrics() const {
    PerformanceMetrics metrics;
    
    metrics.total_pnl = total_realized_pnl_ + total_unrealized_pnl_;
    metrics.total_trades = trades_executed_.load();
    
    // Calculate other metrics based on daily P&L history
    if (!daily_pnl_history_.empty()) {
        double sum_pnl = std::accumulate(daily_pnl_history_.begin(), daily_pnl_history_.end(), 0.0);
        double avg_daily_pnl = sum_pnl / daily_pnl_history_.size();
        
        double variance = 0.0;
        for (double pnl : daily_pnl_history_) {
            variance += std::pow(pnl - avg_daily_pnl, 2);
        }
        variance /= daily_pnl_history_.size();
        double std_dev = std::sqrt(variance);
        
        metrics.sharpe_ratio = (std_dev > 0) ? (avg_daily_pnl / std_dev) * std::sqrt(252.0) : 0.0;
        
        // Calculate max drawdown
        double peak = 0.0;
        double max_dd = 0.0;
        double running_pnl = 0.0;
        
        for (double pnl : daily_pnl_history_) {
            running_pnl += pnl;
            if (running_pnl > peak) peak = running_pnl;
            double drawdown = peak - running_pnl;
            if (drawdown > max_dd) max_dd = drawdown;
        }
        
        metrics.max_drawdown = max_dd;
    }
    
    // Calculate win rate from positions
    size_t winning_trades = 0;
    double largest_win = 0.0;
    double largest_loss = 0.0;
    
    for (const auto& pos_pair : positions_) {
        const auto& position = pos_pair.second;
        double total_pnl = position.realized_pnl + position.unrealized_pnl;
        
        if (total_pnl > 0) {
            winning_trades++;
            if (total_pnl > largest_win) largest_win = total_pnl;
        } else if (total_pnl < largest_loss) {
            largest_loss = total_pnl;
        }
    }
    
    metrics.winning_trades = winning_trades;
    metrics.win_rate = (metrics.total_trades > 0) ? 
                      static_cast<double>(winning_trades) / metrics.total_trades : 0.0;
    metrics.largest_win = largest_win;
    metrics.largest_loss = largest_loss;
    
    return metrics;
}

bool VolatilityArbitrageStrategy::is_healthy() const {
    // Check if models are calibrated and working
    if (!garch_model_->is_model_valid()) return false;
    if (!vol_forecaster_->is_healthy()) return false;
    
    // Check portfolio risk limits
    if (std::abs(portfolio_metrics_.total_delta) > params_.max_portfolio_delta * 2) return false;
    if (portfolio_metrics_.portfolio_var_1day > 5000.0) return false;
    
    // Check for excessive losses
    if (total_realized_pnl_ + total_unrealized_pnl_ < -20000.0) return false;
    
    return true;
}

void VolatilityArbitrageStrategy::reset_performance_metrics() {
    total_realized_pnl_ = 0.0;
    total_unrealized_pnl_ = 0.0;
    signals_generated_.store(0);
    trades_executed_.store(0);
    hedge_trades_.store(0);
    daily_pnl_history_.clear();
    
    event_logger_.log_system_status("Performance metrics reset");
}

} // namespace Alaris::Strategy
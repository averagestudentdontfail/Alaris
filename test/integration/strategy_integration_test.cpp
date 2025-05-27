// test/integration/strategy_integration_test.cpp
#define ALARIS_ENABLE_TESTING

#include "gtest/gtest.h"
#include "src/quantlib/strategy/vol_arb.h"
#include "src/quantlib/pricing/alo_engine.h"
#include "src/quantlib/core/memory_pool.h"
#include "src/quantlib/core/event_log.h"
#include "src/quantlib/ipc/message_types.h"
#include <vector>
#include <iostream>
#include <memory>
#include <unordered_map>

using namespace Alaris;

class SophisticatedStrategyTest : public ::testing::Test {
protected:
    std::unique_ptr<Core::MemoryPool> mem_pool_;
    std::unique_ptr<Core::PerCycleAllocator> allocator_;
    std::unique_ptr<Core::EventLogger> logger_;
    std::unique_ptr<Pricing::QuantLibALOEngine> pricer_;
    std::unique_ptr<Strategy::VolatilityArbitrageStrategy> strategy_;

    void SetUp() override {
        mem_pool_ = std::make_unique<Core::MemoryPool>(20 * 1024 * 1024);
        allocator_ = std::make_unique<Core::PerCycleAllocator>(*mem_pool_);
        logger_ = std::make_unique<Core::EventLogger>("strategy_test.log", false);
        pricer_ = std::make_unique<Pricing::QuantLibALOEngine>(*mem_pool_);
        strategy_ = std::make_unique<Strategy::VolatilityArbitrageStrategy>(*pricer_, *allocator_, *logger_, *mem_pool_);
    }

    IPC::MarketDataMessage create_market_data(uint32_t symbol_id, double underlying_price,
                                             double bid = 0, double ask = 0,
                                             double bid_iv = 0.2, double ask_iv = 0.2) {
        IPC::MarketDataMessage data;
        data.symbol_id = symbol_id;
        data.underlying_price = underlying_price;
        data.bid = (bid > 0) ? bid : underlying_price * 0.999;
        data.ask = (ask > 0) ? ask : underlying_price * 1.001;
        data.bid_iv = bid_iv;
        data.ask_iv = ask_iv;
        data.bid_size = 100;
        data.ask_size = 100;
        data.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::high_resolution_clock::now().time_since_epoch()).count();
        return data;
    }

    Pricing::OptionData create_option_data(uint32_t symbol_id, double underlying_price,
                                          double strike_price, double time_to_expiry = 0.25,
                                          QuantLib::Option::Type type = QuantLib::Option::Call) {
        Pricing::OptionData option;
        option.symbol_id = symbol_id;
        option.underlying_price = underlying_price;
        option.strike_price = strike_price;
        option.risk_free_rate = 0.05;
        option.dividend_yield = 0.02;
        option.volatility = 0.20;
        option.time_to_expiry = time_to_expiry;
        option.option_type = type;
        return option;
    }

    void feed_market_history(uint32_t underlying_symbol, const std::vector<double>& prices) {
        for (double price : prices) {
            auto market_data = create_market_data(underlying_symbol, price);
            strategy_->on_market_data(market_data);
        }
    }
};

TEST_F(SophisticatedStrategyTest, InitializationAndConfiguration) {
    // Test default initialization
    EXPECT_EQ(strategy_->active_positions_count(), 0);
    EXPECT_TRUE(strategy_->is_healthy());
    
    auto initial_params = strategy_->get_parameters();
    EXPECT_GT(initial_params.vol_difference_threshold, 0.0);
    EXPECT_GT(initial_params.confidence_threshold, 0.0);
    EXPECT_LT(initial_params.kelly_fraction, 0.1);  // Should be conservative
    
    // Test parameter updates
    Strategy::StrategyParameters new_params;
    new_params.vol_difference_threshold = 0.04;
    new_params.confidence_threshold = 0.8;
    new_params.strategy_mode = Strategy::StrategyParameters::Mode::GAMMA_SCALPING;
    new_params.max_portfolio_delta = 0.15;
    
    strategy_->set_parameters(new_params);
    auto updated_params = strategy_->get_parameters();
    EXPECT_DOUBLE_EQ(updated_params.vol_difference_threshold, 0.04);
    EXPECT_DOUBLE_EQ(updated_params.confidence_threshold, 0.8);
    EXPECT_EQ(updated_params.strategy_mode, Strategy::StrategyParameters::Mode::GAMMA_SCALPING);
    
    std::cout << "Strategy initialization and configuration tests passed" << std::endl;
}

TEST_F(SophisticatedStrategyTest, MarketDataProcessingAndRegimeDetection) {
    uint32_t underlying_symbol = 1001;
    
    // Feed historical price data to build regime detection
    std::vector<double> price_history;
    double base_price = 100.0;
    
    // Create low volatility period
    for (int i = 0; i < 30; ++i) {
        double price = base_price + 0.5 * std::sin(i * 0.1);  // Low vol oscillation
        price_history.push_back(price);
    }
    
    // Create high volatility period
    for (int i = 0; i < 30; ++i) {
        double price = base_price + 5.0 * std::sin(i * 0.3) + 2.0 * (i % 2 == 0 ? 1 : -1);
        price_history.push_back(price);
    }
    
    feed_market_history(underlying_symbol, price_history);
    
    // Check that market data was processed
    auto market_data_map = strategy_->get_latest_market_data_for_testing();
    EXPECT_TRUE(market_data_map.find(underlying_symbol) != market_data_map.end());
    
    // Regime should have been updated
    auto regime = strategy_->get_current_regime();
    EXPECT_GT(regime.current_realized_vol, 0.0);
    EXPECT_GT(regime.regime_confidence, 0.0);
    
    std::cout << "Current regime - Vol: " << regime.current_realized_vol 
              << ", Confidence: " << regime.regime_confidence << std::endl;
}

TEST_F(SophisticatedStrategyTest, DeltaNeutralSignalGeneration) {
    uint32_t underlying_symbol = 2001;
    uint32_t call_symbol = 2101;
    uint32_t put_symbol = 2201;
    
    // Set strategy to delta neutral mode
    strategy_->set_strategy_mode(Strategy::StrategyParameters::Mode::DELTA_NEUTRAL);
    
    // Feed some market history
    std::vector<double> prices = {98.0, 99.0, 100.0, 101.0, 102.0, 100.5, 99.8, 100.2};
    feed_market_history(underlying_symbol, prices);
    
    // Create option chain
    std::vector<Pricing::OptionData> option_chain;
    option_chain.push_back(create_option_data(call_symbol, 100.0, 100.0, 0.25, QuantLib::Option::Call));
    option_chain.push_back(create_option_data(put_symbol, 100.0, 100.0, 0.25, QuantLib::Option::Put));
    
    // Create market data with vol mismatch
    std::vector<IPC::MarketDataMessage> option_market_data;
    option_market_data.push_back(create_market_data(call_symbol, 0, 4.0, 4.2, 0.15, 0.15));  // Low IV
    option_market_data.push_back(create_market_data(put_symbol, 0, 3.8, 4.0, 0.25, 0.25));   // High IV
    
    // Generate signals
    std::vector<IPC::TradingSignalMessage> signals;
    strategy_->scan_and_generate_signals(underlying_symbol, option_chain, option_market_data, signals);
    
    // Should generate signals for options with significant vol differences
    EXPECT_GT(signals.size(), 0);
    
    for (const auto& signal : signals) {
        EXPECT_GT(signal.confidence, 0.0);
        EXPECT_TRUE(signal.symbol_id == call_symbol || signal.symbol_id == put_symbol);
        EXPECT_NE(signal.quantity, 0);
        
        std::cout << "Signal - Symbol: " << signal.symbol_id 
                  << ", Qty: " << signal.quantity 
                  << ", Confidence: " << signal.confidence 
                  << ", Forecast Vol: " << signal.forecast_volatility
                  << ", Implied Vol: " << signal.implied_volatility << std::endl;
    }
}

TEST_F(SophisticatedStrategyTest, GammaScalpingStrategy) {
    uint32_t underlying_symbol = 3001;
    uint32_t atm_call_symbol = 3101;
    
    // Set strategy to gamma scalping mode
    strategy_->set_strategy_mode(Strategy::StrategyParameters::Mode::GAMMA_SCALPING);
    
    // Feed volatile price history to trigger gamma scalping
    std::vector<double> volatile_prices;
    double base = 100.0;
    for (int i = 0; i < 20; ++i) {
        double vol_move = 3.0 * (i % 2 == 0 ? 1 : -1) * (1 + 0.1 * i);
        volatile_prices.push_back(base + vol_move);
    }
    feed_market_history(underlying_symbol, volatile_prices);
    
    // Create ATM option for gamma scalping
    std::vector<Pricing::OptionData> option_chain;
    option_chain.push_back(create_option_data(atm_call_symbol, 100.0, 100.0, 0.08, QuantLib::Option::Call)); // 4 weeks
    
    std::vector<IPC::MarketDataMessage> option_market_data;
    option_market_data.push_back(create_market_data(atm_call_symbol, 0, 2.8, 3.2, 0.25, 0.25));
    
    std::vector<IPC::TradingSignalMessage> signals;
    strategy_->scan_and_generate_signals(underlying_symbol, option_chain, option_market_data, signals);
    
    // Should generate long gamma position due to high realized volatility
    bool found_gamma_signal = false;
    for (const auto& signal : signals) {
        if (signal.side == 0 && signal.quantity > 0) {  // Long position
            found_gamma_signal = true;
            std::cout << "Gamma scalping signal - Qty: " << signal.quantity 
                      << ", Confidence: " << signal.confidence << std::endl;
        }
    }
    
    EXPECT_TRUE(found_gamma_signal);
}

TEST_F(SophisticatedStrategyTest, PositionManagementAndRiskControl) {
    uint32_t symbol_id = 4001;
    
    // Create an entry signal
    IPC::TradingSignalMessage entry_signal;
    entry_signal.symbol_id = symbol_id;
    entry_signal.quantity = 5;
    entry_signal.side = 0;  // Buy
    entry_signal.signal_type = 0;  // Entry
    entry_signal.market_price = 3.50;
    entry_signal.implied_volatility = 0.22;
    entry_signal.forecast_volatility = 0.28;
    entry_signal.confidence = 0.85;
    entry_signal.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::high_resolution_clock::now().time_since_epoch()).count();
    
    // Simulate fill
    strategy_->on_fill(entry_signal, 3.50, 5, entry_signal.timestamp);
    
    EXPECT_EQ(strategy_->active_positions_count(), 1);
    
    auto position = strategy_->get_position_for_testing(symbol_id);
    ASSERT_NE(position, nullptr);
    EXPECT_DOUBLE_EQ(position->quantity, 5.0);
    EXPECT_DOUBLE_EQ(position->entry_price, 3.50);
    EXPECT_DOUBLE_EQ(position->vol_forecast_at_entry, 0.28);
    EXPECT_DOUBLE_EQ(position->confidence_at_entry, 0.85);
    
    // Test position updates via market data
    auto market_data = create_market_data(symbol_id, 0, 3.8, 3.9);
    strategy_->on_market_data(market_data);
    
    position = strategy_->get_position_for_testing(symbol_id);
    EXPECT_NEAR(position->current_price, 3.85, 0.01);  // Mid price
    EXPECT_GT(position->unrealized_pnl, 0.0);  // Should be profitable
    
    std::cout << "Position - Entry: " << position->entry_price 
              << ", Current: " << position->current_price 
              << ", PnL: " << position->unrealized_pnl << std::endl;
}

TEST_F(SophisticatedStrategyTest, StopLossAndProfitTargets) {
    uint32_t symbol_id = 5001;
    
    // Set aggressive stop loss for testing
    Strategy::StrategyParameters params = strategy_->get_parameters();
    params.stop_loss_percent = 0.10;      // 10% stop loss
    params.profit_target_percent = 0.20;  // 20% profit target
    strategy_->set_parameters(params);
    
    // Create position
    IPC::TradingSignalMessage entry_signal;
    entry_signal.symbol_id = symbol_id;
    entry_signal.quantity = 10;
    entry_signal.side = 0;
    entry_signal.signal_type = 0;
    entry_signal.market_price = 2.00;
    
    strategy_->on_fill(entry_signal, 2.00, 10, 0);
    EXPECT_EQ(strategy_->active_positions_count(), 1);
    
    // Test stop loss trigger
    auto losing_market_data = create_market_data(symbol_id, 0, 1.70, 1.75);  // ~15% loss
    strategy_->on_market_data(losing_market_data);
    
    std::vector<IPC::TradingSignalMessage> stop_signals;
    strategy_->apply_stop_losses(stop_signals);
    
    // Should generate stop loss signal
    EXPECT_GT(stop_signals.size(), 0);
    
    bool found_stop_loss = false;
    for (const auto& signal : stop_signals) {
        if (signal.symbol_id == symbol_id && signal.signal_type == 1) {  // Exit signal
            found_stop_loss = true;
            EXPECT_EQ(signal.quantity, -10);  // Close position
            EXPECT_EQ(signal.urgency, 255);   // Max urgency
            std::cout << "Stop loss triggered at price: " << signal.market_price << std::endl;
        }
    }
    EXPECT_TRUE(found_stop_loss);
    
    // Reset for profit target test
    strategy_->on_fill(entry_signal, 2.00, 10, 0);  // Re-enter position
    
    auto winning_market_data = create_market_data(symbol_id, 0, 2.45, 2.50);  // ~25% gain
    strategy_->on_market_data(winning_market_data);
    
    std::vector<IPC::TradingSignalMessage> profit_signals;
    strategy_->apply_profit_targets(profit_signals);
    
    EXPECT_GT(profit_signals.size(), 0);
    std::cout << "Profit target test completed" << std::endl;
}

TEST_F(SophisticatedStrategyTest, PortfolioRiskManagement) {
    // Create multiple positions to test portfolio risk
    std::vector<uint32_t> symbols = {6001, 6002, 6003};
    
    for (size_t i = 0; i < symbols.size(); ++i) {
        IPC::TradingSignalMessage signal;
        signal.symbol_id = symbols[i];
        signal.quantity = 5 + i;  // Different position sizes
        signal.side = (i % 2 == 0) ? 0 : 1;  // Mix of long/short
        signal.signal_type = 0;
        signal.market_price = 3.0 + i * 0.5;
        
        strategy_->on_fill(signal, signal.market_price, signal.quantity, 0);
    }
    
    EXPECT_EQ(strategy_->active_positions_count(), symbols.size());
    
    // Test portfolio metrics calculation
    auto portfolio_metrics = strategy_->get_portfolio_metrics();
    
    EXPECT_GT(portfolio_metrics.total_notional, 0.0);
    EXPECT_EQ(portfolio_metrics.active_positions, symbols.size());
    EXPECT_TRUE(std::isfinite(portfolio_metrics.total_delta));
    EXPECT_TRUE(std::isfinite(portfolio_metrics.total_gamma));
    EXPECT_TRUE(std::isfinite(portfolio_metrics.total_vega));
    
    std::cout << "Portfolio metrics - Notional: " << portfolio_metrics.total_notional 
              << ", Delta: " << portfolio_metrics.total_delta 
              << ", Gamma: " << portfolio_metrics.total_gamma 
              << ", Vega: " << portfolio_metrics.total_vega << std::endl;
    
    // Test portfolio health
    EXPECT_TRUE(strategy_->is_healthy());
}

TEST_F(SophisticatedStrategyTest, VolatilityModelCalibration) {
    // Create return history for multiple assets
    std::unordered_map<uint32_t, std::vector<double>> returns_by_asset;
    
    std::random_device rd;
    std::mt19937 gen(42);
    std::normal_distribution<> normal(0.0, 0.015);
    
    for (uint32_t asset_id : {7001, 7002, 7003}) {
        std::vector<double> returns;
        for (int i = 0; i < 100; ++i) {
            returns.push_back(normal(gen));
        }
        returns_by_asset[asset_id] = returns;
    }
    
    // Test model calibration
    bool calibration_success = strategy_->calibrate_volatility_models(returns_by_asset);
    EXPECT_TRUE(calibration_success);
    
    std::cout << "Volatility model calibration completed successfully" << std::endl;
}

TEST_F(SophisticatedStrategyTest, PerformanceMetricsAndReporting) {
    // Create some trading activity
    uint32_t symbol = 8001;
    
    // Enter position
    IPC::TradingSignalMessage entry;
    entry.symbol_id = symbol;
    entry.quantity = 10;
    entry.side = 0;
    entry.signal_type = 0;
    entry.market_price = 4.00;
    
    strategy_->on_fill(entry, 4.00, 10, 0);
    
    // Exit position with profit
    IPC::TradingSignalMessage exit;
    exit.symbol_id = symbol;
    exit.quantity = -10;
    exit.side = 1;
    exit.signal_type = 1;
    exit.market_price = 4.50;
    
    strategy_->on_fill(exit, 4.50, -10, 1000000);
    
    // Check performance metrics
    auto metrics = strategy_->get_performance_metrics();
    
    EXPECT_GT(metrics.total_pnl, 0.0);  // Should be profitable
    EXPECT_EQ(metrics.total_trades, 1);
    EXPECT_EQ(metrics.winning_trades, 1);
    EXPECT_DOUBLE_EQ(metrics.win_rate, 1.0);
    EXPECT_GT(metrics.largest_win, 0.0);
    
    std::cout << "Performance metrics - PnL: " << metrics.total_pnl 
              << ", Win rate: " << metrics.win_rate 
              << ", Trades: " << metrics.total_trades << std::endl;
    
    // Test metrics reset
    strategy_->reset_performance_metrics();
    auto reset_metrics = strategy_->get_performance_metrics();
    EXPECT_DOUBLE_EQ(reset_metrics.total_pnl, 0.0);
    EXPECT_EQ(reset_metrics.total_trades, 0);
}

TEST_F(SophisticatedStrategyTest, VolatilitySurfaceAnalysis) {
    uint32_t underlying_symbol = 9001;
    
    // Feed market history
    feed_market_history(underlying_symbol, {95.0, 98.0, 100.0, 102.0, 105.0});
    
    // Create diverse option chain
    std::vector<Pricing::OptionData> option_chain;
    std::vector<IPC::MarketDataMessage> market_data;
    
    std::vector<double> strikes = {90, 95, 100, 105, 110};
    std::vector<double> expiries = {0.08, 0.25, 0.5};  // 1 month, 3 months, 6 months
    
    uint32_t symbol_counter = 9100;
    for (double strike : strikes) {
        for (double expiry : expiries) {
            for (auto type : {QuantLib::Option::Call, QuantLib::Option::Put}) {
                option_chain.push_back(create_option_data(symbol_counter, 100.0, strike, expiry, type));
                
                // Create market data with varying IVs
                double base_iv = 0.20;
                double moneyness_adj = std::abs(strike - 100.0) * 0.002;  // Skew
                double term_adj = expiry * 0.05;  // Term structure
                double iv = base_iv + moneyness_adj + term_adj;
                
                market_data.push_back(create_market_data(symbol_counter, 0, 2.0, 2.2, iv, iv));
                symbol_counter++;
            }
        }
    }
    
    // Analyze volatility surface
    std::vector<IPC::TradingSignalMessage> signals;
    strategy_->scan_and_generate_signals(underlying_symbol, option_chain, market_data, signals);
    
    // Check that surface analysis was performed
    auto surface_analysis = strategy_->get_volatility_surface_analysis();
    EXPECT_GT(surface_analysis.size(), 0);
    
    // Verify analysis contains expected data
    for (const auto& point : surface_analysis) {
        EXPECT_GT(point.time_to_expiry, 0.0);
        EXPECT_GT(point.implied_vol, 0.0);
        EXPECT_GT(point.model_vol, 0.0);
        EXPECT_GE(point.arbitrage_score, 0.0);
    }
    
    std::cout << "Volatility surface analysis completed - " << surface_analysis.size() 
              << " points analyzed" << std::endl;
}

TEST_F(SophisticatedStrategyTest, EmergencyLiquidationAndRiskLimits) {
    // Create positions that exceed risk limits
    std::vector<uint32_t> symbols = {10001, 10002, 10003, 10004, 10005};
    
    for (size_t i = 0; i < symbols.size(); ++i) {
        IPC::TradingSignalMessage signal;
        signal.symbol_id = symbols[i];
        signal.quantity = 20;  // Large positions
        signal.side = 0;
        signal.signal_type = 0;
        signal.market_price = 5.0;
        
        strategy_->on_fill(signal, 5.0, 20, 0);
    }
    
    EXPECT_EQ(strategy_->active_positions_count(), symbols.size());
    
    // Test emergency liquidation
    std::vector<IPC::TradingSignalMessage> liquidation_signals;
    strategy_->emergency_liquidation(liquidation_signals);
    
    // Should generate exit signals for all positions
    EXPECT_EQ(liquidation_signals.size(), symbols.size());
    
    for (const auto& signal : liquidation_signals) {
        EXPECT_EQ(signal.signal_type, 1);  // Exit signal
        EXPECT_EQ(signal.urgency, 255);    // Maximum urgency
        EXPECT_EQ(signal.quantity, -20);   // Close full position
    }
    
    std::cout << "Emergency liquidation test - Generated " << liquidation_signals.size() 
              << " exit signals" << std::endl;
}

TEST_F(SophisticatedStrategyTest, StrategyModeTransitions) {
    uint32_t underlying_symbol = 11001;
    uint32_t option_symbol = 11101;
    
    // Feed market data
    feed_market_history(underlying_symbol, {98.0, 100.0, 102.0, 99.0, 101.0});
    
    std::vector<Pricing::OptionData> option_chain;
    option_chain.push_back(create_option_data(option_symbol, 100.0, 100.0));
    
    std::vector<IPC::MarketDataMessage> market_data;
    market_data.push_back(create_market_data(option_symbol, 0, 3.8, 4.2, 0.18, 0.22));
    
    // Test different strategy modes
    std::vector<Strategy::StrategyParameters::Mode> modes = {
        Strategy::StrategyParameters::Mode::DELTA_NEUTRAL,
        Strategy::StrategyParameters::Mode::GAMMA_SCALPING,
        Strategy::StrategyParameters::Mode::VOLATILITY_TIMING
    };
    
    for (auto mode : modes) {
        strategy_->set_strategy_mode(mode);
        
        std::vector<IPC::TradingSignalMessage> signals;
        strategy_->scan_and_generate_signals(underlying_symbol, option_chain, market_data, signals);
        
        // Each mode should potentially generate different signals
        std::cout << "Mode " << static_cast<int>(mode) << " generated " 
                  << signals.size() << " signals" << std::endl;
        
        // Verify signals are properly formed
        for (const auto& signal : signals) {
            EXPECT_NE(signal.quantity, 0);
            EXPECT_GT(signal.confidence, 0.0);
            EXPECT_TRUE(std::isfinite(signal.market_price));
        }
    }
}

// Test main runner integration
TEST_F(SophisticatedStrategyTest, FullWorkflowIntegration) {
    uint32_t underlying_symbol = 12001;
    std::vector<uint32_t> option_symbols = {12101, 12102, 12103};
    
    // Configure strategy for comprehensive test
    Strategy::StrategyParameters params;
    params.vol_difference_threshold = 0.02;  // Lower threshold for more signals
    params.confidence_threshold = 0.6;       // Lower confidence for more signals
    params.strategy_mode = Strategy::StrategyParameters::Mode::DELTA_NEUTRAL;
    params.auto_hedge_enabled = true;
    strategy_->set_parameters(params);
    
    // Step 1: Build market history
    std::vector<double> price_history;
    for (int i = 0; i < 50; ++i) {
        double price = 100.0 + 5.0 * std::sin(i * 0.1) + (i % 5) * 0.5;
        price_history.push_back(price);
    }
    feed_market_history(underlying_symbol, price_history);
    
    // Step 2: Calibrate models
    std::unordered_map<uint32_t, std::vector<double>> returns_data;
    std::vector<double> returns;
    for (size_t i = 1; i < price_history.size(); ++i) {
        returns.push_back(std::log(price_history[i] / price_history[i-1]));
    }
    returns_data[underlying_symbol] = returns;
    
    bool calibration_success = strategy_->calibrate_volatility_models(returns_data);
    EXPECT_TRUE(calibration_success);
    
    // Step 3: Create option chain with vol mismatches
    std::vector<Pricing::OptionData> option_chain;
    std::vector<IPC::MarketDataMessage> option_market_data;
    
    for (size_t i = 0; i < option_symbols.size(); ++i) {
        option_chain.push_back(create_option_data(option_symbols[i], 100.0, 95.0 + i * 5.0));
        
        double iv = 0.15 + i * 0.05;  // Varying implied vols
        option_market_data.push_back(create_market_data(option_symbols[i], 0, 2.0 + i, 2.2 + i, iv, iv));
    }
    
    // Step 4: Generate signals
    std::vector<IPC::TradingSignalMessage> signals;
    strategy_->scan_and_generate_signals(underlying_symbol, option_chain, option_market_data, signals);
    
    std::cout << "Full workflow - Generated " << signals.size() << " signals" << std::endl;
    
    // Step 5: Execute trades and manage positions
    for (const auto& signal : signals) {
        strategy_->on_fill(signal, signal.market_price, signal.quantity, 
                          std::chrono::duration_cast<std::chrono::nanoseconds>(
                              std::chrono::high_resolution_clock::now().time_since_epoch()).count());
    }
    
    size_t positions_count = strategy_->active_positions_count();
    EXPECT_GT(positions_count, 0);
    
    // Step 6: Risk management
    std::vector<IPC::TradingSignalMessage> risk_signals;
    strategy_->apply_stop_losses(risk_signals);
    strategy_->apply_profit_targets(risk_signals);
    
    // Step 7: Performance analysis
    auto final_metrics = strategy_->get_performance_metrics();
    auto portfolio_metrics = strategy_->get_portfolio_metrics();
    
    EXPECT_GE(final_metrics.total_trades, 0);
    EXPECT_TRUE(std::isfinite(final_metrics.total_pnl));
    EXPECT_TRUE(std::isfinite(portfolio_metrics.total_delta));
    
    // Step 8: Health check
    EXPECT_TRUE(strategy_->is_healthy());
    
    std::cout << "Full workflow integration test completed successfully" << std::endl;
    std::cout << "Final positions: " << positions_count << std::endl;
    std::cout << "Total PnL: " << final_metrics.total_pnl << std::endl;
    std::cout << "Portfolio delta: " << portfolio_metrics.total_delta << std::endl;
}
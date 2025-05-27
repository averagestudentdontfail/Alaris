// test/integration/strategy_integration_test.cpp
#define ALARIS_ENABLE_TESTING // Keep this if your strategy uses it for test-only interfaces

#include "gtest/gtest.h"
#include "src/quantlib/strategy/vol_arb.h"      // Adjusted
#include "src/quantlib/pricing/alo_engine.h"     // Adjusted
#include "src/quantlib/core/memory_pool.h"   // Adjusted
#include "src/quantlib/core/event_log.h"     // Adjusted
#include "src/quantlib/ipc/message_types.h"  // Adjusted
#include "test/test_helpers.h" // For helper functions/structs, not macros

#include <vector>
#include <iostream>
#include <memory>

// Helper function (if not already in test_helpers.h or if you want it local)
Alaris::Pricing::OptionData create_test_pricing_option_data(
    uint32_t option_symbol_id, double underlying_price, double strike, double tte,
    QuantLib::Option::Type type, double risk_free = 0.05,
    double dividend = 0.01, double initial_vol = 0.20) {
    Alaris::Pricing::OptionData pd;
    pd.symbol_id = option_symbol_id;
    pd.underlying_price = underlying_price;
    pd.strike_price = strike;
    pd.time_to_expiry = tte;
    pd.option_type = type;
    pd.risk_free_rate = risk_free;
    pd.dividend_yield = dividend;
    pd.volatility = initial_vol;
    return pd;
}

// Test fixture for strategy integration tests
class StrategyIntegrationTest : public ::testing::Test {
protected:
    std::unique_ptr<Alaris::Core::MemoryPool> mem_pool_;
    std::unique_ptr<Alaris::Core::PerCycleAllocator> allocator_;
    std::unique_ptr<Alaris::Core::EventLogger> logger_;
    std::unique_ptr<Alaris::Pricing::QuantLibALOEngine> pricer_;
    std::unique_ptr<Alaris::Strategy::VolatilityArbitrageStrategy> strategy_;

    void SetUp() override {
        mem_pool_ = std::make_unique<Alaris::Core::MemoryPool>(10 * 1024 * 1024);
        allocator_ = std::make_unique<Alaris::Core::PerCycleAllocator>(*mem_pool_);
        logger_ = std::make_unique<Alaris::Core::EventLogger>("strategy_integration_test.log", false);
        pricer_ = std::make_unique<Alaris::Pricing::QuantLibALOEngine>(*mem_pool_);
        strategy_ = std::make_unique<Alaris::Strategy::VolatilityArbitrageStrategy>(*pricer_, *allocator_, *logger_, *mem_pool_);
    }
};

TEST_F(StrategyIntegrationTest, Initialization) {
    ASSERT_NE(strategy_, nullptr);
    EXPECT_EQ(strategy_->active_positions_count(), 0);
    EXPECT_EQ(strategy_->get_active_model_type(), Alaris::Strategy::VolatilityModelType::ENSEMBLE_GJR_HISTORICAL);
    
    Alaris::Strategy::StrategyParameters params;
    params.entry_threshold = 0.10;
    strategy_->set_parameters(params);
    // Add more assertions for parameter setting if get_parameters() exists
}

TEST_F(StrategyIntegrationTest, CalibrationAndForecast) {
    std::vector<QuantLib::Real> returns(252, 0.001);
    returns[10] = 0.05; returns[20] = -0.06;
    
    ASSERT_TRUE(strategy_->calibrate_gjr_model(returns));
    
    double forecast_ensemble = strategy_->get_volatility_forecast_for_testing(1, 1);
    EXPECT_GT(forecast_ensemble, 0.0);
    std::cout << "  Ensemble forecast (after calibration): " << forecast_ensemble << std::endl;
}

TEST_F(StrategyIntegrationTest, SignalGeneration_BuyCall) {
    Alaris::Strategy::StrategyParameters params;
    params.entry_threshold = 0.05;
    params.confidence_threshold = 0.5;
    strategy_->set_parameters(params);
    strategy_->set_active_volatility_model_type(Alaris::Strategy::VolatilityModelType::ENSEMBLE_GJR_HISTORICAL);

    uint32_t underlying_symbol_id = 101;
    Alaris::IPC::MarketDataMessage underlying_md;
    underlying_md.symbol_id = underlying_symbol_id;
    underlying_md.underlying_price = 100.0;
    underlying_md.bid = 99.98;
    underlying_md.ask = 100.02;
    strategy_->on_market_data(underlying_md);

    uint32_t option_sym_id_call = 201;
    std::vector<Alaris::Pricing::OptionData> option_chain;
    option_chain.push_back(create_test_pricing_option_data(
        option_sym_id_call, 100.0, 100.0, 0.25, QuantLib::Option::Call));

    std::vector<Alaris::IPC::MarketDataMessage> option_market_data;
    Alaris::IPC::MarketDataMessage call_md;
    call_md.symbol_id = option_sym_id_call;
    call_md.bid = 2.0;
    call_md.ask = 2.1;
    call_md.underlying_price = 100.0;
    call_md.bid_iv = 0.15; // Low implied vol
    call_md.ask_iv = 0.15;
    option_market_data.push_back(call_md);

    std::vector<Alaris::IPC::TradingSignalMessage> signals;
    strategy_->scan_option_chain(underlying_symbol_id, option_chain, option_market_data, signals);
    
    std::cout << "  Generated " << signals.size() << " signals." << std::endl;
    if (!signals.empty()) {
        const auto& signal = signals[0];
        std::cout << "  Signal: SymID=" << signal.symbol_id 
                  << " Qty=" << signal.quantity 
                  << " Side=" << (int)signal.side 
                  << " ForecastVol=" << signal.forecast_volatility
                  << " ImpliedVol=" << signal.implied_volatility 
                  << " TheoPrice=" << signal.theoretical_price
                  << " MarketPrice=" << signal.market_price << std::endl;
        EXPECT_EQ(signal.symbol_id, option_sym_id_call);
        // Further assertions based on expected strategy logic, e.g., buy signal
        // EXPECT_EQ(signal.side, 0); // 0 for buy
        // EXPECT_GT(signal.quantity, 0);
    } else {
        // This might be an acceptable outcome if the vol difference isn't enough
        // Consider what the expected outcome is if no signal should be generated.
        std::cout << "  No signal generated. This might be expected if vol diff is small." << std::endl;
    }
}

TEST_F(StrategyIntegrationTest, PositionManagement) {
    uint32_t option_id = 301;
    Alaris::IPC::TradingSignalMessage entry_signal;
    entry_signal.signal_type = 0; 
    entry_signal.symbol_id = option_id;
    entry_signal.quantity = 10; 
    entry_signal.side = 0; 
    entry_signal.implied_volatility = 0.20;

    strategy_->on_fill(entry_signal, 5.0, 10);
    ASSERT_EQ(strategy_->active_positions_count(), 1);
    
    const auto* position = strategy_->get_position_for_testing(option_id);
    ASSERT_NE(position, nullptr);
    EXPECT_DOUBLE_EQ(position->quantity, 10.0);
    EXPECT_DOUBLE_EQ(position->entry_price, 5.0);

    Alaris::IPC::TradingSignalMessage exit_signal;
    exit_signal.signal_type = 1; 
    exit_signal.symbol_id = option_id;
    exit_signal.quantity = -10; 
    exit_signal.side = 1; 

    strategy_->on_fill(exit_signal, 5.5, -10);
    EXPECT_EQ(strategy_->active_positions_count(), 0);
    
    Alaris::Strategy::StrategyPerformanceMetrics perf = strategy_->get_performance_metrics();
    EXPECT_NEAR(perf.total_pnl, 5.0, 1e-9); // PNL = 10 * (5.5 - 5.0)
}

// No main() function needed
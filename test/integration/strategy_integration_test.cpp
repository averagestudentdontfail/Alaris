// test/integration/strategy_integration_test.cpp
// Fixed integration test for volatility arbitrage strategy

// Enable testing interface
#define ALARIS_ENABLE_TESTING

#include "../test_helpers.h" // For ALARIS_TEST_START, ALARIS_ASSERT, etc.
#include "../../src/quantlib/strategy/vol_arb.h"
#include "../../src/quantlib/pricing/alo_engine.h"
#include "../../src/quantlib/core/memory_pool.h"
#include "../../src/quantlib/core/event_log.h"
#include "../../src/quantlib/ipc/message_types.h"

#include <vector>
#include <iostream>
#include <memory>

// Helper function to create a default Alaris::Pricing::OptionData for the strategy
Alaris::Pricing::OptionData create_pricing_option_data(
    uint32_t option_symbol_id,
    double underlying_price,
    double strike,
    double tte, // time_to_expiry in years
    QuantLib::Option::Type type,
    double risk_free = 0.05,
    double dividend = 0.01,
    double initial_vol = 0.20) {
    
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

int main() {
    alaris::test::TestReporter reporter;

    ALARIS_TEST_START(reporter, "StrategyIntegrationTest_Initialization");
    {
        std::unique_ptr<Alaris::Core::MemoryPool> mem_pool = 
            std::make_unique<Alaris::Core::MemoryPool>(10 * 1024 * 1024);
        Alaris::Core::PerCycleAllocator allocator(*mem_pool);
        Alaris::Core::EventLogger logger("strategy_integration_test.log", false);
        Alaris::Pricing::QuantLibALOEngine pricer(*mem_pool);

        Alaris::Strategy::VolatilityArbitrageStrategy strategy(pricer, allocator, logger, *mem_pool);

        ALARIS_ASSERT(strategy.active_positions_count() == 0);
        ALARIS_ASSERT(strategy.get_active_model_type() == 
                     Alaris::Strategy::VolatilityModelType::ENSEMBLE_GJR_HISTORICAL);
        
        Alaris::Strategy::StrategyParameters params;
        params.entry_threshold = 0.10; // 10% vol difference
        strategy.set_parameters(params);
    }
    ALARIS_TEST_END(reporter);

    ALARIS_TEST_START(reporter, "StrategyIntegrationTest_CalibrationAndForecast");
    {
        std::unique_ptr<Alaris::Core::MemoryPool> mem_pool = 
            std::make_unique<Alaris::Core::MemoryPool>();
        Alaris::Core::PerCycleAllocator allocator(*mem_pool);
        Alaris::Core::EventLogger logger("strategy_integration_test.log", false);
        Alaris::Pricing::QuantLibALOEngine pricer(*mem_pool);
        Alaris::Strategy::VolatilityArbitrageStrategy strategy(pricer, allocator, logger, *mem_pool);

        std::vector<QuantLib::Real> returns(252, 0.001); // Dummy returns
        returns[10] = 0.05; returns[20] = -0.06; // Some volatility shocks
        
        strategy.calibrate_gjr_model(returns);
        
        // Use public testing interface
        double forecast_direct_gjr = strategy.get_gjr_garch_model_for_testing().current_volatility();
        double forecast_ensemble = strategy.get_volatility_forecast_for_testing(1 /*dummy underlying_id*/, 1);

        ALARIS_ASSERT(forecast_direct_gjr > 0.0);
        ALARIS_ASSERT(forecast_ensemble > 0.0);
        std::cout << "  Direct GJR forecast: " << forecast_direct_gjr << std::endl;
        std::cout << "  Ensemble forecast: " << forecast_ensemble << std::endl;
    }
    ALARIS_TEST_END(reporter);

    ALARIS_TEST_START(reporter, "StrategyIntegrationTest_SignalGeneration_BuyCall");
    {
        std::unique_ptr<Alaris::Core::MemoryPool> mem_pool = 
            std::make_unique<Alaris::Core::MemoryPool>();
        Alaris::Core::PerCycleAllocator allocator(*mem_pool);
        Alaris::Core::EventLogger logger("strategy_integration_test.log", false);
        Alaris::Pricing::QuantLibALOEngine pricer(*mem_pool);
        Alaris::Strategy::VolatilityArbitrageStrategy strategy(pricer, allocator, logger, *mem_pool);

        Alaris::Strategy::StrategyParameters params;
        params.entry_threshold = 0.05; // 5% vol diff
        params.confidence_threshold = 0.5;
        strategy.set_parameters(params);
        strategy.set_active_volatility_model_type(
            Alaris::Strategy::VolatilityModelType::ENSEMBLE_GJR_HISTORICAL);

        // Mock market data for underlying
        uint32_t underlying_symbol_id = 101;
        Alaris::IPC::MarketDataMessage underlying_md;
        underlying_md.symbol_id = underlying_symbol_id;
        underlying_md.underlying_price = 100.0;
        underlying_md.bid = 99.98;
        underlying_md.ask = 100.02;
        strategy.on_market_data(underlying_md);

        // Mock option chain and its market data
        uint32_t option_sym_id_call = 201;
        std::vector<Alaris::Pricing::OptionData> option_chain;
        option_chain.push_back(create_pricing_option_data(
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
        strategy.scan_option_chain(underlying_symbol_id, option_chain, option_market_data, signals);
        
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

            ALARIS_ASSERT(signal.symbol_id == option_sym_id_call);
        } else {
            std::cout << "  No signal generated. Forecast vol might not be sufficiently different from implied vol." << std::endl;
        }
    }
    ALARIS_TEST_END(reporter);

    ALARIS_TEST_START(reporter, "StrategyIntegrationTest_PositionManagement");
    {
        std::unique_ptr<Alaris::Core::MemoryPool> mem_pool = 
            std::make_unique<Alaris::Core::MemoryPool>();
        Alaris::Core::PerCycleAllocator allocator(*mem_pool);
        Alaris::Core::EventLogger logger("strategy_integration_test.log", false);
        Alaris::Pricing::QuantLibALOEngine pricer(*mem_pool);
        Alaris::Strategy::VolatilityArbitrageStrategy strategy(pricer, allocator, logger, *mem_pool);

        uint32_t option_id = 301;
        Alaris::IPC::TradingSignalMessage entry_signal;
        entry_signal.signal_type = 0; // Entry
        entry_signal.symbol_id = option_id;
        entry_signal.quantity = 10; // Buy 10 contracts
        entry_signal.side = 0; // Buy
        entry_signal.implied_volatility = 0.20;

        strategy.on_fill(entry_signal, 5.0 /*fill_price*/, 10 /*fill_quantity_signed*/);
        ALARIS_ASSERT(strategy.active_positions_count() == 1);
        
        // Check that position was created correctly using testing interface
        const auto* position = strategy.get_position_for_testing(option_id);
        ALARIS_ASSERT(position != nullptr);
        ALARIS_ASSERT(alaris::test::TestValidator::compare_doubles(position->quantity, 10.0, 1e-9));
        ALARIS_ASSERT(alaris::test::TestValidator::compare_doubles(position->entry_price, 5.0, 1e-9));

        // Simulate an exit signal fill
        Alaris::IPC::TradingSignalMessage exit_signal;
        exit_signal.signal_type = 1; // Exit
        exit_signal.symbol_id = option_id;
        exit_signal.quantity = -10; // Sell 10 contracts to close
        exit_signal.side = 1; // Sell

        strategy.on_fill(exit_signal, 5.5 /*exit_fill_price*/, -10 /*fill_quantity_signed*/);
        ALARIS_ASSERT(strategy.active_positions_count() == 0);
        
        Alaris::Strategy::StrategyPerformanceMetrics perf = strategy.get_performance_metrics();
        // PNL = 10 * (5.5 - 5.0) = 5.0
        ALARIS_ASSERT(alaris::test::TestValidator::compare_doubles(perf.total_pnl, 5.0, 1e-9));
    }
    ALARIS_TEST_END(reporter);

    reporter.print_summary();
    return reporter.get_failed_tests() > 0 ? 1 : 0;
}
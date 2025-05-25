// test/quantlib/pricing_test.cpp
#include "../../src/quantlib/pricing/alo_engine.h" // For Alaris::Pricing::QuantLibALOEngine and Alaris::Pricing::OptionData
#include "../../src/quantlib/core/memory_pool.h"    // For Alaris::Core::MemoryPool
#include <gtest/gtest.h>
#include <vector>
#include <chrono>   // For std::chrono
#include <cmath>    // For std::max, std::abs

// Using namespace Alaris for brevity in test definitions.
using namespace Alaris;

class ALOEngineTest : public ::testing::Test {
protected:
    std::unique_ptr<Core::MemoryPool> mem_pool_;
    std::unique_ptr<Pricing::QuantLibALOEngine> engine_;

    void SetUp() override {
        // Initialize memory pool for the engine
        mem_pool_ = std::make_unique<Core::MemoryPool>(64 * 1024 * 1024); // 64MB
        engine_ = std::make_unique<Pricing::QuantLibALOEngine>(*mem_pool_);
        
        // Set a consistent evaluation date for QuantLib within tests if not globally set by engine
        // QuantLib::Settings::instance().evaluationDate() = QuantLib::Date(1, QuantLib::January, 2024);
        // The engine constructor already sets an evaluation date.
    }

    void TearDown() override {
        // unique_ptr will handle cleanup
    }

    // Helper to create a default OptionData instance, matching Alaris::Pricing::OptionData
    Pricing::OptionData create_default_option_data() {
        Pricing::OptionData option;
        option.underlying_price = 100.0;
        option.strike_price = 100.0;
        option.risk_free_rate = 0.05;
        option.dividend_yield = 0.01; // Example dividend yield
        option.volatility = 0.20;
        option.time_to_expiry = 0.25; // 3 months
        option.option_type = QuantLib::Option::Call; // Default to Call
        option.symbol_id = 1; // Example symbol ID
        return option;
    }
};

TEST_F(ALOEngineTest, BasicCallPricing) {
    Pricing::OptionData option = create_default_option_data();
    option.option_type = QuantLib::Option::Call;
    option.strike_price = 100.0; // At-the-money

    double price = engine_->calculate_option_price(option);
    
    EXPECT_GT(price, 0.0);
    // For an ATM call, price should be less than underlying_price * exp(-dividend_yield * T)
    // and greater than a certain minimum (e.g. Black-Scholes price for European if American)
    // A simple sanity check: price < underlying_price
    EXPECT_LT(price, option.underlying_price); 
}

TEST_F(ALOEngineTest, BasicPutPricing) {
    Pricing::OptionData option = create_default_option_data();
    option.option_type = QuantLib::Option::Put;
    option.strike_price = 105.0; // In-the-money put

    double price = engine_->calculate_option_price(option);
    
    EXPECT_GT(price, 0.0);
    EXPECT_LT(price, option.strike_price); // Price of a put cannot exceed its strike

    // For an American ITM put, price should be >= intrinsic value
    double intrinsic_value = std::max(0.0, option.strike_price - option.underlying_price);
    EXPECT_GE(price, intrinsic_value - 1e-9); // Allow for small numerical tolerance
}

TEST_F(ALOEngineTest, GreeksCalculationForCall) {
    Pricing::OptionData option = create_default_option_data(); // ATM Call
    
    Pricing::OptionGreeks greeks = engine_->calculate_greeks(option);
    
    // Basic sanity checks for an ATM call option
    EXPECT_GT(greeks.price, 0.0);
    // Delta: 0 < delta < 1 (typically around 0.5 for ATM)
    EXPECT_GT(greeks.delta, 0.0);
    EXPECT_LT(greeks.delta, 1.0);
    // Gamma: Should be positive
    EXPECT_GT(greeks.gamma, 0.0);
    // Theta: Should be negative (time decay)
    EXPECT_LT(greeks.theta, 0.0);
    // Vega: Should be positive
    EXPECT_GT(greeks.vega, 0.0);
    // Rho: Should be positive for a call
    EXPECT_GT(greeks.rho, 0.0);

    // Check for NaN or Inf values
    EXPECT_TRUE(std::isfinite(greeks.price));
    EXPECT_TRUE(std::isfinite(greeks.delta));
    EXPECT_TRUE(std::isfinite(greeks.gamma));
    EXPECT_TRUE(std::isfinite(greeks.theta));
    EXPECT_TRUE(std::isfinite(greeks.vega));
    EXPECT_TRUE(std::isfinite(greeks.rho));
}

TEST_F(ALOEngineTest, BatchPricingConsistency) {
    std::vector<Pricing::OptionData> options;
    
    Pricing::OptionData call_option = create_default_option_data();
    call_option.option_type = QuantLib::Option::Call;
    
    Pricing::OptionData put_option = create_default_option_data();
    put_option.option_type = QuantLib::Option::Put;
    put_option.strike_price = 95.0; // OTM Put

    options.push_back(call_option);
    options.push_back(put_option);

    // Batch calculate prices
    std::vector<double> batch_results;
    engine_->batch_calculate_prices(options, batch_results);
    ASSERT_EQ(batch_results.size(), options.size());

    // Single calculate prices for comparison
    double single_call_price = engine_->calculate_option_price(options[0]);
    double single_put_price = engine_->calculate_option_price(options[1]);

    // Prices should be very close (allowing for minor floating point differences if any)
    EXPECT_NEAR(batch_results[0], single_call_price, 1e-9);
    EXPECT_NEAR(batch_results[1], single_put_price, 1e-9);
}

TEST_F(ALOEngineTest, BatchGreeksConsistency) {
    std::vector<Pricing::OptionData> options;
    options.push_back(create_default_option_data()); // Call
    options.push_back(create_default_option_data()); 
    options.back().option_type = QuantLib::Option::Put; // Put

    std::vector<Pricing::OptionGreeks> batch_greeks_results;
    engine_->batch_calculate_greeks(options, batch_greeks_results);
    ASSERT_EQ(batch_greeks_results.size(), options.size());

    Pricing::OptionGreeks single_call_greeks = engine_->calculate_greeks(options[0]);
    Pricing::OptionGreeks single_put_greeks = engine_->calculate_greeks(options[1]);

    EXPECT_NEAR(batch_greeks_results[0].price, single_call_greeks.price, 1e-9);
    EXPECT_NEAR(batch_greeks_results[0].delta, single_call_greeks.delta, 1e-9);
    // ... (can check other greeks too) ...
    EXPECT_NEAR(batch_greeks_results[1].price, single_put_greeks.price, 1e-9);
    EXPECT_NEAR(batch_greeks_results[1].delta, single_put_greeks.delta, 1e-9);
}


TEST_F(ALOEngineTest, PerformanceBenchmarkSingleOption) {
    Pricing::OptionData option = create_default_option_data();
    
    const int num_iterations = 1000; // As in original test
    
    auto start_time = std::chrono::high_resolution_clock::now();
    for (int i = 0; i < num_iterations; ++i) {
        double price = engine_->calculate_option_price(option);
        // Check price validity inside loop to ensure engine is working, not just fast
        ASSERT_GT(price, 0.0); 
        ASSERT_TRUE(std::isfinite(price));
    }
    auto end_time = std::chrono::high_resolution_clock::now();
    
    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end_time - start_time);
    double avg_time_us = static_cast<double>(duration.count()) / num_iterations;
    
    std::cout << "[PERF] ALOEngineTest - Average single option pricing time: " << avg_time_us << " µs (" << num_iterations << " iterations)" << std::endl;
    
    // Performance target (example: should be under 100 microseconds on average on typical hardware)
    // This target is highly dependent on the machine, QuantLib engine settings, and option parameters.
    EXPECT_LT(avg_time_us, 200.0) << "Average pricing time exceeds performance target of 200 µs.";
}

TEST_F(ALOEngineTest, PricingNearExpiry) {
    Pricing::OptionData option = create_default_option_data();
    option.time_to_expiry = 1.0 / 365.0; // 1 day to expiry
    option.option_type = QuantLib::Option::Call;
    option.strike_price = 100.0; // ATM

    double price = engine_->calculate_option_price(option);
    EXPECT_GT(price, 0.0); // Should still have some time value
    EXPECT_NEAR(price, std::max(0.0, option.underlying_price - option.strike_price), 0.5); // Close to intrinsic for ATM near expiry (rough check)

    Pricing::OptionGreeks greeks = engine_->calculate_greeks(option);
    EXPECT_TRUE(std::isfinite(greeks.theta)) << "Theta should be finite even near expiry.";
    // Theta will be large (negative)
}

TEST_F(ALOEngineTest, DeepInTheMoneyAmericanCall) {
    // Deep ITM American call should ideally be priced close to S - K*exp(-rT) (early exercise often optimal if no dividends)
    // Or S - K if dividend yield q > risk_free_rate r.
    // If q <= r, American call = European call.
    Pricing::OptionData option = create_default_option_data();
    option.underlying_price = 120.0;
    option.strike_price = 80.0; // Deep ITM
    option.option_type = QuantLib::Option::Call;
    option.dividend_yield = 0.0; // No dividends, early exercise not optimal for American Call
                                 // So, C_Am = C_EU

    double price = engine_->calculate_option_price(option);
    double intrinsic = option.underlying_price - option.strike_price; // 40.0
    
    // Price should be at least intrinsic.
    EXPECT_GE(price, intrinsic - 1e-9);
    // For American call with no dividends, price = European price.
    // It should be greater than intrinsic if T > 0 or vol > 0.
    EXPECT_GT(price, intrinsic); 
}

TEST_F(ALOEngineTest, DeepOutOfMoneyAmericanPut) {
    Pricing::OptionData option = create_default_option_data();
    option.underlying_price = 100.0;
    option.strike_price = 70.0; // Deep OTM
    option.option_type = QuantLib::Option::Put;

    double price = engine_->calculate_option_price(option);
    EXPECT_GT(price, 0.0); // Should still have some (small) time value
    EXPECT_LT(price, 1.0); // Expect very small price for deep OTM
}
// test/quantlib/pricing_test.cpp
#include "src/quantlib/pricing/alo_engine.h" // Adjusted path
#include "src/quantlib/core/memory_pool.h"    // Adjusted path
#include "gtest/gtest.h"
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
        mem_pool_ = std::make_unique<Core::MemoryPool>(64 * 1024 * 1024); // 64MB
        engine_ = std::make_unique<Pricing::QuantLibALOEngine>(*mem_pool_);
    }

    void TearDown() override {
        // unique_ptr will handle cleanup
    }

    Pricing::OptionData create_default_option_data() {
        Pricing::OptionData option;
        option.underlying_price = 100.0;
        option.strike_price = 100.0;
        option.risk_free_rate = 0.05;
        option.dividend_yield = 0.01;
        option.volatility = 0.20;
        option.time_to_expiry = 0.25; 
        option.option_type = QuantLib::Option::Call;
        option.symbol_id = 1; 
        return option;
    }
};

TEST_F(ALOEngineTest, BasicCallPricing) {
    Pricing::OptionData option = create_default_option_data();
    option.option_type = QuantLib::Option::Call;
    option.strike_price = 100.0;

    double price = engine_->calculate_option_price(option);
    
    EXPECT_GT(price, 0.0);
    EXPECT_LT(price, option.underlying_price); 
}

TEST_F(ALOEngineTest, BasicPutPricing) {
    Pricing::OptionData option = create_default_option_data();
    option.option_type = QuantLib::Option::Put;
    option.strike_price = 105.0; 

    double price = engine_->calculate_option_price(option);
    
    EXPECT_GT(price, 0.0);
    EXPECT_LT(price, option.strike_price); 

    double intrinsic_value = std::max(0.0, option.strike_price - option.underlying_price);
    EXPECT_GE(price, intrinsic_value - 1e-9); 
}

TEST_F(ALOEngineTest, GreeksCalculationForCall) {
    Pricing::OptionData option = create_default_option_data(); 
    
    Pricing::OptionGreeks greeks = engine_->calculate_greeks(option);
    
    EXPECT_GT(greeks.price, 0.0);
    EXPECT_GT(greeks.delta, 0.0);
    EXPECT_LT(greeks.delta, 1.0);
    EXPECT_GT(greeks.gamma, 0.0);
    EXPECT_LT(greeks.theta, 0.0);
    EXPECT_GT(greeks.vega, 0.0);
    EXPECT_GT(greeks.rho, 0.0);

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
    put_option.strike_price = 95.0; 

    options.push_back(call_option);
    options.push_back(put_option);

    std::vector<double> batch_results;
    engine_->batch_calculate_prices(options, batch_results);
    ASSERT_EQ(batch_results.size(), options.size());

    double single_call_price = engine_->calculate_option_price(options[0]);
    double single_put_price = engine_->calculate_option_price(options[1]);

    EXPECT_NEAR(batch_results[0], single_call_price, 1e-9);
    EXPECT_NEAR(batch_results[1], single_put_price, 1e-9);
}

TEST_F(ALOEngineTest, BatchGreeksConsistency) {
    std::vector<Pricing::OptionData> options;
    options.push_back(create_default_option_data()); 
    options.push_back(create_default_option_data()); 
    options.back().option_type = QuantLib::Option::Put; 

    std::vector<Pricing::OptionGreeks> batch_greeks_results;
    engine_->batch_calculate_greeks(options, batch_greeks_results);
    ASSERT_EQ(batch_greeks_results.size(), options.size());

    Pricing::OptionGreeks single_call_greeks = engine_->calculate_greeks(options[0]);
    Pricing::OptionGreeks single_put_greeks = engine_->calculate_greeks(options[1]);

    EXPECT_NEAR(batch_greeks_results[0].price, single_call_greeks.price, 1e-9);
    EXPECT_NEAR(batch_greeks_results[0].delta, single_call_greeks.delta, 1e-9);
    EXPECT_NEAR(batch_greeks_results[1].price, single_put_greeks.price, 1e-9);
    EXPECT_NEAR(batch_greeks_results[1].delta, single_put_greeks.delta, 1e-9);
}


TEST_F(ALOEngineTest, PerformanceBenchmarkSingleOption) {
    Pricing::OptionData option = create_default_option_data();
    
    const int num_iterations = 100; // Reduced for quicker CI, increase for local bench
    
    auto start_time = std::chrono::high_resolution_clock::now();
    for (int i = 0; i < num_iterations; ++i) {
        double price = engine_->calculate_option_price(option);
        ASSERT_GT(price, 0.0); 
        ASSERT_TRUE(std::isfinite(price));
    }
    auto end_time = std::chrono::high_resolution_clock::now();
    
    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end_time - start_time);
    double avg_time_us = static_cast<double>(duration.count()) / num_iterations;
    
    std::cout << "[PERF] ALOEngineTest - Average single option pricing time: " << avg_time_us << " µs (" << num_iterations << " iterations)" << std::endl;
    EXPECT_LT(avg_time_us, 2000.0) << "Average pricing time exceeds performance target of 2000 µs (relaxed for tests)."; // Relaxed target
}

TEST_F(ALOEngineTest, PricingNearExpiry) {
    Pricing::OptionData option = create_default_option_data();
    option.time_to_expiry = 1.0 / 365.0; 
    option.option_type = QuantLib::Option::Call;
    option.strike_price = 100.0; 

    double price = engine_->calculate_option_price(option);
    EXPECT_GT(price, 0.0); 
    EXPECT_NEAR(price, std::max(0.0, option.underlying_price - option.strike_price), 0.5);

    Pricing::OptionGreeks greeks = engine_->calculate_greeks(option);
    EXPECT_TRUE(std::isfinite(greeks.theta)) << "Theta should be finite even near expiry.";
}

TEST_F(ALOEngineTest, DeepInTheMoneyAmericanCall) {
    Pricing::OptionData option = create_default_option_data();
    option.underlying_price = 120.0;
    option.strike_price = 80.0; 
    option.option_type = QuantLib::Option::Call;
    option.dividend_yield = 0.0;

    double price = engine_->calculate_option_price(option);
    double intrinsic = option.underlying_price - option.strike_price; 
    
    EXPECT_GE(price, intrinsic - 1e-9);
    EXPECT_GT(price, intrinsic); 
}

TEST_F(ALOEngineTest, DeepOutOfMoneyAmericanPut) {
    Pricing::OptionData option = create_default_option_data();
    option.underlying_price = 100.0;
    option.strike_price = 70.0; 
    option.option_type = QuantLib::Option::Put;

    double price = engine_->calculate_option_price(option);
    EXPECT_GT(price, 0.0); 
    EXPECT_LT(price, 1.0); 
}

// No main() function needed here when using GTest::Main
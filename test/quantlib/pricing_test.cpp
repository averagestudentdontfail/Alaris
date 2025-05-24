// test/quantlib/pricing_test.cpp
#include "../../src/quantlib/pricing/alo_engine.h"
#include "../../src/quantlib/core/memory_pool.h"
#include <gtest/gtest.h>
#include <vector>
#include <chrono>

using namespace Alaris;

class ALOEngineTest : public ::testing::Test {
protected:
    void SetUp() override {
        mem_pool_ = std::make_unique<Core::MemoryPool>(64 * 1024 * 1024);
        engine_ = std::make_unique<Pricing::QuantLibALOEngine>(*mem_pool_);
    }

    std::unique_ptr<Core::MemoryPool> mem_pool_;
    std::unique_ptr<Pricing::QuantLibALOEngine> engine_;
};

TEST_F(ALOEngineTest, BasicPutPricing) {
    Pricing::OptionData option;
    option.underlying_price = 100.0;
    option.strike_price = 105.0;
    option.risk_free_rate = 0.05;
    option.dividend_yield = 0.0;
    option.volatility = 0.20;
    option.time_to_expiry = 0.25; // 3 months
    option.option_type = QuantLib::Option::Put;
    
    double price = engine_->calculate_option_price(option);
    
    // American put should be worth more than European put
    EXPECT_GT(price, 0.0);
    EXPECT_LT(price, option.strike_price); // Sanity check
    
    // For ITM put, should be greater than intrinsic value
    double intrinsic = std::max(0.0, option.strike_price - option.underlying_price);
    EXPECT_GE(price, intrinsic);
}

TEST_F(ALOEngineTest, GreeksCalculation) {
    Pricing::OptionData option;
    option.underlying_price = 100.0;
    option.strike_price = 100.0;
    option.risk_free_rate = 0.05;
    option.dividend_yield = 0.0;
    option.volatility = 0.20;
    option.time_to_expiry = 0.25;
    option.option_type = QuantLib::Option::Call;
    
    auto greeks = engine_->calculate_greeks(option);
    
    // Basic sanity checks for ATM call
    EXPECT_GT(greeks.price, 0.0);
    EXPECT_GT(greeks.delta, 0.0);
    EXPECT_LT(greeks.delta, 1.0);
    EXPECT_GT(greeks.gamma, 0.0);
    EXPECT_LT(greeks.theta, 0.0); // Theta should be negative
    EXPECT_GT(greeks.vega, 0.0);
    EXPECT_GT(greeks.rho, 0.0);
}

TEST_F(ALOEngineTest, BatchPricing) {
    std::vector<Pricing::OptionData> options;
    std::vector<double> results;
    
    // Create batch of options with different strikes
    for (double strike = 95.0; strike <= 105.0; strike += 1.0) {
        Pricing::OptionData option;
        option.underlying_price = 100.0;
        option.strike_price = strike;
        option.risk_free_rate = 0.05;
        option.dividend_yield = 0.0;
        option.volatility = 0.20;
        option.time_to_expiry = 0.25;
        option.option_type = QuantLib::Option::Call;
        
        options.push_back(option);
    }
    
    auto start = std::chrono::high_resolution_clock::now();
    engine_->batch_calculate_prices(options, results);
    auto end = std::chrono::high_resolution_clock::now();
    
    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
    std::cout << "Batch pricing of " << options.size() << " options took " << duration.count() << " μs" << std::endl;
    
    EXPECT_EQ(results.size(), options.size());
    
    // Verify monotonicity for call options (higher strike -> lower price)
    for (size_t i = 1; i < results.size(); ++i) {
        EXPECT_LE(results[i], results[i-1]);
    }
}

TEST_F(ALOEngineTest, PerformanceBenchmark) {
    Pricing::OptionData option;
    option.underlying_price = 100.0;
    option.strike_price = 100.0;
    option.risk_free_rate = 0.05;
    option.dividend_yield = 0.0;
    option.volatility = 0.20;
    option.time_to_expiry = 0.25;
    option.option_type = QuantLib::Option::Call;
    
    const int num_iterations = 1000;
    
    auto start = std::chrono::high_resolution_clock::now();
    
    for (int i = 0; i < num_iterations; ++i) {
        double price = engine_->calculate_option_price(option);
        EXPECT_GT(price, 0.0);
    }
    
    auto end = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
    
    double avg_time = static_cast<double>(duration.count()) / num_iterations;
    std::cout << "Average pricing time: " << avg_time << " μs" << std::endl;
    
    // Performance target: should price options in under 100 microseconds on average
    EXPECT_LT(avg_time, 100.0);
}
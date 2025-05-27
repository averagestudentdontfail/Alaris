// test/quantlib/pricing_test.cpp
#include "src/quantlib/pricing/alo_engine.h"
#include "src/quantlib/core/memory_pool.h"
#include "gtest/gtest.h"
#include <vector>
#include <chrono>
#include <cmath>

using namespace Alaris;

class EnhancedALOEngineTest : public ::testing::Test {
protected:
    std::unique_ptr<Core::MemoryPool> mem_pool_;
    std::unique_ptr<Pricing::QuantLibALOEngine> engine_;

    void SetUp() override {
        mem_pool_ = std::make_unique<Core::MemoryPool>(64 * 1024 * 1024);
        engine_ = std::make_unique<Pricing::QuantLibALOEngine>(*mem_pool_);
    }

    Pricing::OptionData create_test_option(QuantLib::Option::Type type = QuantLib::Option::Call,
                                         double strike_ratio = 1.0,
                                         double time_to_expiry = 0.25) {
        Pricing::OptionData option;
        option.underlying_price = 100.0;
        option.strike_price = 100.0 * strike_ratio;
        option.risk_free_rate = 0.05;
        option.dividend_yield = 0.02;
        option.volatility = 0.20;
        option.time_to_expiry = time_to_expiry;
        option.option_type = type;
        option.symbol_id = 12345;
        return option;
    }
};

TEST_F(EnhancedALOEngineTest, BasicAmericanOptionPricing) {
    auto call_option = create_test_option(QuantLib::Option::Call);
    auto put_option = create_test_option(QuantLib::Option::Put);
    
    double call_price = engine_->calculate_option_price(call_option);
    double put_price = engine_->calculate_option_price(put_option);
    
    EXPECT_GT(call_price, 0.0);
    EXPECT_GT(put_price, 0.0);
    
    // American options should be worth at least their intrinsic value
    double call_intrinsic = std::max(0.0, call_option.underlying_price - call_option.strike_price);
    double put_intrinsic = std::max(0.0, put_option.strike_price - put_option.underlying_price);
    
    EXPECT_GE(call_price, call_intrinsic - 1e-6);
    EXPECT_GE(put_price, put_intrinsic - 1e-6);
    
    std::cout << "Call price: " << call_price << ", Put price: " << put_price << std::endl;
}

TEST_F(EnhancedALOEngineTest, NumericalGreeksCalculation) {
    auto option = create_test_option();
    
    auto greeks = engine_->calculate_greeks(option);
    
    // Verify all Greeks are finite and reasonable
    EXPECT_TRUE(std::isfinite(greeks.price));
    EXPECT_TRUE(std::isfinite(greeks.delta));
    EXPECT_TRUE(std::isfinite(greeks.gamma));
    EXPECT_TRUE(std::isfinite(greeks.theta));
    EXPECT_TRUE(std::isfinite(greeks.vega));
    EXPECT_TRUE(std::isfinite(greeks.rho));
    EXPECT_TRUE(std::isfinite(greeks.vanna));
    EXPECT_TRUE(std::isfinite(greeks.volga));
    
    // Call option Greeks should satisfy basic bounds
    EXPECT_GT(greeks.price, 0.0);
    EXPECT_GT(greeks.delta, 0.0);
    EXPECT_LT(greeks.delta, 1.0);
    EXPECT_GT(greeks.gamma, 0.0);
    EXPECT_LT(greeks.theta, 0.0);  // Time decay
    EXPECT_GT(greeks.vega, 0.0);
    EXPECT_GT(greeks.rho, 0.0);
    
    std::cout << "Greeks - Delta: " << greeks.delta 
              << ", Gamma: " << greeks.gamma 
              << ", Theta: " << greeks.theta 
              << ", Vega: " << greeks.vega
              << ", Vanna: " << greeks.vanna
              << ", Volga: " << greeks.volga << std::endl;
}

TEST_F(EnhancedALOEngineTest, MoneynessDependentGreeks) {
    // Test Greeks across different moneyness levels
    std::vector<double> strike_ratios = {0.9, 0.95, 1.0, 1.05, 1.1};
    
    for (double ratio : strike_ratios) {
        auto option = create_test_option(QuantLib::Option::Call, ratio);
        auto greeks = engine_->calculate_greeks(option);
        
        EXPECT_TRUE(std::isfinite(greeks.delta));
        EXPECT_TRUE(std::isfinite(greeks.gamma));
        
        // Delta should be higher for ITM options
        if (ratio < 1.0) {  // ITM
            EXPECT_GT(greeks.delta, 0.5);
        } else if (ratio > 1.0) {  // OTM
            EXPECT_LT(greeks.delta, 0.5);
        }
        
        // Gamma should peak around ATM
        EXPECT_GT(greeks.gamma, 0.0);
        
        std::cout << "Strike ratio: " << ratio 
                  << ", Delta: " << greeks.delta 
                  << ", Gamma: " << greeks.gamma << std::endl;
    }
}

TEST_F(EnhancedALOEngineTest, ImpliedVolatilityCalculation) {
    auto option = create_test_option();
    double true_price = engine_->calculate_option_price(option);
    
    // Test implied vol calculation
    double calculated_iv = engine_->calculate_implied_volatility(option, true_price);
    
    EXPECT_NEAR(calculated_iv, option.volatility, 0.001);  // Should recover original vol
    EXPECT_GT(calculated_iv, 0.01);  // Reasonable lower bound
    EXPECT_LT(calculated_iv, 5.0);   // Reasonable upper bound
    
    std::cout << "Original vol: " << option.volatility 
              << ", Calculated IV: " << calculated_iv << std::endl;
}

TEST_F(EnhancedALOEngineTest, PortfolioGreeksCalculation) {
    std::vector<std::pair<Pricing::OptionData, double>> portfolio;
    
    // Create a simple portfolio: long call, short put
    auto call = create_test_option(QuantLib::Option::Call);
    auto put = create_test_option(QuantLib::Option::Put);
    
    portfolio.emplace_back(call, 10.0);   // Long 10 calls
    portfolio.emplace_back(put, -5.0);    // Short 5 puts
    
    double portfolio_delta = engine_->calculate_portfolio_delta(portfolio);
    double portfolio_gamma = engine_->calculate_portfolio_gamma(portfolio);
    double portfolio_vega = engine_->calculate_portfolio_vega(portfolio);
    
    EXPECT_TRUE(std::isfinite(portfolio_delta));
    EXPECT_TRUE(std::isfinite(portfolio_gamma));
    EXPECT_TRUE(std::isfinite(portfolio_vega));
    
    // Portfolio delta should be positive (net long delta)
    EXPECT_GT(portfolio_delta, 0.0);
    
    std::cout << "Portfolio Greeks - Delta: " << portfolio_delta 
              << ", Gamma: " << portfolio_gamma 
              << ", Vega: " << portfolio_vega << std::endl;
}

TEST_F(EnhancedALOEngineTest, BatchProcessingPerformance) {
    const size_t num_options = 50;
    std::vector<Pricing::OptionData> options;
    
    // Create diverse option portfolio
    for (size_t i = 0; i < num_options; ++i) {
        double strike_ratio = 0.8 + 0.4 * i / num_options;  // 80% to 120%
        double time_to_expiry = 0.02 + 0.5 * i / num_options;  // 1 week to 6 months
        QuantLib::Option::Type type = (i % 2 == 0) ? QuantLib::Option::Call : QuantLib::Option::Put;
        
        options.push_back(create_test_option(type, strike_ratio, time_to_expiry));
        options.back().symbol_id = 10000 + i;
    }
    
    // Test batch pricing
    auto start = std::chrono::high_resolution_clock::now();
    std::vector<double> prices;
    engine_->batch_calculate_prices(options, prices);
    auto end = std::chrono::high_resolution_clock::now();
    
    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
    double avg_time = static_cast<double>(duration.count()) / num_options;
    
    ASSERT_EQ(prices.size(), num_options);
    for (double price : prices) {
        EXPECT_GT(price, 0.0);
        EXPECT_TRUE(std::isfinite(price));
    }
    
    // Test batch Greeks calculation
    start = std::chrono::high_resolution_clock::now();
    std::vector<Pricing::OptionGreeks> greeks_batch;
    engine_->batch_calculate_greeks(options, greeks_batch);
    end = std::chrono::high_resolution_clock::now();
    
    auto greeks_duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
    double avg_greeks_time = static_cast<double>(greeks_duration.count()) / num_options;
    
    ASSERT_EQ(greeks_batch.size(), num_options);
    for (const auto& greeks : greeks_batch) {
        EXPECT_TRUE(std::isfinite(greeks.delta));
        EXPECT_TRUE(std::isfinite(greeks.gamma));
        EXPECT_TRUE(std::isfinite(greeks.vega));
    }
    
    std::cout << "Batch performance - Avg pricing: " << avg_time 
              << " μs, Avg Greeks: " << avg_greeks_time << " μs per option" << std::endl;
    
    // Performance targets (relaxed for CI)
    EXPECT_LT(avg_time, 5000.0);        // <5ms per option for pricing
    EXPECT_LT(avg_greeks_time, 15000.0); // <15ms per option for full Greeks
}

TEST_F(EnhancedALOEngineTest, CacheEffectiveness) {
    auto option = create_test_option();
    
    // Clear cache and get baseline stats
    engine_->clear_cache();
    engine_->reset_performance_stats();
    
    // Calculate same option multiple times
    for (int i = 0; i < 10; ++i) {
        auto greeks = engine_->calculate_greeks(option);
        EXPECT_TRUE(std::isfinite(greeks.price));
    }
    
    auto final_stats = engine_->get_performance_stats();
    
    // Should have cache hits after first calculation
    EXPECT_GT(final_stats.cache_hits, 0);
    EXPECT_GT(final_stats.cache_hit_ratio, 0.5);  // At least 50% hit ratio
    
    std::cout << "Cache stats - Hits: " << final_stats.cache_hits 
              << ", Misses: " << final_stats.cache_misses 
              << ", Hit ratio: " << final_stats.cache_hit_ratio << std::endl;
}

TEST_F(EnhancedALOEngineTest, AmericanVsEuropeanComparison) {
    // For dividend-paying stocks, American calls should be worth more than European
    auto american_call = create_test_option(QuantLib::Option::Call);
    american_call.dividend_yield = 0.05;  // 5% dividend yield
    
    double american_price = engine_->calculate_option_price(american_call);
    
    // Calculate approximate European price using Black-Scholes for comparison
    // (In practice, you'd use a European pricing engine)
    double S = american_call.underlying_price;
    double K = american_call.strike_price;
    double T = american_call.time_to_expiry;
    double r = american_call.risk_free_rate;
    double q = american_call.dividend_yield;
    double sigma = american_call.volatility;
    
    double d1 = (std::log(S/K) + (r - q + 0.5*sigma*sigma)*T) / (sigma*std::sqrt(T));
    double d2 = d1 - sigma*std::sqrt(T);
    
    auto norm_cdf = [](double x) {
        return 0.5 * (1.0 + std::erf(x / std::sqrt(2.0)));
    };
    
    double european_price = S * std::exp(-q*T) * norm_cdf(d1) - K * std::exp(-r*T) * norm_cdf(d2);
    
    EXPECT_GT(american_price, european_price - 1e-6);  // American >= European
    
    std::cout << "American call: " << american_price 
              << ", European call (approx): " << european_price 
              << ", Early exercise value: " << (american_price - european_price) << std::endl;
}

TEST_F(EnhancedALOEngineTest, StressTestExtremeParameters) {
    // Test with extreme but valid parameters
    std::vector<Pricing::OptionData> stress_options;
    
    // Very short expiry
    auto short_expiry = create_test_option();
    short_expiry.time_to_expiry = 1.0/365.0;  // 1 day
    stress_options.push_back(short_expiry);
    
    // Very long expiry
    auto long_expiry = create_test_option();
    long_expiry.time_to_expiry = 2.0;  // 2 years
    stress_options.push_back(long_expiry);
    
    // High volatility
    auto high_vol = create_test_option();
    high_vol.volatility = 1.0;  // 100%
    stress_options.push_back(high_vol);
    
    // Deep ITM
    auto deep_itm = create_test_option();
    deep_itm.strike_price = 50.0;  // 50% of spot
    stress_options.push_back(deep_itm);
    
    // Deep OTM
    auto deep_otm = create_test_option();
    deep_otm.strike_price = 150.0;  // 150% of spot
    stress_options.push_back(deep_otm);
    
    for (size_t i = 0; i < stress_options.size(); ++i) {
        auto& option = stress_options[i];
        
        try {
            double price = engine_->calculate_option_price(option);
            auto greeks = engine_->calculate_greeks(option);
            
            EXPECT_TRUE(std::isfinite(price));
            EXPECT_GT(price, 0.0);
            EXPECT_TRUE(std::isfinite(greeks.delta));
            EXPECT_TRUE(std::isfinite(greeks.gamma));
            
            std::cout << "Stress test " << i << " - Price: " << price 
                      << ", Delta: " << greeks.delta << std::endl;
                      
        } catch (const std::exception& e) {
            FAIL() << "Exception in stress test " << i << ": " << e.what();
        }
    }
}

TEST_F(EnhancedALOEngineTest, BumpSizeConfiguration) {
    auto option = create_test_option();
    
    // Test with different bump sizes
    engine_->set_bump_sizes(0.005, 0.0005, 0.5/365.0, 0.00005);  // Smaller bumps
    auto greeks_small = engine_->calculate_greeks(option);
    
    engine_->set_bump_sizes(0.02, 0.002, 2.0/365.0, 0.0002);    // Larger bumps
    auto greeks_large = engine_->calculate_greeks(option);
    
    // Greeks should be reasonably close regardless of bump size
    EXPECT_NEAR(greeks_small.delta, greeks_large.delta, 0.05);
    EXPECT_NEAR(greeks_small.gamma, greeks_large.gamma, 0.01);
    EXPECT_NEAR(greeks_small.vega, greeks_large.vega, 0.5);
    
    std::cout << "Small bumps - Delta: " << greeks_small.delta << ", Gamma: " << greeks_small.gamma << std::endl;
    std::cout << "Large bumps - Delta: " << greeks_large.delta << ", Gamma: " << greeks_large.gamma << std::endl;
}
/**
 * @file test_helpers.cpp
 * @brief Common test utilities and helpers for Alaris trading system tests
 */

#include "test_helpers.h"
#include <iostream>
#include <chrono>
#include <random>
#include <cmath>

namespace alaris {
namespace test {

TestTimer::TestTimer(const std::string& name) : name_(name) {
    start_ = std::chrono::high_resolution_clock::now();
}

TestTimer::~TestTimer() {
    auto end = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start_);
    std::cout << "[TIMER] " << name_ << ": " << duration.count() << " μs" << std::endl;
}

double TestTimer::elapsed_microseconds() const {
    auto now = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(now - start_);
    return static_cast<double>(duration.count());
}

TestMarketDataGenerator::TestMarketDataGenerator(double initial_price, double volatility)
    : price_(initial_price), volatility_(volatility), rng_(std::random_device{}()) {
}

MarketData TestMarketDataGenerator::next() {
    static std::normal_distribution<double> normal(0.0, 1.0);
    
    // Simple geometric Brownian motion
    double dt = 1.0 / 252.0 / 24.0 / 60.0; // 1 minute
    double drift = 0.05 * dt; // 5% annual drift
    double shock = volatility_ * std::sqrt(dt) * normal(rng_);
    
    price_ *= std::exp(drift + shock);
    
    MarketData data;
    data.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::system_clock::now().time_since_epoch()).count();
    data.underlying = price_;
    data.bid = price_ * 0.999; // 0.1% bid-ask spread
    data.ask = price_ * 1.001;
    
    return data;
}

TestOptionDataGenerator::TestOptionDataGenerator() {
    // Generate a variety of option strikes around current spot
    strikes_ = {90, 95, 100, 105, 110, 115, 120};
    maturities_ = {1.0/12.0, 2.0/12.0, 3.0/12.0, 6.0/12.0, 1.0}; // 1M to 1Y
}

std::vector<OptionData> TestOptionDataGenerator::generate_option_chain(double spot) {
    std::vector<OptionData> options;
    
    for (double strike : strikes_) {
        double adjusted_strike = strike * spot / 100.0; // Scale to current spot
        
        for (double maturity : maturities_) {
            // Put option
            OptionData put_option;
            put_option.strike = adjusted_strike;
            put_option.maturity = maturity;
            put_option.option_type = OptionType::Put;
            put_option.spot = spot;
            options.push_back(put_option);
            
            // Call option
            OptionData call_option;
            call_option.strike = adjusted_strike;
            call_option.maturity = maturity;
            call_option.option_type = OptionType::Call;
            call_option.spot = spot;
            options.push_back(call_option);
        }
    }
    
    return options;
}

bool TestValidator::validate_pricing_result(const PricingResult& result) {
    if (std::isnan(result.price) || std::isinf(result.price)) {
        std::cerr << "Invalid price: " << result.price << std::endl;
        return false;
    }
    
    if (result.price < 0.0) {
        std::cerr << "Negative price: " << result.price << std::endl;
        return false;
    }
    
    if (std::isnan(result.delta) || std::isinf(result.delta)) {
        std::cerr << "Invalid delta: " << result.delta << std::endl;
        return false;
    }
    
    if (std::isnan(result.gamma) || std::isinf(result.gamma)) {
        std::cerr << "Invalid gamma: " << result.gamma << std::endl;
        return false;
    }
    
    if (result.gamma < 0.0) {
        std::cerr << "Negative gamma: " << result.gamma << std::endl;
        return false;
    }
    
    return true;
}

bool TestValidator::validate_volatility_forecast(double forecast, double min_vol, double max_vol) {
    if (std::isnan(forecast) || std::isinf(forecast)) {
        std::cerr << "Invalid volatility forecast: " << forecast << std::endl;
        return false;
    }
    
    if (forecast < min_vol || forecast > max_vol) {
        std::cerr << "Volatility forecast out of range: " << forecast 
                  << " (expected: " << min_vol << " - " << max_vol << ")" << std::endl;
        return false;
    }
    
    return true;
}

bool TestValidator::compare_doubles(double a, double b, double tolerance) {
    return std::abs(a - b) <= tolerance;
}

void TestReporter::start_test(const std::string& test_name) {
    current_test_ = test_name;
    test_start_ = std::chrono::high_resolution_clock::now();
    std::cout << "[TEST START] " << test_name << std::endl;
}

void TestReporter::end_test(bool passed) {
    auto end = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - test_start_);
    
    std::string status = passed ? "PASSED" : "FAILED";
    std::cout << "[TEST END] " << current_test_ << " - " << status 
              << " (" << duration.count() << " μs)" << std::endl;
    
    if (passed) {
        passed_tests_++;
    } else {
        failed_tests_++;
    }
    total_tests_++;
}

void TestReporter::print_summary() {
    std::cout << "\n=== Test Summary ===" << std::endl;
    std::cout << "Total tests: " << total_tests_ << std::endl;
    std::cout << "Passed: " << passed_tests_ << std::endl;
    std::cout << "Failed: " << failed_tests_ << std::endl;
    std::cout << "Success rate: " << (total_tests_ > 0 ? 
        (100.0 * passed_tests_ / total_tests_) : 0.0) << "%" << std::endl;
    std::cout << "===================" << std::endl;
}

PerformanceBenchmark::PerformanceBenchmark(const std::string& name, size_t iterations)
    : name_(name), iterations_(iterations) {
}

void PerformanceBenchmark::run(std::function<void()> test_function) {
    std::vector<double> timings;
    timings.reserve(iterations_);
    
    std::cout << "[BENCHMARK] Starting " << name_ << " (" << iterations_ << " iterations)" << std::endl;
    
    for (size_t i = 0; i < iterations_; ++i) {
        auto start = std::chrono::high_resolution_clock::now();
        test_function();
        auto end = std::chrono::high_resolution_clock::now();
        
        auto duration = std::chrono::duration_cast<std::chrono::nanoseconds>(end - start);
        timings.push_back(static_cast<double>(duration.count()));
    }
    
    // Calculate statistics
    std::sort(timings.begin(), timings.end());
    
    double min_time = timings.front();
    double max_time = timings.back();
    double median = timings[timings.size() / 2];
    double p95 = timings[static_cast<size_t>(timings.size() * 0.95)];
    double p99 = timings[static_cast<size_t>(timings.size() * 0.99)];
    
    double sum = 0.0;
    for (double t : timings) {
        sum += t;
    }
    double mean = sum / timings.size();
    
    std::cout << "[BENCHMARK] " << name_ << " Results:" << std::endl;
    std::cout << "  Min:    " << min_time / 1000.0 << " μs" << std::endl;
    std::cout << "  Mean:   " << mean / 1000.0 << " μs" << std::endl;
    std::cout << "  Median: " << median / 1000.0 << " μs" << std::endl;
    std::cout << "  95th:   " << p95 / 1000.0 << " μs" << std::endl;
    std::cout << "  99th:   " << p99 / 1000.0 << " μs" << std::endl;
    std::cout << "  Max:    " << max_time / 1000.0 << " μs" << std::endl;
}

} // namespace test
} // namespace alaris
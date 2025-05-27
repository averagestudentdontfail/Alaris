// test/test_helpers.h
#pragma once

#include <string>
#include <vector>
#include <chrono>
#include <functional>
#include <random>
#include <cmath> // For std::abs, std::isnan, std::isinf
#include <iostream> // For TestTimer output, or remove if not needed

// Assuming these structs are defined in your actual codebase or here for tests
namespace Alaris { // Or global if that's the project structure
    namespace IPC { struct MarketDataMessage; } // Forward declare
    namespace Pricing { struct OptionData; struct OptionGreeks; }
}


namespace alaris {
namespace test {

// Forward declarations for types used by helpers
// Use actual types from your project by including their headers
// For example:
// #include "src/quantlib/ipc/message_types.h"
// #include "src/quantlib/pricing/alo_engine.h" // For OptionData, OptionGreeks

// Simplified structs for example, replace with actuals
struct MarketData { /* ... */ uint64_t timestamp; double underlying; double bid; double ask; };
enum class OptionType { Call, Put };
struct OptionData { /* ... */ double strike; double maturity; OptionType option_type; double spot; };
struct PricingResult { /* ... */ double price; double delta; double gamma; double theta; double vega; };


class TestTimer {
public:
    explicit TestTimer(const std::string& name);
    ~TestTimer();
    double elapsed_microseconds() const;
private:
    std::string name_;
    std::chrono::high_resolution_clock::time_point start_;
};

class TestMarketDataGenerator {
public:
    TestMarketDataGenerator(double initial_price = 100.0, double volatility = 0.2);
    MarketData next();
    void set_price(double price) { price_ = price; }
    void set_volatility(double vol) { volatility_ = vol; }
private:
    double price_;
    double volatility_;
    std::mt19937 rng_;
};

class TestOptionDataGenerator {
public:
    TestOptionDataGenerator();
    std::vector<OptionData> generate_option_chain(double spot);
private:
    std::vector<double> strikes_;
    std::vector<double> maturities_;
};

class TestValidator {
public:
    static bool validate_pricing_result(const PricingResult& result);
    static bool validate_volatility_forecast(double forecast, double min_vol = 0.01, double max_vol = 5.0);
    // compare_doubles is good, keep it or use GTest's EXPECT_NEAR/ASSERT_NEAR
    static bool compare_doubles(double a, double b, double tolerance = 1e-8) {
        return std::abs(a - b) <= tolerance;
    }
};

class PerformanceBenchmark {
public:
    PerformanceBenchmark(const std::string& name, size_t iterations = 1000);
    void run(std::function<void()> test_function);
private:
    std::string name_;
    size_t iterations_;
};

// Custom ALARIS_ASSERT macros are removed as Google Test provides ASSERT_*, EXPECT_*

} // namespace test
} // namespace alaris
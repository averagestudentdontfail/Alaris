/**
 * @file test_helpers.h
 * @brief Common test utilities and helpers for Alaris trading system tests
 */

#pragma once

#include <string>
#include <vector>
#include <chrono>
#include <functional>
#include <random>

namespace alaris {
namespace test {

// Forward declarations for types that will be defined in actual implementation
struct MarketData {
    uint64_t timestamp;
    double underlying;
    double bid;
    double ask;
};

enum class OptionType {
    Call,
    Put
};

struct OptionData {
    double strike;
    double maturity;
    OptionType option_type;
    double spot;
};

struct PricingResult {
    double price;
    double delta;
    double gamma;
    double theta;
    double vega;
};

/**
 * @brief Simple timer for measuring test execution time
 */
class TestTimer {
public:
    explicit TestTimer(const std::string& name);
    ~TestTimer();
    
    double elapsed_microseconds() const;

private:
    std::string name_;
    std::chrono::high_resolution_clock::time_point start_;
};

/**
 * @brief Generate realistic market data for testing
 */
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

/**
 * @brief Generate option data for testing strategies
 */
class TestOptionDataGenerator {
public:
    TestOptionDataGenerator();
    
    std::vector<OptionData> generate_option_chain(double spot);

private:
    std::vector<double> strikes_;
    std::vector<double> maturities_;
};

/**
 * @brief Validate calculation results
 */
class TestValidator {
public:
    static bool validate_pricing_result(const PricingResult& result);
    static bool validate_volatility_forecast(double forecast, double min_vol = 0.01, double max_vol = 5.0);
    static bool compare_doubles(double a, double b, double tolerance = 1e-8);
};

/**
 * @brief Test execution reporting
 */
class TestReporter {
public:
    void start_test(const std::string& test_name);
    void end_test(bool passed);
    void print_summary();
    
    int get_passed_tests() const { return passed_tests_; }
    int get_failed_tests() const { return failed_tests_; }
    int get_total_tests() const { return total_tests_; }

private:
    std::string current_test_;
    std::chrono::high_resolution_clock::time_point test_start_;
    int passed_tests_ = 0;
    int failed_tests_ = 0;
    int total_tests_ = 0;
};

/**
 * @brief Performance benchmarking utility
 */
class PerformanceBenchmark {
public:
    PerformanceBenchmark(const std::string& name, size_t iterations = 1000);
    
    void run(std::function<void()> test_function);

private:
    std::string name_;
    size_t iterations_;
};

/**
 * @brief Macros for easier test writing
 */
#define ALARIS_TEST_START(reporter, name) do { \
    (reporter).start_test(name); \
    bool test_passed = true; \
    try {

#define ALARIS_TEST_END(reporter) \
    } catch (const std::exception& e) { \
        std::cerr << "Test failed with exception: " << e.what() << std::endl; \
        test_passed = false; \
    } catch (...) { \
        std::cerr << "Test failed with unknown exception" << std::endl; \
        test_passed = false; \
    } \
    (reporter).end_test(test_passed); \
} while(0)

#define ALARIS_ASSERT(condition) do { \
    if (!(condition)) { \
        std::cerr << "Assertion failed: " << #condition << " at " << __FILE__ << ":" << __LINE__ << std::endl; \
        test_passed = false; \
    } \
} while(0)

#define ALARIS_ASSERT_DOUBLES_EQUAL(a, b, tolerance) do { \
    if (!TestValidator::compare_doubles(a, b, tolerance)) { \
        std::cerr << "Assertion failed: " << #a << " (" << (a) << ") != " << #b << " (" << (b) \
                  << ") within tolerance " << (tolerance) << " at " << __FILE__ << ":" << __LINE__ << std::endl; \
        test_passed = false; \
    } \
} while(0)

} // namespace test
} // namespace alaris
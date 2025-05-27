// test/test_helpers.cpp
#include "test_helpers.h" // Ensure this matches the actual header filename
#include <iostream>
#include <algorithm> // For std::sort
#include <cmath>     // For std::abs, std::isnan, std::isinf

namespace alaris {
namespace test {

TestTimer::TestTimer(const std::string& name) : name_(name) {
    start_ = std::chrono::high_resolution_clock::now();
}

TestTimer::~TestTimer() {
    auto end = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start_);
    // Output can be handled by GTest or removed if too verbose for general runs
    // std::cout << "[TIMER] " << name_ << ": " << duration.count() << " μs" << std::endl;
}

double TestTimer::elapsed_microseconds() const {
    auto now = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(now - start_);
    return static_cast<double>(duration.count());
}

// TestMarketDataGenerator and TestOptionDataGenerator implementations remain the same
// as they are helper classes not directly related to test macros.
// Their definitions from the provided file are assumed correct.

TestMarketDataGenerator::TestMarketDataGenerator(double initial_price, double volatility)
    : price_(initial_price), volatility_(volatility), rng_(std::random_device{}()) {
}

MarketData TestMarketDataGenerator::next() {
    static std::normal_distribution<double> normal(0.0, 1.0);
    double dt = 1.0 / 252.0 / 24.0 / 60.0; 
    double drift = 0.05 * dt; 
    double shock = volatility_ * std::sqrt(dt) * normal(rng_);
    price_ *= std::exp(drift + shock);
    MarketData data;
    data.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::system_clock::now().time_since_epoch()).count();
    data.underlying = price_;
    data.bid = price_ * 0.999; 
    data.ask = price_ * 1.001;
    return data;
}

TestOptionDataGenerator::TestOptionDataGenerator() {
    strikes_ = {90, 95, 100, 105, 110, 115, 120};
    maturities_ = {1.0/12.0, 2.0/12.0, 3.0/12.0, 6.0/12.0, 1.0};
}

std::vector<OptionData> TestOptionDataGenerator::generate_option_chain(double spot) {
    std::vector<OptionData> options;
    for (double strike : strikes_) {
        double adjusted_strike = strike * spot / 100.0;
        for (double maturity : maturities_) {
            OptionData put_option;
            put_option.strike = adjusted_strike;
            put_option.maturity = maturity;
            put_option.option_type = OptionType::Put;
            put_option.spot = spot;
            options.push_back(put_option);
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
    if (std::isnan(result.price) || std::isinf(result.price)) return false;
    if (result.price < 0.0) return false;
    if (std::isnan(result.delta) || std::isinf(result.delta)) return false;
    if (std::isnan(result.gamma) || std::isinf(result.gamma)) return false;
    // Gamma can be negative for some exotic options, but for vanilla, usually positive.
    // For simplicity here, not enforcing positive gamma strictly.
    return true;
}

bool TestValidator::validate_volatility_forecast(double forecast, double min_vol, double max_vol) {
    if (std::isnan(forecast) || std::isinf(forecast)) return false;
    return forecast >= min_vol && forecast <= max_vol;
}

// TestReporter class is removed as GTest provides this.

PerformanceBenchmark::PerformanceBenchmark(const std::string& name, size_t iterations)
    : name_(name), iterations_(iterations) {}

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
    std::sort(timings.begin(), timings.end());
    double min_time = timings.front();
    double max_time = timings.back();
    double median = timings[timings.size() / 2];
    double p95 = timings[static_cast<size_t>(timings.size() * 0.95)];
    double p99 = timings[static_cast<size_t>(timings.size() * 0.99)];
    double sum = 0.0;
    for (double t : timings) sum += t;
    double mean = sum / timings.size();
    std::cout << "[BENCHMARK] " << name_ << " Results (ns):" << std::endl;
    std::cout << "  Min:    " << min_time << " ns" << std::endl;
    std::cout << "  Mean:   " << mean << " ns" << std::endl;
    std::cout << "  Median: " << median << " ns" << std::endl;
    std::cout << "  95th:   " << p95 << " ns" << std::endl;
    std::cout << "  99th:   " << p99 << " ns" << std::endl;
    std::cout << "  Max:    " << max_time << " ns" << std::endl;
}

} // namespace test
} // namespace alaris
// src/quantlib/pricing/alo_engine.cpp
#include "alo_engine.h"
#include <ql/time/calendars/unitedstates.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/exercise.hpp>
#include <ql/payoff.hpp>
#include <ql/pricingengines/blackcalculator.hpp>
#include <ql/math/solvers1d/brent.hpp>
#include <cmath>
#include <algorithm>
#include <future>
#include <thread>
#include <functional>

namespace Alaris::Pricing {

QuantLibALOEngine::QuantLibALOEngine(Core::MemoryPool& mem_pool)
    : mem_pool_(mem_pool),
      fp_equation_(QuantLib::QdFpAmericanEngine::Auto) {
    
    // Initialize QuantLib components with deterministic settings
    QuantLib::Settings::instance().evaluationDate() = 
        QuantLib::Date(1, QuantLib::January, 2024);
    
    // Create market data quotes
    underlying_quote_ = QuantLib::ext::make_shared<QuantLib::SimpleQuote>(100.0);
    volatility_quote_ = QuantLib::ext::make_shared<QuantLib::SimpleQuote>(0.20);
    
    // Create term structures
    QuantLib::Calendar calendar = QuantLib::UnitedStates(QuantLib::UnitedStates::Settlement);
    QuantLib::DayCounter day_counter = QuantLib::Actual365Fixed();
    QuantLib::Date evaluation_date = QuantLib::Settings::instance().evaluationDate();
    
    risk_free_rate_ = QuantLib::ext::make_shared<QuantLib::FlatForward>(
        evaluation_date, 0.05, day_counter);
    
    dividend_yield_ = QuantLib::ext::make_shared<QuantLib::FlatForward>(
        evaluation_date, 0.0, day_counter);
    
    vol_surface_ = QuantLib::ext::make_shared<QuantLib::BlackConstantVol>(
        evaluation_date, calendar, 0.20, day_counter);
    
    // Create Black-Scholes process
    process_ = QuantLib::ext::make_shared<QuantLib::GeneralizedBlackScholesProcess>(
        QuantLib::Handle<QuantLib::Quote>(underlying_quote_),
        QuantLib::Handle<QuantLib::YieldTermStructure>(dividend_yield_),
        QuantLib::Handle<QuantLib::YieldTermStructure>(risk_free_rate_),
        QuantLib::Handle<QuantLib::BlackVolTermStructure>(vol_surface_));
    
    // Create iteration scheme and engine
    iteration_scheme_ = QuantLib::QdFpAmericanEngine::accurateScheme();
    engine_ = QuantLib::ext::make_shared<QuantLib::QdFpAmericanEngine>(
        process_, iteration_scheme_, fp_equation_);
}

void QuantLibALOEngine::update_process(const OptionData& option) {
    // Update market data
    underlying_quote_->setValue(option.underlying_price);
    volatility_quote_->setValue(option.volatility);
    
    // Update yield curves
    QuantLib::Calendar calendar = QuantLib::UnitedStates(QuantLib::UnitedStates::Settlement);
    QuantLib::DayCounter day_counter = QuantLib::Actual365Fixed();
    QuantLib::Date evaluation_date = QuantLib::Settings::instance().evaluationDate();
    
    risk_free_rate_ = QuantLib::ext::make_shared<QuantLib::FlatForward>(
        evaluation_date, option.risk_free_rate, day_counter);
    
    dividend_yield_ = QuantLib::ext::make_shared<QuantLib::FlatForward>(
        evaluation_date, option.dividend_yield, day_counter);
    
    vol_surface_ = QuantLib::ext::make_shared<QuantLib::BlackConstantVol>(
        evaluation_date, calendar, option.volatility, day_counter);
    
    // Update process
    process_ = QuantLib::ext::make_shared<QuantLib::GeneralizedBlackScholesProcess>(
        QuantLib::Handle<QuantLib::Quote>(underlying_quote_),
        QuantLib::Handle<QuantLib::YieldTermStructure>(dividend_yield_),
        QuantLib::Handle<QuantLib::YieldTermStructure>(risk_free_rate_),
        QuantLib::Handle<QuantLib::BlackVolTermStructure>(vol_surface_));
    
    // Update engine
    engine_ = QuantLib::ext::make_shared<QuantLib::QdFpAmericanEngine>(
        process_, iteration_scheme_, fp_equation_);
}

QuantLib::ext::shared_ptr<QuantLib::VanillaOption> 
QuantLibALOEngine::create_option(const OptionData& data) {
    // Create payoff
    auto payoff = QuantLib::ext::make_shared<QuantLib::PlainVanillaPayoff>(
        data.option_type, data.strike_price);
    
    // Create exercise
    QuantLib::Date evaluation_date = QuantLib::Settings::instance().evaluationDate();
    QuantLib::Calendar calendar = QuantLib::UnitedStates(QuantLib::UnitedStates::Settlement);
    
    QuantLib::Integer days_to_expiry = 
        static_cast<QuantLib::Integer>(data.time_to_expiry * 365);
    QuantLib::Date expiry_date = calendar.advance(evaluation_date, days_to_expiry, QuantLib::Days);
    
    auto exercise = QuantLib::ext::make_shared<QuantLib::AmericanExercise>(
        evaluation_date, expiry_date);
    
    // Create option
    auto option = QuantLib::ext::make_shared<QuantLib::VanillaOption>(payoff, exercise);
    option->setPricingEngine(engine_);
    
    return option;
}

uint64_t QuantLibALOEngine::calculate_option_hash(const OptionData& data) const {
    // Simple hash combining key parameters
    uint64_t hash = 0;
    hash ^= std::hash<double>{}(data.underlying_price) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
    hash ^= std::hash<double>{}(data.strike_price) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
    hash ^= std::hash<double>{}(data.volatility) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
    hash ^= std::hash<double>{}(data.time_to_expiry) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
    hash ^= std::hash<double>{}(data.risk_free_rate) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
    hash ^= std::hash<int>{}(static_cast<int>(data.option_type)) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
    return hash;
}

QuantLibALOEngine::CachedOption* 
QuantLibALOEngine::find_cached_option(const OptionData& data) const {
    uint64_t hash = calculate_option_hash(data);
    auto it = option_cache_.find(hash);
    if (it != option_cache_.end()) {
        it->second.access_count++;
        cache_hits_.fetch_add(1, std::memory_order_relaxed);
        return &it->second;
    }
    cache_misses_.fetch_add(1, std::memory_order_relaxed);
    return nullptr;
}

void QuantLibALOEngine::cache_option_result(const OptionData& data, 
                                           const OptionGreeks& greeks) const {
    uint64_t hash = calculate_option_hash(data);
    
    // Clean up cache if it gets too large
    if (option_cache_.size() >= MAX_CACHE_SIZE) {
        cleanup_cache();
    }
    
    CachedOption cached;
    cached.data = data;
    cached.greeks = greeks;
    cached.timestamp = std::chrono::duration_cast<std::chrono::microseconds>(
        std::chrono::high_resolution_clock::now().time_since_epoch()).count();
    cached.is_valid = true;
    cached.access_count = 1;
    
    option_cache_[hash] = cached;
}

void QuantLibALOEngine::cleanup_cache() const {
    if (option_cache_.size() < MAX_CACHE_SIZE / 2) return;
    
    // Remove least recently used entries
    std::vector<std::pair<uint64_t, uint64_t>> entries; // hash, timestamp
    for (const auto& pair : option_cache_) {
        entries.emplace_back(pair.first, pair.second.timestamp);
    }
    
    std::sort(entries.begin(), entries.end(), 
        [](const auto& a, const auto& b) { return a.second < b.second; });
    
    // Remove oldest 25% of entries
    size_t to_remove = entries.size() / 4;
    for (size_t i = 0; i < to_remove; ++i) {
        option_cache_.erase(entries[i].first);
    }
}

double QuantLibALOEngine::calculate_price_bump(const OptionData& option, 
                                              const std::string& param, 
                                              double bump_size) const {
    OptionData bumped_option = option;
    
    if (param == "spot") {
        bumped_option.underlying_price += bump_size;
    } else if (param == "vol") {
        bumped_option.volatility += bump_size;
    } else if (param == "time") {
        bumped_option.time_to_expiry += bump_size;
    } else if (param == "rate") {
        bumped_option.risk_free_rate += bump_size;
    }
    
    // Create temporary engine for bumped calculation
    QuantLibALOEngine* non_const_this = const_cast<QuantLibALOEngine*>(this);
    non_const_this->update_process(bumped_option);
    auto option_obj = non_const_this->create_option(bumped_option);
    
    double price = option_obj->NPV();
    
    // Restore original state
    non_const_this->update_process(option);
    
    return price;
}

OptionGreeks QuantLibALOEngine::calculate_numerical_greeks(const OptionData& option) const {
    OptionGreeks greeks;
    
    // Calculate base price
    update_process(option);
    auto option_obj = create_option(option);
    greeks.price = option_obj->NPV();
    
    if (!std::isfinite(greeks.price) || greeks.price < 0) {
        return calculate_black_scholes_greeks(option);
    }
    
    try {
        // Delta: ∂V/∂S
        double spot_bump = option.underlying_price * bump_sizes_.spot_bump;
        double price_up = calculate_price_bump(option, "spot", spot_bump);
        double price_down = calculate_price_bump(option, "spot", -spot_bump);
        greeks.delta = (price_up - price_down) / (2.0 * spot_bump);
        
        // Gamma: ∂²V/∂S²
        greeks.gamma = (price_up - 2.0 * greeks.price + price_down) / (spot_bump * spot_bump);
        
        // Vega: ∂V/∂σ
        double vol_up = calculate_price_bump(option, "vol", bump_sizes_.vol_bump);
        double vol_down = calculate_price_bump(option, "vol", -bump_sizes_.vol_bump);
        greeks.vega = (vol_up - vol_down) / (2.0 * bump_sizes_.vol_bump);
        
        // Theta: -∂V/∂t
        double time_up = calculate_price_bump(option, "time", bump_sizes_.time_bump);
        greeks.theta = -(time_up - greeks.price) / bump_sizes_.time_bump;
        
        // Rho: ∂V/∂r
        double rate_up = calculate_price_bump(option, "rate", bump_sizes_.rate_bump);
        double rate_down = calculate_price_bump(option, "rate", -bump_sizes_.rate_bump);
        greeks.rho = (rate_up - rate_down) / (2.0 * bump_sizes_.rate_bump);
        
        // Higher-order Greeks for sophisticated strategies
        
        // Vanna: ∂²V/∂S∂σ
        OptionData spot_vol_up = option;
        spot_vol_up.underlying_price += spot_bump;
        spot_vol_up.volatility += bump_sizes_.vol_bump;
        double price_spot_vol_up = calculate_price_bump(spot_vol_up, "spot", 0);
        
        OptionData spot_vol_down = option;
        spot_vol_down.underlying_price += spot_bump;
        spot_vol_down.volatility -= bump_sizes_.vol_bump;
        double price_spot_vol_down = calculate_price_bump(spot_vol_down, "spot", 0);
        
        double delta_vol_up = (price_spot_vol_up - vol_up) / spot_bump;
        double delta_vol_down = (price_spot_vol_down - vol_down) / spot_bump;
        greeks.vanna = (delta_vol_up - delta_vol_down) / (2.0 * bump_sizes_.vol_bump);
        
        // Volga: ∂²V/∂σ²
        greeks.volga = (vol_up - 2.0 * greeks.price + vol_down) / 
                      (bump_sizes_.vol_bump * bump_sizes_.vol_bump);
        
        // Charm: ∂Δ/∂t
        OptionData time_bumped = option;
        time_bumped.time_to_expiry += bump_sizes_.time_bump;
        double price_time_spot_up = calculate_price_bump(time_bumped, "spot", spot_bump);
        double price_time_spot_down = calculate_price_bump(time_bumped, "spot", -spot_bump);
        double delta_time_up = (price_time_spot_up - price_time_spot_down) / (2.0 * spot_bump);
        greeks.charm = (delta_time_up - greeks.delta) / bump_sizes_.time_bump;
        
        // Veta: ∂ν/∂t
        double vega_time_up = calculate_price_bump(time_bumped, "vol", bump_sizes_.vol_bump) - 
                             calculate_price_bump(time_bumped, "vol", -bump_sizes_.vol_bump);
        vega_time_up /= (2.0 * bump_sizes_.vol_bump);
        greeks.veta = (vega_time_up - greeks.vega) / bump_sizes_.time_bump;
        
    } catch (const std::exception& e) {
        // Fallback to Black-Scholes approximation
        return calculate_black_scholes_greeks(option);
    }
    
    // Validate Greeks
    if (!validate_greeks(greeks, option)) {
        return calculate_black_scholes_greeks(option);
    }
    
    return greeks;
}

bool QuantLibALOEngine::validate_greeks(const OptionGreeks& greeks, 
                                       const OptionData& option) const {
    // Basic sanity checks
    if (!std::isfinite(greeks.delta) || !std::isfinite(greeks.gamma) || 
        !std::isfinite(greeks.theta) || !std::isfinite(greeks.vega) || 
        !std::isfinite(greeks.rho)) {
        return false;
    }
    
    // Delta bounds
    if (option.option_type == QuantLib::Option::Call) {
        if (greeks.delta < -0.1 || greeks.delta > 1.1) return false;
    } else {
        if (greeks.delta < -1.1 || greeks.delta > 0.1) return false;
    }
    
    // Gamma should be non-negative for vanilla options
    if (greeks.gamma < -0.01) return false;
    
    // Vega should be non-negative
    if (greeks.vega < -0.01) return false;
    
    // Theta bounds (should be negative for long options most of the time)
    if (std::abs(greeks.theta) > option.underlying_price) return false;
    
    return true;
}

OptionGreeks QuantLibALOEngine::calculate_black_scholes_greeks(const OptionData& option_data) const {
    OptionGreeks greeks;
    
    double S = option_data.underlying_price;
    double K = option_data.strike_price;
    double T = option_data.time_to_expiry;
    double r = option_data.risk_free_rate;
    double q = option_data.dividend_yield;
    double sigma = option_data.volatility;
    
    if (T <= 0 || S <= 0 || K <= 0 || sigma <= 0) {
        return greeks;
    }
    
    double d1 = (std::log(S/K) + (r - q + 0.5*sigma*sigma)*T) / (sigma*std::sqrt(T));
    double d2 = d1 - sigma*std::sqrt(T);
    
    auto norm_cdf = [](double x) {
        return 0.5 * (1.0 + std::erf(x / std::sqrt(2.0)));
    };
    
    auto norm_pdf = [](double x) {
        return std::exp(-0.5 * x * x) / std::sqrt(2.0 * M_PI);
    };
    
    if (option_data.option_type == QuantLib::Option::Call) {
        greeks.price = S * std::exp(-q*T) * norm_cdf(d1) - K * std::exp(-r*T) * norm_cdf(d2);
        greeks.delta = std::exp(-q*T) * norm_cdf(d1);
        greeks.rho = K * T * std::exp(-r*T) * norm_cdf(d2);
    } else {
        greeks.price = K * std::exp(-r*T) * norm_cdf(-d2) - S * std::exp(-q*T) * norm_cdf(-d1);
        greeks.delta = -std::exp(-q*T) * norm_cdf(-d1);
        greeks.rho = -K * T * std::exp(-r*T) * norm_cdf(-d2);
    }
    
    greeks.gamma = std::exp(-q*T) * norm_pdf(d1) / (S * sigma * std::sqrt(T));
    greeks.theta = -(S * norm_pdf(d1) * sigma * std::exp(-q*T)) / (2*std::sqrt(T)) 
                   - r * K * std::exp(-r*T) * norm_cdf(d2) 
                   + q * S * std::exp(-q*T) * norm_cdf(d1);
    greeks.vega = S * std::exp(-q*T) * norm_pdf(d1) * std::sqrt(T);
    
    // Higher-order Greeks
    greeks.vanna = -std::exp(-q*T) * norm_pdf(d1) * d2 / sigma;
    greeks.volga = S * std::exp(-q*T) * norm_pdf(d1) * std::sqrt(T) * d1 * d2 / sigma;
    greeks.charm = -std::exp(-q*T) * norm_pdf(d1) * 
                   (2*(r-q)*T - d2*sigma*std::sqrt(T)) / (2*T*sigma*std::sqrt(T));
    
    return greeks;
}

double QuantLibALOEngine::calculate_option_price(const OptionData& option_data) {
    total_calculations_.fetch_add(1, std::memory_order_relaxed);
    
    // Check cache first
    CachedOption* cached = find_cached_option(option_data);
    if (cached && cached->is_valid) {
        return cached->greeks.price;
    }
    
    // Calculate price
    update_process(option_data);
    auto option = create_option(option_data);
    double price = option->NPV();
    
    // Cache just the price (full Greeks calculation is more expensive)
    OptionGreeks simple_greeks;
    simple_greeks.price = price;
    cache_option_result(option_data, simple_greeks);
    
    return price;
}

OptionGreeks QuantLibALOEngine::calculate_greeks(const OptionData& option_data) {
    total_calculations_.fetch_add(1, std::memory_order_relaxed);
    
    // Check cache first
    CachedOption* cached = find_cached_option(option_data);
    if (cached && cached->is_valid && cached->greeks.delta != 0.0) {
        return cached->greeks;
    }
    
    // Calculate Greeks numerically
    OptionGreeks greeks = calculate_numerical_greeks(option_data);
    cache_option_result(option_data, greeks);
    
    return greeks;
}

void QuantLibALOEngine::batch_calculate_prices(const std::vector<OptionData>& options,
                                              std::vector<double>& results) {
    results.resize(options.size());
    
    // Use parallel execution for large batches
    if (options.size() > 10) {
        const size_t num_threads = std::min(static_cast<size_t>(std::thread::hardware_concurrency()), 
                                           options.size());
        const size_t chunk_size = options.size() / num_threads;
        
        std::vector<std::future<void>> futures;
        
        for (size_t t = 0; t < num_threads; ++t) {
            size_t start = t * chunk_size;
            size_t end = (t == num_threads - 1) ? options.size() : (t + 1) * chunk_size;
            
            futures.emplace_back(std::async(std::launch::async, [this, &options, &results, start, end]() {
                for (size_t i = start; i < end; ++i) {
                    results[i] = calculate_option_price(options[i]);
                }
            }));
        }
        
        for (auto& future : futures) {
            future.wait();
        }
    } else {
        for (size_t i = 0; i < options.size(); ++i) {
            results[i] = calculate_option_price(options[i]);
        }
    }
}

void QuantLibALOEngine::batch_calculate_greeks(const std::vector<OptionData>& options,
                                              std::vector<OptionGreeks>& results) {
    results.resize(options.size());
    
    // Greeks calculation is more expensive, so use parallel execution for smaller batches
    if (options.size() > 4) {
        const size_t num_threads = std::min(static_cast<size_t>(std::thread::hardware_concurrency()), 
                                           options.size());
        const size_t chunk_size = options.size() / num_threads;
        
        std::vector<std::future<void>> futures;
        
        for (size_t t = 0; t < num_threads; ++t) {
            size_t start = t * chunk_size;
            size_t end = (t == num_threads - 1) ? options.size() : (t + 1) * chunk_size;
            
            futures.emplace_back(std::async(std::launch::async, [this, &options, &results, start, end]() {
                for (size_t i = start; i < end; ++i) {
                    results[i] = calculate_greeks(options[i]);
                }
            }));
        }
        
        for (auto& future : futures) {
            future.wait();
        }
    } else {
        for (size_t i = 0; i < options.size(); ++i) {
            results[i] = calculate_greeks(options[i]);
        }
    }
}

double QuantLibALOEngine::calculate_implied_volatility(const OptionData& option, 
                                                      double market_price,
                                                      double accuracy, 
                                                      size_t max_iterations) {
    class ImpliedVolObjective {
    public:
        ImpliedVolObjective(QuantLibALOEngine* engine, const OptionData& option, double target_price)
            : engine_(engine), option_(option), target_price_(target_price) {}
        
        double operator()(double vol) const {
            OptionData temp_option = option_;
            temp_option.volatility = vol;
            return engine_->calculate_option_price(temp_option) - target_price_;
        }
        
    private:
        QuantLibALOEngine* engine_;
        OptionData option_;
        double target_price_;
    };
    
    try {
        ImpliedVolObjective objective(this, option, market_price);
        QuantLib::Brent solver;
        solver.setMaxEvaluations(max_iterations);
        
        // Search bounds: 1% to 500% volatility
        return solver.solve(objective, accuracy, 0.20, 0.01, 5.0);
        
    } catch (const std::exception& e) {
        // Return a reasonable default if implied vol calculation fails
        return 0.20;
    }
}

double QuantLibALOEngine::calculate_portfolio_delta(
    const std::vector<std::pair<OptionData, double>>& positions) {
    double total_delta = 0.0;
    
    for (const auto& position : positions) {
        OptionGreeks greeks = calculate_greeks(position.first);
        total_delta += greeks.delta * position.second;
    }
    
    return total_delta;
}

double QuantLibALOEngine::calculate_portfolio_gamma(
    const std::vector<std::pair<OptionData, double>>& positions) {
    double total_gamma = 0.0;
    
    for (const auto& position : positions) {
        OptionGreeks greeks = calculate_greeks(position.first);
        total_gamma += greeks.gamma * position.second;
    }
    
    return total_gamma;
}

double QuantLibALOEngine::calculate_portfolio_vega(
    const std::vector<std::pair<OptionData, double>>& positions) {
    double total_vega = 0.0;
    
    for (const auto& position : positions) {
        OptionGreeks greeks = calculate_greeks(position.first);
        total_vega += greeks.vega * position.second;
    }
    
    return total_vega;
}

void QuantLibALOEngine::set_fixed_point_equation(QuantLib::QdFpAmericanEngine::FixedPointEquation equation) {
    fp_equation_ = equation;
    
    // Recreate engine with new equation
    engine_ = QuantLib::ext::make_shared<QuantLib::QdFpAmericanEngine>(
        process_, iteration_scheme_, fp_equation_);
}

void QuantLibALOEngine::set_iteration_scheme(QuantLib::ext::shared_ptr<QuantLib::QdFpIterationScheme> scheme) {
    iteration_scheme_ = scheme;
    
    // Recreate engine with new scheme
    engine_ = QuantLib::ext::make_shared<QuantLib::QdFpAmericanEngine>(
        process_, iteration_scheme_, fp_equation_);
}

void QuantLibALOEngine::set_bump_sizes(double spot_bump, double vol_bump, 
                                      double time_bump, double rate_bump) {
    bump_sizes_.spot_bump = spot_bump;
    bump_sizes_.vol_bump = vol_bump;
    bump_sizes_.time_bump = time_bump;
    bump_sizes_.rate_bump = rate_bump;
}

void QuantLibALOEngine::clear_cache() {
    option_cache_.clear();
    cache_access_count_ = 0;
}

void QuantLibALOEngine::warm_up_cache(const std::vector<OptionData>& typical_options) {
    for (const auto& option : typical_options) {
        calculate_greeks(option);  // This will populate the cache
    }
}

QuantLibALOEngine::PerformanceStats QuantLibALOEngine::get_performance_stats() const {
    PerformanceStats stats;
    stats.total_calculations = total_calculations_.load();
    stats.cache_hits = cache_hits_.load();
    stats.cache_misses = cache_misses_.load();
    stats.cache_hit_ratio = (stats.cache_hits + stats.cache_misses > 0) ? 
                           static_cast<double>(stats.cache_hits) / (stats.cache_hits + stats.cache_misses) : 0.0;
    stats.cache_size = option_cache_.size();
    return stats;
}

void QuantLibALOEngine::reset_performance_stats() {
    total_calculations_.store(0);
    cache_hits_.store(0);
    cache_misses_.store(0);
}

} // namespace Alaris::Pricing
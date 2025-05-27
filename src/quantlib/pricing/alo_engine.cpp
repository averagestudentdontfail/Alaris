#include "alo_engine.h"
#include <ql/time/calendars/unitedstates.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/exercise.hpp>
#include <ql/payoff.hpp>
#include <cmath>
#include <algorithm>

namespace Alaris::Pricing {

QuantLibALOEngine::QuantLibALOEngine(Core::MemoryPool& mem_pool)
    : mem_pool_(mem_pool),
      fp_equation_(QuantLib::QdFpAmericanEngine::Auto),
      time_steps_(800),
      asset_steps_(800),
      cache_index_(0) {
    
    // Initialize QuantLib components with deterministic settings
    QuantLib::Settings::instance().evaluationDate() = 
        QuantLib::Date(1, QuantLib::January, 2024);
    
    // Create market data quotes
    underlying_quote_ = QuantLib::ext::make_shared<QuantLib::SimpleQuote>(100.0);
    volatility_quote_ = QuantLib::ext::make_shared<QuantLib::SimpleQuote>(0.20);
    
    // Create term structures with correct calendar usage
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
    
    // Create iteration scheme using static methods
    iteration_scheme_ = QuantLib::QdFpAmericanEngine::accurateScheme();
    
    // Create ALO engine with correct constructor
    engine_ = QuantLib::ext::make_shared<QuantLib::QdFpAmericanEngine>(
        process_, iteration_scheme_, fp_equation_);
    
    // Initialize cache
    option_cache_.resize(CACHE_SIZE);
    for (auto& cached : option_cache_) {
        cached.is_valid = false;
    }
}

void QuantLibALOEngine::update_process(const OptionData& option) {
    // Update market data
    underlying_quote_->setValue(option.underlying_price);
    volatility_quote_->setValue(option.volatility);
    
    // Update yield curves if needed
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
    
    // Create exercise with correct calendar
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

double QuantLibALOEngine::calculate_option_price(const OptionData& option_data) {
    // Check cache first
    CachedOption* cached = find_cached_option(option_data);
    if (cached && cached->is_valid) {
        return cached->greeks.price;
    }
    
    // Update process with new parameters
    update_process(option_data);
    
    // Create and price option
    auto option = create_option(option_data);
    double price = option->NPV();
    
    // Cache result
    OptionGreeks greeks;
    greeks.price = price;
    cache_option_result(option_data, greeks);
    
    return price;
}

OptionGreeks QuantLibALOEngine::calculate_greeks(const OptionData& option_data) {
    // Check cache first
    CachedOption* cached = find_cached_option(option_data);
    if (cached && cached->is_valid) {
        return cached->greeks;
    }
    
    // Update process with new parameters
    update_process(option_data);
    
    // Create option
    auto option = create_option(option_data);
    
    // Calculate greeks with proper error handling
    OptionGreeks greeks;
    
    try {
        // First calculate NPV (this is required for Greeks)
        greeks.price = option->NPV();
        
        // Check if price calculation succeeded
        if (!std::isfinite(greeks.price) || greeks.price < 0) {
            throw std::runtime_error("Invalid option price calculated");
        }
        
        // Calculate Greeks with individual error handling
        try {
            greeks.delta = option->delta();
        } catch (const std::exception& e) {
            // If delta calculation fails, use finite difference approximation
            greeks.delta = calculate_finite_difference_delta(option_data);
        }
        
        try {
            greeks.gamma = option->gamma();
        } catch (const std::exception& e) {
            greeks.gamma = 0.0; // Fallback
        }
        
        try {
            greeks.theta = option->theta();
        } catch (const std::exception& e) {
            greeks.theta = 0.0; // Fallback
        }
        
        try {
            greeks.vega = option->vega();
        } catch (const std::exception& e) {
            greeks.vega = 0.0; // Fallback
        }
        
        try {
            greeks.rho = option->rho();
        } catch (const std::exception& e) {
            greeks.rho = 0.0; // Fallback
        }
        
    } catch (const std::exception& e) {
        // If all else fails, use Black-Scholes approximation
        greeks = calculate_black_scholes_greeks(option_data);
    }
    
    // Cache result
    cache_option_result(option_data, greeks);
    
    return greeks;
}

double QuantLibALOEngine::calculate_finite_difference_delta(const OptionData& option_data) {
    const double bump = 0.01; // 1% bump
    
    OptionData up_data = option_data;
    up_data.underlying_price *= (1.0 + bump);
    
    OptionData down_data = option_data;
    down_data.underlying_price *= (1.0 - bump);
    
    try {
        double up_price = calculate_option_price(up_data);
        double down_price = calculate_option_price(down_data);
        return (up_price - down_price) / (2.0 * option_data.underlying_price * bump);
    } catch (...) {
        return 0.5; // Default delta for at-the-money option
    }
}

OptionGreeks QuantLibALOEngine::calculate_black_scholes_greeks(const OptionData& option_data) {
    // Simplified Black-Scholes Greeks as fallback
    OptionGreeks greeks;
    
    double S = option_data.underlying_price;
    double K = option_data.strike_price;
    double T = option_data.time_to_expiry;
    double r = option_data.risk_free_rate;
    double q = option_data.dividend_yield;
    double sigma = option_data.volatility;
    
    if (T <= 0 || S <= 0 || K <= 0 || sigma <= 0) {
        // Invalid parameters, return zeros
        return greeks;
    }
    
    double d1 = (std::log(S/K) + (r - q + 0.5*sigma*sigma)*T) / (sigma*std::sqrt(T));
    double d2 = d1 - sigma*std::sqrt(T);
    
    // Standard normal CDF approximation
    auto norm_cdf = [](double x) {
        return 0.5 * (1.0 + std::erf(x / std::sqrt(2.0)));
    };
    
    auto norm_pdf = [](double x) {
        return std::exp(-0.5 * x * x) / std::sqrt(2.0 * M_PI);
    };
    
    if (option_data.option_type == QuantLib::Option::Call) {
        greeks.price = S * std::exp(-q*T) * norm_cdf(d1) - K * std::exp(-r*T) * norm_cdf(d2);
        greeks.delta = std::exp(-q*T) * norm_cdf(d1);
    } else {
        greeks.price = K * std::exp(-r*T) * norm_cdf(-d2) - S * std::exp(-q*T) * norm_cdf(-d1);
        greeks.delta = -std::exp(-q*T) * norm_cdf(-d1);
    }
    
    greeks.gamma = std::exp(-q*T) * norm_pdf(d1) / (S * sigma * std::sqrt(T));
    greeks.theta = -(S * norm_pdf(d1) * sigma * std::exp(-q*T)) / (2*std::sqrt(T)) 
                   - r * K * std::exp(-r*T) * norm_cdf(d2) 
                   + q * S * std::exp(-q*T) * norm_cdf(d1);
    greeks.vega = S * std::exp(-q*T) * norm_pdf(d1) * std::sqrt(T);
    greeks.rho = K * T * std::exp(-r*T) * norm_cdf(d2);
    
    return greeks;
}

void QuantLibALOEngine::batch_calculate_prices(const std::vector<OptionData>& options,
                                              std::vector<double>& results) {
    results.resize(options.size());
    
    for (size_t i = 0; i < options.size(); ++i) {
        results[i] = calculate_option_price(options[i]);
    }
}

void QuantLibALOEngine::batch_calculate_greeks(const std::vector<OptionData>& options,
                                              std::vector<OptionGreeks>& results) {
    results.resize(options.size());
    
    for (size_t i = 0; i < options.size(); ++i) {
        results[i] = calculate_greeks(options[i]);
    }
}

QuantLibALOEngine::CachedOption* 
QuantLibALOEngine::find_cached_option(const OptionData& data) const {
    // Simple linear search for now - could be optimized with hash table
    for (auto& cached : option_cache_) {
        if (cached.is_valid &&
            std::abs(cached.data.underlying_price - data.underlying_price) < 1e-6 &&
            std::abs(cached.data.strike_price - data.strike_price) < 1e-6 &&
            std::abs(cached.data.volatility - data.volatility) < 1e-6 &&
            std::abs(cached.data.time_to_expiry - data.time_to_expiry) < 1e-6 &&
            cached.data.option_type == data.option_type) {
            return &cached;
        }
    }
    return nullptr;
}

void QuantLibALOEngine::cache_option_result(const OptionData& data, 
                                           const OptionGreeks& greeks) const {
    auto& cached = option_cache_[cache_index_];
    cached.data = data;
    cached.greeks = greeks;
    cached.is_valid = true;
    cached.timestamp = std::chrono::duration_cast<std::chrono::microseconds>(
        std::chrono::high_resolution_clock::now().time_since_epoch()).count();
    
    cache_index_ = (cache_index_ + 1) % CACHE_SIZE;
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

void QuantLibALOEngine::clear_cache() {
    for (auto& cached : option_cache_) {
        cached.is_valid = false;
    }
    cache_index_ = 0;
}

size_t QuantLibALOEngine::cache_hit_count() const {
    return 0; // Simplified for now
}

size_t QuantLibALOEngine::cache_miss_count() const {
    return 0; // Simplified for now
}

double QuantLibALOEngine::cache_hit_ratio() const {
    return 0.0; // Simplified for now
}

} // namespace Alaris::Pricing
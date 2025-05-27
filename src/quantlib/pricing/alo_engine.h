// src/quantlib/pricing/alo_engine.h
#pragma once

#include <ql/quantlib.hpp>
#include "../core/memory_pool.h"
#include <vector>
#include <memory>
#include <unordered_map>

namespace Alaris::Pricing {

struct OptionData {
    double underlying_price;
    double strike_price;
    double risk_free_rate;
    double dividend_yield;
    double volatility;
    double time_to_expiry;
    QuantLib::Option::Type option_type;
    uint32_t symbol_id;
    
    OptionData() : underlying_price(0), strike_price(0), risk_free_rate(0),
                  dividend_yield(0), volatility(0), time_to_expiry(0),
                  option_type(QuantLib::Option::Call), symbol_id(0) {}
};

struct OptionGreeks {
    double delta;
    double gamma;
    double theta;
    double vega;
    double rho;
    double price;
    
    // Additional Greeks for sophisticated strategies
    double vanna;      // d²V/dS/dσ
    double volga;      // d²V/dσ²
    double charm;      // dΔ/dt
    double veta;       // dν/dt
    
    OptionGreeks() : delta(0), gamma(0), theta(0), vega(0), rho(0), price(0),
                    vanna(0), volga(0), charm(0), veta(0) {}
};

// Enhanced ALO Engine with numerical Greeks calculation
class QuantLibALOEngine {
private:
    // Memory management
    Core::MemoryPool& mem_pool_;

    // QuantLib components
    QuantLib::ext::shared_ptr<QuantLib::QdFpAmericanEngine> engine_;
    QuantLib::ext::shared_ptr<QuantLib::GeneralizedBlackScholesProcess> process_;
    QuantLib::ext::shared_ptr<QuantLib::SimpleQuote> underlying_quote_;
    QuantLib::ext::shared_ptr<QuantLib::SimpleQuote> volatility_quote_;
    QuantLib::ext::shared_ptr<QuantLib::YieldTermStructure> risk_free_rate_;
    QuantLib::ext::shared_ptr<QuantLib::YieldTermStructure> dividend_yield_;
    QuantLib::ext::shared_ptr<QuantLib::BlackVolTermStructure> vol_surface_;
    
    // Engine configuration
    QuantLib::QdFpAmericanEngine::FixedPointEquation fp_equation_;
    QuantLib::ext::shared_ptr<QuantLib::QdFpIterationScheme> iteration_scheme_;
    
    // Numerical differentiation parameters
    struct BumpSizes {
        double spot_bump = 0.01;        // 1% for delta/gamma
        double vol_bump = 0.001;        // 0.1% for vega/volga/vanna
        double time_bump = 1.0/365.0;   // 1 day for theta/charm/veta
        double rate_bump = 0.0001;      // 1bp for rho
    } bump_sizes_;
    
    // Cache for repeated calculations
    struct CachedOption {
        OptionData data;
        OptionGreeks greeks;
        uint64_t timestamp;
        bool is_valid;
        uint32_t access_count;
    };
    
    mutable std::unordered_map<uint64_t, CachedOption> option_cache_;
    static constexpr size_t MAX_CACHE_SIZE = 2048;
    mutable size_t cache_access_count_ = 0;
    
    // Performance tracking
    mutable std::atomic<size_t> total_calculations_{0};
    mutable std::atomic<size_t> cache_hits_{0};
    mutable std::atomic<size_t> cache_misses_{0};
    
    // Helper methods
    void update_process(const OptionData& option);
    QuantLib::ext::shared_ptr<QuantLib::VanillaOption> create_option(const OptionData& data);
    uint64_t calculate_option_hash(const OptionData& data) const;
    CachedOption* find_cached_option(const OptionData& data) const;
    void cache_option_result(const OptionData& data, const OptionGreeks& greeks) const;
    void cleanup_cache() const;
    
    // Numerical Greeks calculation methods
    double calculate_price_bump(const OptionData& option, 
                               const std::string& param, double bump_size) const;
    OptionGreeks calculate_numerical_greeks(const OptionData& option) const;
    
    // Black-Scholes fallback for validation
    OptionGreeks calculate_black_scholes_greeks(const OptionData& option_data) const;
    bool validate_greeks(const OptionGreeks& greeks, const OptionData& option) const;

public:
    explicit QuantLibALOEngine(Core::MemoryPool& mem_pool);
    ~QuantLibALOEngine() = default;
    
    // Non-copyable
    QuantLibALOEngine(const QuantLibALOEngine&) = delete;
    QuantLibALOEngine& operator=(const QuantLibALOEngine&) = delete;
    
    // Core pricing functions
    double calculate_option_price(const OptionData& option);
    OptionGreeks calculate_greeks(const OptionData& option);
    
    // Batch processing with parallel execution
    void batch_calculate_prices(const std::vector<OptionData>& options, 
                               std::vector<double>& results);
    void batch_calculate_greeks(const std::vector<OptionData>& options,
                               std::vector<OptionGreeks>& results);
    
    // Advanced pricing methods for strategy use
    double calculate_implied_volatility(const OptionData& option, double market_price,
                                       double accuracy = 1e-6, size_t max_iterations = 100);
    
    // Risk calculations
    double calculate_portfolio_delta(const std::vector<std::pair<OptionData, double>>& positions);
    double calculate_portfolio_gamma(const std::vector<std::pair<OptionData, double>>& positions);
    double calculate_portfolio_vega(const std::vector<std::pair<OptionData, double>>& positions);
    
    // Engine configuration
    void set_fixed_point_equation(QuantLib::QdFpAmericanEngine::FixedPointEquation equation);
    void set_iteration_scheme(QuantLib::ext::shared_ptr<QuantLib::QdFpIterationScheme> scheme);
    void set_bump_sizes(double spot_bump, double vol_bump, double time_bump, double rate_bump);
    
    // Performance and diagnostics
    void clear_cache();
    void warm_up_cache(const std::vector<OptionData>& typical_options);
    
    // Statistics
    struct PerformanceStats {
        size_t total_calculations;
        size_t cache_hits;
        size_t cache_misses;
        double cache_hit_ratio;
        size_t cache_size;
    };
    
    PerformanceStats get_performance_stats() const;
    void reset_performance_stats();
};

} // namespace Alaris::Pricing
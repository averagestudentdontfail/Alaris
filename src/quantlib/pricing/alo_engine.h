#pragma once

#include <ql/quantlib.hpp>
#include "../core/memory_pool.h"
#include <vector>
#include <memory>

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
    
    OptionGreeks() : delta(0), gamma(0), theta(0), vega(0), rho(0), price(0) {}
};

class QuantLibALOEngine {
private:
    // QuantLib components - using QdFpAmericanEngine correctly
    QuantLib::ext::shared_ptr<QuantLib::QdFpAmericanEngine> engine_;
    QuantLib::ext::shared_ptr<QuantLib::GeneralizedBlackScholesProcess> process_;
    QuantLib::ext::shared_ptr<QuantLib::SimpleQuote> underlying_quote_;
    QuantLib::ext::shared_ptr<QuantLib::SimpleQuote> volatility_quote_;
    QuantLib::ext::shared_ptr<QuantLib::YieldTermStructure> risk_free_rate_;
    QuantLib::ext::shared_ptr<QuantLib::YieldTermStructure> dividend_yield_;
    QuantLib::ext::shared_ptr<QuantLib::BlackVolTermStructure> vol_surface_;
    
    // Engine configuration - using correct enum
    QuantLib::QdFpAmericanEngine::FixedPointEquation fp_equation_;
    QuantLib::ext::shared_ptr<QuantLib::QdFpIterationScheme> iteration_scheme_;
    QuantLib::Size time_steps_;
    QuantLib::Size asset_steps_;
    
    // Memory management for deterministic execution
    Core::MemoryPool& mem_pool_;
    
    // Cache for repeated calculations
    struct CachedOption {
        OptionData data;
        OptionGreeks greeks;
        uint64_t timestamp;
        bool is_valid;
    };
    
    mutable std::vector<CachedOption> option_cache_;
    static constexpr size_t CACHE_SIZE = 1024;
    mutable size_t cache_index_;
    
    // Helper methods
    void update_process(const OptionData& option);
    QuantLib::ext::shared_ptr<QuantLib::VanillaOption> create_option(const OptionData& data);
    CachedOption* find_cached_option(const OptionData& data) const;
    void cache_option_result(const OptionData& data, const OptionGreeks& greeks) const;
    
public:
    explicit QuantLibALOEngine(Core::MemoryPool& mem_pool);
    ~QuantLibALOEngine() = default;
    
    // Non-copyable
    QuantLibALOEngine(const QuantLibALOEngine&) = delete;
    QuantLibALOEngine& operator=(const QuantLibALOEngine&) = delete;
    
    // Core pricing functions using QuantLib ALO algorithm
    double calculate_option_price(const OptionData& option);
    OptionGreeks calculate_greeks(const OptionData& option);
    
    // Batch processing for multiple options
    void batch_calculate_prices(const std::vector<OptionData>& options, 
                               std::vector<double>& results);
    void batch_calculate_greeks(const std::vector<OptionData>& options,
                               std::vector<OptionGreeks>& results);
    
    // Engine configuration using correct API
    void set_fixed_point_equation(QuantLib::QdFpAmericanEngine::FixedPointEquation equation);
    void set_iteration_scheme(QuantLib::ext::shared_ptr<QuantLib::QdFpIterationScheme> scheme);
    
    // Performance optimization
    void warm_up_cache(const std::vector<OptionData>& typical_options);
    void clear_cache();
    
    // Diagnostics
    size_t cache_hit_count() const;
    size_t cache_miss_count() const;
    double cache_hit_ratio() const;
};

} // namespace Alaris::Pricing

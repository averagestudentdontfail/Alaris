#pragma once

#include <ql/quantlib.hpp>
#include "../core/memory_pool.h"
#include <vector>
#include <deque>
#include <memory>

namespace Alaris::Volatility {

class QuantLibGJRGARCHModel {
private:
    // Memory management - initialized first
    Core::MemoryPool& mem_pool_;

    // Model parameters (specific to GJR-GARCH(1,1) for simplicity)
    QuantLib::Real omega_;    // Constant term
    QuantLib::Real alpha_;    // ARCH parameter
    QuantLib::Real beta_;     // GARCH parameter
    QuantLib::Real gamma_;    // Asymmetry parameter

    // Historical data and state
    std::deque<QuantLib::Real> returns_;
    std::deque<QuantLib::Real> squared_returns_;
    std::deque<QuantLib::Real> conditional_variances_;
    std::deque<bool> negative_returns_;

    QuantLib::Real current_variance_;
    QuantLib::Real current_volatility_;

    // Model configuration
    QuantLib::Size max_history_length_;
    QuantLib::Real tolerance_;
    QuantLib::Size max_iterations_;

    // Performance tracking
    mutable size_t forecast_count_;

    // Helper methods
    void initialize_parameters();
    void update_variance_series();
    bool is_stationary() const;

public:
    explicit QuantLibGJRGARCHModel(Core::MemoryPool& mem_pool);
    ~QuantLibGJRGARCHModel() = default;

    // Non-copyable
    QuantLibGJRGARCHModel(const QuantLibGJRGARCHModel&) = delete;
    QuantLibGJRGARCHModel& operator=(const QuantLibGJRGARCHModel&) = delete;

    // Initialize model with parameters
    void set_parameters(QuantLib::Real omega, QuantLib::Real alpha,
                        QuantLib::Real beta, QuantLib::Real gamma);

    // Update model with new market data
    void update(QuantLib::Real new_return);
    void update_batch(const std::vector<QuantLib::Real>& returns_batch);

    // Generate volatility forecast using GJR-GARCH
    QuantLib::Real forecast_volatility(QuantLib::Size horizon = 1);
    std::vector<QuantLib::Real> forecast_volatility_path(QuantLib::Size horizon);

    // Calibrate model to historical data
    bool calibrate(const std::vector<QuantLib::Real>& historical_returns);

    // Model diagnostics and validation
    QuantLib::Real log_likelihood() const;
    std::vector<QuantLib::Real> get_parameters() const;
    bool is_model_valid() const;

    // Configuration
    void set_max_history_length(QuantLib::Size length);
    void set_calibration_parameters(QuantLib::Real tolerance, QuantLib::Size max_iterations);

    // Current state accessors
    QuantLib::Real current_volatility() const { return current_volatility_; }
    QuantLib::Real current_variance() const { return current_variance_; }
    QuantLib::Size sample_size() const { return returns_.size(); }
    
    // Data access - ADDED to fix compilation error
    const std::deque<QuantLib::Real>& get_returns() const { return returns_; }
};

} // namespace Alaris::Volatility
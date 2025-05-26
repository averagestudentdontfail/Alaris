#pragma once

#include <ql/quantlib.hpp>
#include "../core/memory_pool.h"
#include <vector>
#include <deque>
#include <memory> 

namespace Alaris::Volatility {

class QuantLibGJRGARCHModel {
private:
    // Memory management
    Core::MemoryPool& mem_pool_; // Reference to a memory pool

    // Model parameters (specific to GJR-GARCH(1,1) for simplicity, can be extended)
    QuantLib::Real omega_;    // Constant term (omega_[0])
    QuantLib::Real alpha_;    // ARCH parameter (alpha_[0])
    QuantLib::Real beta_;     // GARCH parameter (beta_[0])
    QuantLib::Real gamma_;    // Asymmetry parameter (gamma_[0])

    // Historical data and state
    std::deque<QuantLib::Real> returns_;
    std::deque<QuantLib::Real> squared_returns_;
    std::deque<QuantLib::Real> conditional_variances_;
    std::deque<bool> negative_returns_; // For asymmetry effect

    QuantLib::Real current_variance_;
    QuantLib::Real current_volatility_;

    // Model configuration
    QuantLib::Size max_history_length_;
    // Lags are fixed to 1 for GJR-GARCH(1,1) in this simplified setup.
    // If more complex GJR-GARCH(p,q) is needed, lag_p_ and lag_q_ would be reinstated.

    // Calibration parameters
    QuantLib::Real tolerance_;
    QuantLib::Size max_iterations_;

    // Performance tracking
    mutable size_t forecast_count_;
    // mutable QuantLib::Real cumulative_error_; // Can be added back if error tracking is sophisticated

    // Helper methods
    void initialize_parameters(); // Initializes GJR-GARCH(1,1) parameters
    // QuantLib::Real calculate_conditional_variance(QuantLib::Size index) const; // May not be needed externally
    void update_variance_series(); // Recalculates variance series based on current data and parameters
    bool is_stationary() const;    // Checks stationarity for GJR-GARCH(1,1)

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
    void update_batch(const std::vector<QuantLib::Real>& returns_batch); // Changed name for clarity

    // Generate volatility forecast using GJR-GARCH
    QuantLib::Real forecast_volatility(QuantLib::Size horizon = 1);
    std::vector<QuantLib::Real> forecast_volatility_path(QuantLib::Size horizon);

    // Calibrate model to historical data
    bool calibrate(const std::vector<QuantLib::Real>& historical_returns); // Changed name for clarity

    // Model diagnostics and validation
    QuantLib::Real log_likelihood() const; // Calculates log-likelihood for the current parameters and data
    std::vector<QuantLib::Real> get_parameters() const; // Returns [omega, alpha, beta, gamma]
    bool is_model_valid() const; // Checks parameter validity and stationarity

    // Performance metrics (example)
    // QuantLib::Real get_forecast_accuracy() const; // Implementation would require actuals
    // void reset_performance_metrics();

    // Configuration
    void set_max_history_length(QuantLib::Size length);
    void set_calibration_parameters(QuantLib::Real tolerance, QuantLib::Size max_iterations);

    // Current state
    QuantLib::Real current_volatility() const { return current_volatility_; }
    QuantLib::Real current_variance() const { return current_variance_; }
    QuantLib::Size sample_size() const { return returns_.size(); }
};

// Factory function (optional, if specific setup is needed beyond constructor)
// std::unique_ptr<QuantLibGJRGARCHModel> create_gjrgarch_model(Core::MemoryPool& mem_pool);

// No standard GARCH model or its utilities here anymore

} // namespace Alaris::Volatility
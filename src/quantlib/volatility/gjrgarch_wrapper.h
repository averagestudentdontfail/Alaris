#pragma once

#include <ql/quantlib.hpp>
#include "../core/memory_pool.h"
#include <vector>
#include <deque>

namespace Alaris::Volatility {

class QuantLibGJRGARCHModel {
private:
    // Model parameters
    QuantLib::Array omega_;    // Constant term
    QuantLib::Array alpha_;    // ARCH parameters
    QuantLib::Array beta_;     // GARCH parameters
    QuantLib::Array gamma_;    // Asymmetry parameters (GJR extension)
    
    // Historical data and state
    std::deque<QuantLib::Real> returns_;
    std::deque<QuantLib::Real> squared_returns_;
    std::deque<QuantLib::Real> conditional_variances_;
    std::deque<bool> negative_returns_; // For asymmetry effect
    
    QuantLib::Real current_variance_;
    QuantLib::Real current_volatility_;
    
    // Model configuration
    QuantLib::Size max_history_length_;
    QuantLib::Size lag_p_; // GARCH lags
    QuantLib::Size lag_q_; // ARCH lags
    
    // Calibration parameters
    QuantLib::Real tolerance_;
    QuantLib::Size max_iterations_;
    
    // Memory management
    Core::MemoryPool& mem_pool_;
    
    // Performance tracking
    mutable size_t forecast_count_;
    mutable QuantLib::Real cumulative_error_;
    
    // Helper methods
    void initialize_parameters();
    QuantLib::Real calculate_conditional_variance(QuantLib::Size index) const;
    void update_variance_series();
    bool is_stationary() const;
    
public:
    explicit QuantLibGJRGARCHModel(Core::MemoryPool& mem_pool,
                                  QuantLib::Size lag_p = 1,
                                  QuantLib::Size lag_q = 1);
    
    ~QuantLibGJRGARCHModel() = default;
    
    // Non-copyable
    QuantLibGJRGARCHModel(const QuantLibGJRGARCHModel&) = delete;
    QuantLibGJRGARCHModel& operator=(const QuantLibGJRGARCHModel&) = delete;
    
    // Initialize model with QuantLib parameters
    void set_parameters(const QuantLib::Array& omega, const QuantLib::Array& alpha,
                       const QuantLib::Array& beta, const QuantLib::Array& gamma);
    
    // Update model with new market data
    void update(QuantLib::Real new_return);
    void update_batch(const std::vector<QuantLib::Real>& returns);
    
    // Generate volatility forecast using GJR-GARCH
    QuantLib::Real forecast_volatility(QuantLib::Size horizon = 1);
    std::vector<QuantLib::Real> forecast_volatility_path(QuantLib::Size horizon);
    
    // Calibrate model to historical data with bounded execution time
    bool calibrate(const std::vector<QuantLib::Real>& returns);
    
    // Model diagnostics and validation
    QuantLib::Real log_likelihood() const;
    QuantLib::Array get_parameters() const;
    bool is_model_valid() const;
    
    // Performance metrics
    QuantLib::Real get_forecast_accuracy() const;
    void reset_performance_metrics();
    
    // Configuration
    void set_max_history_length(QuantLib::Size length);
    void set_calibration_parameters(QuantLib::Real tolerance, QuantLib::Size max_iterations);
    
    // Current state
    QuantLib::Real current_volatility() const { return current_volatility_; }
    QuantLib::Real current_variance() const { return current_variance_; }
    QuantLib::Size sample_size() const { return returns_.size(); }
};

// Standard GARCH model for comparison (now properly separated)
class QuantLibGARCHModel {
private:
    // Model parameters
    double omega_;    // Constant term
    double alpha_;    // ARCH parameter
    double beta_;     // GARCH parameter
    
    // Historical data
    std::vector<double> returns_;
    std::vector<double> conditional_variances_;
    
    double current_variance_;
    double current_volatility_;
    
    // Model configuration
    size_t max_history_length_;
    
    // Memory management
    Core::MemoryPool& mem_pool_;
    
    void initialize_parameters();
    
public:
    explicit QuantLibGARCHModel(Core::MemoryPool& mem_pool);
    ~QuantLibGARCHModel() = default;
    
    // Non-copyable
    QuantLibGARCHModel(const QuantLibGARCHModel&) = delete;
    QuantLibGARCHModel& operator=(const QuantLibGARCHModel&) = delete;
    
    // Standard GARCH model interface
    void set_parameters(double omega, double alpha, double beta);
    
    void update(double new_return);
    double forecast_volatility(size_t horizon = 1);
    bool calibrate(const std::vector<double>& returns);
    
    // Current state
    double current_volatility() const { return current_volatility_; }
    double current_variance() const { return current_variance_; }
    size_t sample_size() const { return returns_.size(); }
};

// Factory functions
std::unique_ptr<QuantLibGARCHModel> create_garch_model(Core::MemoryPool& mem_pool);

// Utility functions
double calculate_garch_likelihood(const std::vector<double>& returns,
                                 double omega, double alpha, double beta);

void optimize_garch_parameters(const std::vector<double>& returns,
                              double& omega, double& alpha, double& beta);

double forecast_garch_volatility(double current_variance, double last_return,
                                double omega, double alpha, double beta, int horizon = 1);

} // namespace Alaris::Volatility

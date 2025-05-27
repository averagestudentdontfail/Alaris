// src/quantlib/volatility/garch_wrapper.h
#pragma once

#include <ql/quantlib.hpp>
#include "../core/memory_pool.h"
#include <vector>
#include <deque>
#include <memory>
#include <mutex>

namespace Alaris::Volatility {

// Enhanced GARCH model using QuantLib's production-ready Garch11 implementation
class QuantLibGARCHModel {
private:
    // Memory management
    Core::MemoryPool& mem_pool_;

    // QuantLib GARCH model - this has proper calibration infrastructure
    std::unique_ptr<QuantLib::Garch11> garch_model_;
    
    // Model parameters
    QuantLib::Real omega_;    // Constant term
    QuantLib::Real alpha_;    // ARCH parameter
    QuantLib::Real beta_;     // GARCH parameter
    
    // Historical data and state
    std::deque<QuantLib::Real> returns_;
    std::deque<QuantLib::Real> conditional_variances_;
    QuantLib::Real current_variance_;
    QuantLib::Real current_volatility_;
    
    // Model configuration
    QuantLib::Size max_history_length_;
    QuantLib::Real tolerance_;
    QuantLib::Size max_iterations_;
    
    // Performance and state tracking
    mutable size_t forecast_count_;
    bool is_calibrated_;
    double last_log_likelihood_;
    
    // Thread safety for production use
    mutable std::mutex model_mutex_;
    
    // Helper methods
    void initialize_default_parameters();
    void update_variance_series();
    bool validate_parameters() const;
    void calculate_unconditional_variance();
    
public:
    explicit QuantLibGARCHModel(Core::MemoryPool& mem_pool);
    ~QuantLibGARCHModel() = default;

    // Non-copyable for safety
    QuantLibGARCHModel(const QuantLibGARCHModel&) = delete;
    QuantLibGARCHModel& operator=(const QuantLibGARCHModel&) = delete;

    // Model parameter management
    void set_parameters(QuantLib::Real omega, QuantLib::Real alpha, QuantLib::Real beta);
    std::vector<QuantLib::Real> get_parameters() const;
    
    // Data management
    void update(QuantLib::Real new_return);
    void update_batch(const std::vector<QuantLib::Real>& returns_batch);
    void clear_history();
    
    // Calibration using QuantLib's infrastructure
    bool calibrate(const std::vector<QuantLib::Real>& historical_returns);
    bool calibrate_with_initial_guess(const std::vector<QuantLib::Real>& historical_returns,
                                     QuantLib::Real omega_guess,
                                     QuantLib::Real alpha_guess,
                                     QuantLib::Real beta_guess);
    
    // Volatility forecasting
    QuantLib::Real forecast_volatility(QuantLib::Size horizon = 1);
    std::vector<QuantLib::Real> forecast_volatility_path(QuantLib::Size horizon);
    
    // Advanced forecasting methods for strategy use
    QuantLib::Real forecast_conditional_variance(QuantLib::Size horizon);
    std::vector<QuantLib::Real> forecast_variance_path(QuantLib::Size horizon);
    
    // Model diagnostics and validation
    QuantLib::Real log_likelihood() const;
    QuantLib::Real aic() const;  // Akaike Information Criterion
    QuantLib::Real bic() const;  // Bayesian Information Criterion
    bool is_stationary() const;
    bool is_model_valid() const;
    bool is_calibrated() const { return is_calibrated_; }
    
    // Residual analysis for model validation
    std::vector<QuantLib::Real> calculate_standardized_residuals() const;
    QuantLib::Real ljung_box_test(QuantLib::Size lags = 10) const;
    
    // Configuration
    void set_max_history_length(QuantLib::Size length);
    void set_calibration_parameters(QuantLib::Real tolerance, QuantLib::Size max_iterations);
    
    // Current state accessors
    QuantLib::Real current_volatility() const { 
        std::lock_guard<std::mutex> lock(model_mutex_);
        return current_volatility_; 
    }
    QuantLib::Real current_variance() const { 
        std::lock_guard<std::mutex> lock(model_mutex_);
        return current_variance_; 
    }
    QuantLib::Size sample_size() const { 
        std::lock_guard<std::mutex> lock(model_mutex_);
        return returns_.size(); 
    }
    
    // Performance tracking
    size_t get_forecast_count() const { return forecast_count_; }
    void reset_forecast_count() { forecast_count_ = 0; }
    
    // Data access for external analysis
    const std::deque<QuantLib::Real>& get_returns() const { return returns_; }
    const std::deque<QuantLib::Real>& get_conditional_variances() const { return conditional_variances_; }
    
    // Model comparison utilities
    struct ModelFitStatistics {
        QuantLib::Real log_likelihood;
        QuantLib::Real aic;
        QuantLib::Real bic;
        QuantLib::Real ljung_box_p_value;
        bool is_stationary;
        size_t sample_size;
    };
    
    ModelFitStatistics get_fit_statistics() const;
};

// Enhanced volatility forecasting system
class VolatilityForecaster {
private:
    QuantLibGARCHModel& garch_model_;
    Core::MemoryPool& mem_pool_;
    
    // Ensemble components and weights
    std::vector<double> model_weights_;
    std::vector<double> model_accuracies_;
    
    // Historical volatility calculation parameters
    static constexpr size_t DEFAULT_HISTORICAL_WINDOW = 30;
    static constexpr size_t MIN_HISTORICAL_WINDOW = 5;
    static constexpr size_t MAX_HISTORICAL_WINDOW = 252;
    static constexpr double DEFAULT_VOLATILITY = 0.20;
    
    // Performance tracking
    mutable std::mutex forecaster_mutex_;
    size_t total_forecasts_;
    double forecast_error_sum_;
    
    // Helper methods
    double calculate_historical_volatility(const std::vector<double>& returns, 
                                         size_t window = DEFAULT_HISTORICAL_WINDOW) const;
    double calculate_ewma_volatility(const std::vector<double>& returns, 
                                   double lambda = 0.94) const;
    void update_model_weights();
    
public:
    explicit VolatilityForecaster(QuantLibGARCHModel& garch_model, 
                                 Core::MemoryPool& mem_pool);
    
    // Forecasting methods
    double generate_ensemble_forecast(size_t horizon, const std::vector<double>& returns);
    std::vector<double> generate_forecast_path(size_t horizon, const std::vector<double>& returns);
    
    // Individual model forecasts for comparison
    double generate_garch_forecast(size_t horizon);
    double generate_historical_forecast(const std::vector<double>& returns, size_t window = DEFAULT_HISTORICAL_WINDOW);
    double generate_ewma_forecast(const std::vector<double>& returns, double lambda = 0.94);
    
    // Model weight management
    void set_model_weights(const std::vector<double>& weights);
    std::vector<double> get_model_weights() const;
    void update_forecast_accuracy(double forecast_error);
    
    // Performance and diagnostics
    double get_average_forecast_error() const;
    size_t get_total_forecasts() const { return total_forecasts_; }
    void reset_performance_stats();
    
    bool is_healthy() const;
};

// Global volatility forecasting infrastructure
void initialize_volatility_forecaster(QuantLibGARCHModel& garch_model, 
                                     Core::MemoryPool& mem_pool);

// Production-ready forecasting functions
double forecast_volatility_ensemble(size_t horizon, const std::vector<double>& returns);
std::vector<double> forecast_volatility_path_ensemble(size_t horizon, 
                                                     const std::vector<double>& returns);

// Model validation and selection utilities
bool validate_forecast_parameters(size_t horizon, const std::vector<double>& returns);
double calculate_forecast_confidence(const std::vector<double>& recent_forecasts,
                                   const std::vector<double>& realized_values);

// Advanced forecasting methods for strategy use
struct VolatilityRegime {
    double low_vol_threshold;
    double high_vol_threshold;
    double current_regime_probability;
    enum class Regime { LOW, MEDIUM, HIGH };
    Regime current_regime;
};

VolatilityRegime detect_volatility_regime(const std::vector<double>& returns, 
                                         size_t lookback_window = 60);

// Risk-adjusted forecasting for position sizing
double calculate_vol_of_vol(const std::vector<double>& vol_forecasts);
double calculate_forecast_uncertainty(const std::vector<double>& recent_forecasts,
                                    const std::vector<double>& realized_values);

// System health and monitoring
bool is_forecasting_system_healthy();
void reset_forecasting_system();

} // namespace Alaris::Volatility
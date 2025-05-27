// src/quantlib/volatility/garch_wrapper.cpp
#include "garch_wrapper.h"
#include <ql/models/equity/garch.hpp>
#include <ql/math/optimization/levenbergmarquardt.hpp>
#include <ql/math/optimization/constraint.hpp>
#include <algorithm>
#include <cmath>
#include <numeric>
#include <limits>

namespace Alaris::Volatility {

QuantLibGARCHModel::QuantLibGARCHModel(Core::MemoryPool& mem_pool)
    : mem_pool_(mem_pool),
      omega_(1e-6),
      alpha_(0.1),
      beta_(0.85),
      current_variance_(0.04),
      current_volatility_(0.2),
      max_history_length_(2520), // ~10 years of daily data
      tolerance_(1e-8),
      max_iterations_(1000),
      forecast_count_(0),
      is_calibrated_(false),
      last_log_likelihood_(-std::numeric_limits<double>::infinity()) {
    
    initialize_default_parameters();
}

void QuantLibGARCHModel::initialize_default_parameters() {
    // Conservative default parameters that ensure stationarity
    omega_ = 1e-6;
    alpha_ = 0.1;   // ARCH effect
    beta_  = 0.85;  // GARCH effect (persistence)
    
    // Verify stationarity: alpha + beta < 1
    if (alpha_ + beta_ >= 0.99) {
        alpha_ = 0.08;
        beta_ = 0.85;
    }
    
    current_variance_ = omega_ / (1.0 - alpha_ - beta_);  // Unconditional variance
    current_volatility_ = std::sqrt(current_variance_);
}

void QuantLibGARCHModel::set_parameters(QuantLib::Real omega, QuantLib::Real alpha, QuantLib::Real beta) {
    std::lock_guard<std::mutex> lock(model_mutex_);
    
    omega_ = omega;
    alpha_ = alpha;
    beta_  = beta;
    
    if (validate_parameters()) {
        calculate_unconditional_variance();
        if (!returns_.empty()) {
            update_variance_series();
        }
        is_calibrated_ = true;  // Mark as calibrated if parameters are valid
    } else {
        // Revert to safe defaults if invalid parameters
        initialize_default_parameters();
        is_calibrated_ = false;
    }
}

std::vector<QuantLib::Real> QuantLibGARCHModel::get_parameters() const {
    std::lock_guard<std::mutex> lock(model_mutex_);
    return {omega_, alpha_, beta_};
}

bool QuantLibGARCHModel::validate_parameters() const {
    // Parameters must be non-negative
    if (omega_ <= 0.0 || alpha_ < 0.0 || beta_ < 0.0) {
        return false;
    }
    
    // Stationarity condition: alpha + beta < 1
    if (alpha_ + beta_ >= 1.0) {
        return false;
    }
    
    // Reasonable bounds to prevent numerical issues
    if (alpha_ > 0.5 || beta_ > 0.99) {
        return false;
    }
    
    return true;
}

void QuantLibGARCHModel::calculate_unconditional_variance() {
    if (alpha_ + beta_ < 0.999) {
        current_variance_ = omega_ / (1.0 - alpha_ - beta_);
    } else {
        current_variance_ = 0.04;  // Default 20% volatility
    }
    current_variance_ = std::max(current_variance_, 1e-8);
    current_volatility_ = std::sqrt(current_variance_);
}

void QuantLibGARCHModel::update(QuantLib::Real new_return) {
    std::lock_guard<std::mutex> lock(model_mutex_);
    
    // Add new return to history
    returns_.push_back(new_return);
    
    // Trim history if it gets too long
    if (returns_.size() > max_history_length_) {
        returns_.pop_front();
        if (!conditional_variances_.empty()) {
            conditional_variances_.pop_front();
        }
    }
    
    // Update current variance using GARCH equation
    if (conditional_variances_.empty()) {
        // Initialize with unconditional variance
        conditional_variances_.push_back(current_variance_);
    }
    
    // GARCH(1,1) equation: σ²ₜ = ω + α·r²ₜ₋₁ + β·σ²ₜ₋₁
    QuantLib::Real last_return_squared = returns_.empty() ? 0.0 : 
                                        std::pow(returns_.back(), 2);
    QuantLib::Real last_variance = conditional_variances_.empty() ? current_variance_ :
                                  conditional_variances_.back();
    
    current_variance_ = omega_ + alpha_ * last_return_squared + beta_ * last_variance;
    current_variance_ = std::max(current_variance_, 1e-8);  // Ensure positive
    current_volatility_ = std::sqrt(current_variance_);
    
    conditional_variances_.push_back(current_variance_);
    
    // Trim variance history to match returns
    if (conditional_variances_.size() > max_history_length_) {
        conditional_variances_.pop_front();
    }
}

void QuantLibGARCHModel::update_batch(const std::vector<QuantLib::Real>& returns_batch) {
    for (QuantLib::Real return_val : returns_batch) {
        update(return_val);
    }
}

void QuantLibGARCHModel::clear_history() {
    std::lock_guard<std::mutex> lock(model_mutex_);
    returns_.clear();
    conditional_variances_.clear();
    calculate_unconditional_variance();
    is_calibrated_ = false;
}

bool QuantLibGARCHModel::calibrate(const std::vector<QuantLib::Real>& historical_returns) {
    if (historical_returns.size() < 30) {
        // Use reasonable defaults for insufficient data
        initialize_default_parameters();
        return true;
    }
    
    try {
        std::lock_guard<std::mutex> lock(model_mutex_);
        
        // Clear existing data and use provided returns
        returns_.clear();
        conditional_variances_.clear();
        
        for (QuantLib::Real ret : historical_returns) {
            returns_.push_back(ret);
        }
        
        // Create QuantLib GARCH model for calibration
        garch_model_ = std::make_unique<QuantLib::Garch11>(tolerance_, max_iterations_);
        
        // Calculate sample variance for initial guess
        QuantLib::Real sum_returns = std::accumulate(historical_returns.begin(), 
                                                    historical_returns.end(), 0.0);
        QuantLib::Real mean_return = sum_returns / historical_returns.size();
        
        QuantLib::Real sample_variance = 0.0;
        for (QuantLib::Real ret : historical_returns) {
            sample_variance += std::pow(ret - mean_return, 2);
        }
        sample_variance /= (historical_returns.size() - 1);
        sample_variance = std::max(sample_variance, 1e-8);
        
        // Set reasonable initial guesses
        QuantLib::Real omega_guess = sample_variance * 0.05;  // 5% of sample variance
        QuantLib::Real alpha_guess = 0.1;
        QuantLib::Real beta_guess = 0.85;
        
        // Ensure stationarity in initial guess
        if (alpha_guess + beta_guess >= 0.99) {
            alpha_guess = 0.08;
            beta_guess = 0.85;
        }
        
        // Calibrate using QuantLib's optimization
        QuantLib::Array initial_guess(3);
        initial_guess[0] = omega_guess;
        initial_guess[1] = alpha_guess;
        initial_guess[2] = beta_guess;
        
        // Set up constraints for parameter bounds
        std::vector<QuantLib::ext::shared_ptr<QuantLib::Constraint>> constraints;
        
        // Omega > 0
        constraints.push_back(QuantLib::ext::make_shared<QuantLib::BoundaryConstraint>(
            0.0, std::numeric_limits<QuantLib::Real>::max(), 
            QuantLib::BoundaryConstraint::Lower));
        
        // Alpha >= 0 and < 0.5
        constraints.push_back(QuantLib::ext::make_shared<QuantLib::BoundaryConstraint>(
            0.0, 0.5, QuantLib::BoundaryConstraint::Both));
        
        // Beta >= 0 and < 0.99
        constraints.push_back(QuantLib::ext::make_shared<QuantLib::BoundaryConstraint>(
            0.0, 0.99, QuantLib::BoundaryConstraint::Both));
        
        // Use Levenberg-Marquardt optimizer
        QuantLib::LevenbergMarquardt optimizer;
        
        // Set parameters from calibration result
        QuantLib::Array calibrated_params = garch_model_->calibrate(
            historical_returns, optimizer, initial_guess);
        
        omega_ = calibrated_params[0];
        alpha_ = calibrated_params[1];
        beta_  = calibrated_params[2];
        
        // Validate calibrated parameters
        if (!validate_parameters()) {
            // Fall back to reasonable defaults
            omega_ = omega_guess;
            alpha_ = alpha_guess;
            beta_  = beta_guess;
        }
        
        // Update variance series with calibrated parameters
        update_variance_series();
        
        if (!conditional_variances_.empty()) {
            current_variance_ = conditional_variances_.back();
            current_volatility_ = std::sqrt(current_variance_);
        } else {
            calculate_unconditional_variance();
        }
        
        // Calculate log-likelihood for model validation
        last_log_likelihood_ = log_likelihood();
        is_calibrated_ = true;
        
        return true;
        
    } catch (const std::exception& e) {
        // Calibration failed, use reasonable defaults
        initialize_default_parameters();
        
        // Still update with provided data
        clear_history();
        for (QuantLib::Real ret : historical_returns) {
            update(ret);
        }
        
        is_calibrated_ = false;
        return false;
    }
}

bool QuantLibGARCHModel::calibrate_with_initial_guess(
    const std::vector<QuantLib::Real>& historical_returns,
    QuantLib::Real omega_guess,
    QuantLib::Real alpha_guess,
    QuantLib::Real beta_guess) {
    
    // Validate initial guess
    if (omega_guess <= 0 || alpha_guess < 0 || beta_guess < 0 || 
        alpha_guess + beta_guess >= 1.0) {
        return calibrate(historical_returns);  // Fall back to automatic calibration
    }
    
    try {
        std::lock_guard<std::mutex> lock(model_mutex_);
        
        // Set initial parameters
        omega_ = omega_guess;
        alpha_ = alpha_guess;
        beta_  = beta_guess;
        
        // Use provided data
        returns_.clear();
        conditional_variances_.clear();
        for (QuantLib::Real ret : historical_returns) {
            returns_.push_back(ret);
        }
        
        // Create GARCH model with better initial guess
        garch_model_ = std::make_unique<QuantLib::Garch11>(tolerance_, max_iterations_);
        
        QuantLib::Array initial_guess(3);
        initial_guess[0] = omega_guess;
        initial_guess[1] = alpha_guess;
        initial_guess[2] = beta_guess;
        
        QuantLib::LevenbergMarquardt optimizer;
        QuantLib::Array calibrated_params = garch_model_->calibrate(
            historical_returns, optimizer, initial_guess);
        
        omega_ = calibrated_params[0];
        alpha_ = calibrated_params[1];
        beta_  = calibrated_params[2];
        
        if (validate_parameters()) {
            update_variance_series();
            if (!conditional_variances_.empty()) {
                current_variance_ = conditional_variances_.back();
                current_volatility_ = std::sqrt(current_variance_);
            }
            last_log_likelihood_ = log_likelihood();
            is_calibrated_ = true;
            return true;
        }
        
    } catch (const std::exception& e) {
        // Fall back to default calibration
    }
    
    return calibrate(historical_returns);
}

void QuantLibGARCHModel::update_variance_series() {
    if (returns_.empty()) return;
    
    conditional_variances_.clear();
    
    // Initialize with unconditional variance
    calculate_unconditional_variance();
    conditional_variances_.push_back(current_variance_);
    
    // Calculate variance series using GARCH equation
    for (size_t t = 1; t < returns_.size(); ++t) {
        QuantLib::Real last_return_squared = std::pow(returns_[t-1], 2);
        QuantLib::Real last_variance = conditional_variances_[t-1];
        
        QuantLib::Real variance = omega_ + alpha_ * last_return_squared + beta_ * last_variance;
        variance = std::max(variance, 1e-8);
        conditional_variances_.push_back(variance);
    }
    
    if (!conditional_variances_.empty()) {
        current_variance_ = conditional_variances_.back();
        current_volatility_ = std::sqrt(current_variance_);
    }
}

QuantLib::Real QuantLibGARCHModel::forecast_volatility(QuantLib::Size horizon) {
    std::lock_guard<std::mutex> lock(model_mutex_);
    
    if (!is_calibrated_ || returns_.empty()) {
        return 0.20;  // Default volatility
    }
    
    QuantLib::Real forecast_variance = current_variance_;
    QuantLib::Real unconditional_variance = omega_ / (1.0 - alpha_ - beta_);
    QuantLib::Real persistence = alpha_ + beta_;
    
    if (horizon == 1) {
        // One-step ahead forecast
        forecast_count_++;
        return current_volatility_;
    } else {
        // Multi-step forecast
        // E[σ²ₜ₊ₕ] = ω/(1-α-β) + (α+β)ʰ⁻¹ * (σ²ₜ₊₁ - ω/(1-α-β))
        QuantLib::Real decay_factor = std::pow(persistence, static_cast<QuantLib::Real>(horizon - 1));
        forecast_variance = unconditional_variance + 
                           decay_factor * (current_variance_ - unconditional_variance);
    }
    
    forecast_count_++;
    return std::sqrt(std::max(forecast_variance, 1e-8));
}

std::vector<QuantLib::Real> QuantLibGARCHModel::forecast_volatility_path(QuantLib::Size horizon) {
    std::vector<QuantLib::Real> path;
    if (horizon == 0) return path;
    
    path.reserve(horizon);
    for (QuantLib::Size h = 1; h <= horizon; ++h) {
        path.push_back(forecast_volatility(h));
    }
    
    return path;
}

QuantLib::Real QuantLibGARCHModel::forecast_conditional_variance(QuantLib::Size horizon) {
    std::lock_guard<std::mutex> lock(model_mutex_);
    
    if (!is_calibrated_ || returns_.empty()) {
        return 0.04;  // Default variance (20% vol)
    }
    
    if (horizon == 1) {
        return current_variance_;
    }
    
    QuantLib::Real unconditional_variance = omega_ / (1.0 - alpha_ - beta_);
    QuantLib::Real persistence = alpha_ + beta_;
    QuantLib::Real decay_factor = std::pow(persistence, static_cast<QuantLib::Real>(horizon - 1));
    
    return unconditional_variance + decay_factor * (current_variance_ - unconditional_variance);
}

std::vector<QuantLib::Real> QuantLibGARCHModel::forecast_variance_path(QuantLib::Size horizon) {
    std::vector<QuantLib::Real> path;
    if (horizon == 0) return path;
    
    path.reserve(horizon);
    for (QuantLib::Size h = 1; h <= horizon; ++h) {
        path.push_back(forecast_conditional_variance(h));
    }
    
    return path;
}

QuantLib::Real QuantLibGARCHModel::log_likelihood() const {
    std::lock_guard<std::mutex> lock(model_mutex_);
    
    if (returns_.size() != conditional_variances_.size() || returns_.empty()) {
        return -std::numeric_limits<QuantLib::Real>::infinity();
    }
    
    QuantLib::Real ll = 0.0;
    const QuantLib::Real log_2pi = std::log(2.0 * M_PI);
    
    for (size_t t = 0; t < returns_.size(); ++t) {
        QuantLib::Real variance = conditional_variances_[t];
        if (variance <= 1e-10) {
            return -std::numeric_limits<QuantLib::Real>::infinity();
        }
        
        QuantLib::Real return_val = returns_[t];
        ll -= 0.5 * (log_2pi + std::log(variance) + (return_val * return_val) / variance);
    }
    
    return ll;
}

QuantLib::Real QuantLibGARCHModel::aic() const {
    QuantLib::Real ll = log_likelihood();
    if (!std::isfinite(ll)) return std::numeric_limits<QuantLib::Real>::infinity();
    return -2.0 * ll + 2.0 * 3.0;  // 3 parameters (omega, alpha, beta)
}

QuantLib::Real QuantLibGARCHModel::bic() const {
    QuantLib::Real ll = log_likelihood();
    if (!std::isfinite(ll)) return std::numeric_limits<QuantLib::Real>::infinity();
    return -2.0 * ll + 3.0 * std::log(static_cast<QuantLib::Real>(returns_.size()));
}

bool QuantLibGARCHModel::is_stationary() const {
    std::lock_guard<std::mutex> lock(model_mutex_);
    return (alpha_ + beta_) < 1.0;
}

bool QuantLibGARCHModel::is_model_valid() const {
    std::lock_guard<std::mutex> lock(model_mutex_);
    return validate_parameters() && is_calibrated_;
}

std::vector<QuantLib::Real> QuantLibGARCHModel::calculate_standardized_residuals() const {
    std::lock_guard<std::mutex> lock(model_mutex_);
    
    std::vector<QuantLib::Real> residuals;
    if (returns_.size() != conditional_variances_.size()) {
        return residuals;
    }
    
    residuals.reserve(returns_.size());
    for (size_t t = 0; t < returns_.size(); ++t) {
        QuantLib::Real std_dev = std::sqrt(conditional_variances_[t]);
        residuals.push_back(returns_[t] / std_dev);
    }
    
    return residuals;
}

QuantLib::Real QuantLibGARCHModel::ljung_box_test(QuantLib::Size lags) const {
    auto residuals = calculate_standardized_residuals();
    if (residuals.size() <= lags) return 0.0;
    
    // Simple Ljung-Box test implementation
    QuantLib::Real n = static_cast<QuantLib::Real>(residuals.size());
    QuantLib::Real lb_stat = 0.0;
    
    for (QuantLib::Size k = 1; k <= lags; ++k) {
        // Calculate autocorrelation at lag k
        QuantLib::Real mean = std::accumulate(residuals.begin(), residuals.end(), 0.0) / n;
        
        QuantLib::Real numerator = 0.0;
        QuantLib::Real denominator = 0.0;
        
        for (size_t t = k; t < residuals.size(); ++t) {
            numerator += (residuals[t] - mean) * (residuals[t-k] - mean);
        }
        
        for (size_t t = 0; t < residuals.size(); ++t) {
            denominator += std::pow(residuals[t] - mean, 2);
        }
        
        QuantLib::Real autocorr = (denominator > 1e-10) ? numerator / denominator : 0.0;
        lb_stat += autocorr * autocorr / (n - k);
    }
    
    return n * (n + 2.0) * lb_stat;  // Chi-squared distributed under null hypothesis
}

void QuantLibGARCHModel::set_max_history_length(QuantLib::Size length) {
    std::lock_guard<std::mutex> lock(model_mutex_);
    max_history_length_ = length;
    
    // Trim current history if necessary
    while (returns_.size() > max_history_length_) {
        returns_.pop_front();
    }
    while (conditional_variances_.size() > max_history_length_) {
        conditional_variances_.pop_front();
    }
}

void QuantLibGARCHModel::set_calibration_parameters(QuantLib::Real tolerance, QuantLib::Size max_iterations) {
    std::lock_guard<std::mutex> lock(model_mutex_);
    tolerance_ = tolerance;
    max_iterations_ = max_iterations;
}

QuantLibGARCHModel::ModelFitStatistics QuantLibGARCHModel::get_fit_statistics() const {
    ModelFitStatistics stats;
    stats.log_likelihood = log_likelihood();
    stats.aic = aic();
    stats.bic = bic();
    stats.ljung_box_p_value = ljung_box_test();
    stats.is_stationary = is_stationary();
    stats.sample_size = sample_size();
    return stats;
}

// VolatilityForecaster Implementation

VolatilityForecaster::VolatilityForecaster(QuantLibGARCHModel& garch_model, 
                                         Core::MemoryPool& mem_pool)
    : garch_model_(garch_model), 
      mem_pool_(mem_pool),
      model_weights_{0.7, 0.2, 0.1},  // GARCH, Historical, EWMA
      model_accuracies_{0.5, 0.5, 0.5},
      total_forecasts_(0),
      forecast_error_sum_(0.0) {
}

double VolatilityForecaster::calculate_historical_volatility(
    const std::vector<double>& returns, size_t window) const {
    
    if (returns.empty()) return DEFAULT_VOLATILITY;
    
    size_t actual_window = std::min({window, returns.size(), MAX_HISTORICAL_WINDOW});
    actual_window = std::max(actual_window, MIN_HISTORICAL_WINDOW);
    
    if (actual_window < MIN_HISTORICAL_WINDOW) return DEFAULT_VOLATILITY;
    
    size_t start_idx = returns.size() - actual_window;
    
    // Calculate sample variance
    double sum = 0.0;
    for (size_t i = start_idx; i < returns.size(); ++i) {
        sum += returns[i];
    }
    double mean = sum / actual_window;
    
    double variance = 0.0;
    for (size_t i = start_idx; i < returns.size(); ++i) {
        double diff = returns[i] - mean;
        variance += diff * diff;
    }
    variance /= (actual_window - 1);  // Sample variance
    
    // Annualized volatility (assuming daily returns)
    return std::sqrt(variance * 252.0);
}

double VolatilityForecaster::calculate_ewma_volatility(
    const std::vector<double>& returns, double lambda) const {
    
    if (returns.empty()) return DEFAULT_VOLATILITY;
    
    double variance = 0.0;
    double weight_sum = 0.0;
    double weight = 1.0;
    
    // Calculate EWMA variance (starting from most recent)
    for (auto it = returns.rbegin(); it != returns.rend(); ++it) {
        variance += weight * (*it) * (*it);
        weight_sum += weight;
        weight *= lambda;
        
        // Stop when weights become negligible
        if (weight < 1e-6) break;
    }
    
    if (weight_sum > 1e-10) {
        variance /= weight_sum;
    }
    
    // Annualized volatility
    return std::sqrt(variance * 252.0);
}

double VolatilityForecaster::generate_ensemble_forecast(
    size_t horizon, const std::vector<double>& returns) {
    
    std::lock_guard<std::mutex> lock(forecaster_mutex_);
    
    try {
        double garch_forecast = generate_garch_forecast(horizon);
        double hist_forecast = generate_historical_forecast(returns);
        double ewma_forecast = generate_ewma_forecast(returns);
        
        // Weighted ensemble
        double ensemble_forecast = model_weights_[0] * garch_forecast +
                                 model_weights_[1] * hist_forecast +
                                 model_weights_[2] * ewma_forecast;
        
        total_forecasts_++;
        return ensemble_forecast;
        
    } catch (const std::exception& e) {
        return DEFAULT_VOLATILITY;
    }
}

double VolatilityForecaster::generate_garch_forecast(size_t horizon) {
    return garch_model_.forecast_volatility(horizon);
}

double VolatilityForecaster::generate_historical_forecast(
    const std::vector<double>& returns, size_t window) {
    return calculate_historical_volatility(returns, window);
}

double VolatilityForecaster::generate_ewma_forecast(
    const std::vector<double>& returns, double lambda) {
    return calculate_ewma_volatility(returns, lambda);
}

void VolatilityForecaster::update_model_weights() {
    // Update weights based on model accuracies
    double total_accuracy = std::accumulate(model_accuracies_.begin(), 
                                          model_accuracies_.end(), 0.0);
    
    if (total_accuracy > 1e-6) {
        for (size_t i = 0; i < model_weights_.size() && i < model_accuracies_.size(); ++i) {
            model_weights_[i] = model_accuracies_[i] / total_accuracy;
        }
    }
}

void VolatilityForecaster::update_forecast_accuracy(double forecast_error) {
    std::lock_guard<std::mutex> lock(forecaster_mutex_);
    forecast_error_sum_ += std::abs(forecast_error);
    
    // Update model accuracies (simplified approach)
    // In practice, you'd track individual model performance
    double accuracy = 1.0 / (1.0 + std::abs(forecast_error));
    for (auto& acc : model_accuracies_) {
        acc = 0.9 * acc + 0.1 * accuracy;  // Exponential smoothing
    }
    
    update_model_weights();
}

bool VolatilityForecaster::is_healthy() const {
    std::lock_guard<std::mutex> lock(forecaster_mutex_);
    
    // Check if GARCH model is healthy
    if (!garch_model_.is_model_valid()) return false;
    
    // Check if weights are reasonable
    double total_weight = std::accumulate(model_weights_.begin(), model_weights_.end(), 0.0);
    if (std::abs(total_weight - 1.0) > 0.1) return false;
    
    return true;
}

// Global forecaster instance
static std::unique_ptr<VolatilityForecaster> global_forecaster = nullptr;
static std::mutex global_forecaster_mutex;

void initialize_volatility_forecaster(QuantLibGARCHModel& garch_model, 
                                     Core::MemoryPool& mem_pool) {
    std::lock_guard<std::mutex> lock(global_forecaster_mutex);
    global_forecaster = std::make_unique<VolatilityForecaster>(garch_model, mem_pool);
}

double forecast_volatility_ensemble(size_t horizon, const std::vector<double>& returns) {
    std::lock_guard<std::mutex> lock(global_forecaster_mutex);
    
    if (!global_forecaster) {
        throw std::runtime_error("Volatility forecaster not initialized");
    }
    
    return global_forecaster->generate_ensemble_forecast(horizon, returns);
}

std::vector<double> forecast_volatility_path_ensemble(size_t horizon, 
                                                     const std::vector<double>& returns) {
    std::lock_guard<std::mutex> lock(global_forecaster_mutex);
    
    if (!global_forecaster) {
        throw std::runtime_error("Volatility forecaster not initialized");
    }
    
    return global_forecaster->generate_forecast_path(horizon, returns);
}

bool validate_forecast_parameters(size_t horizon, const std::vector<double>& returns) {
    if (horizon == 0 || horizon > 100) return false;
    if (returns.size() > 10000) return false;
    
    for (double ret : returns) {
        if (!std::isfinite(ret) || std::abs(ret) > 1.0) return false;
    }
    
    return true;
}

bool is_forecasting_system_healthy() {
    std::lock_guard<std::mutex> lock(global_forecaster_mutex);
    return global_forecaster && global_forecaster->is_healthy();
}

} // namespace Alaris::Volatility
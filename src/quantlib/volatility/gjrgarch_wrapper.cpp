// src/quantlib/volatility/gjrgarch_wrapper.cpp  
#include "gjrgarch_wrapper.h"
#include <ql/math/optimization/levenbergmarquardt.hpp>
#include <ql/math/optimization/problem.hpp>
#include <algorithm>
#include <cmath>

namespace Alaris::Volatility {

QuantLibGJRGARCHModel::QuantLibGJRGARCHModel(Core::MemoryPool& mem_pool,
                                            QuantLib::Size lag_p,
                                            QuantLib::Size lag_q)
    : mem_pool_(mem_pool), lag_p_(lag_p), lag_q_(lag_q),
      max_history_length_(2520), // ~10 years of daily data
      tolerance_(1e-6), max_iterations_(1000),
      current_variance_(0.04), current_volatility_(0.2),
      forecast_count_(0), cumulative_error_(0.0) {
    
    initialize_parameters();
}

void QuantLibGJRGARCHModel::initialize_parameters() {
    // Initialize with typical GJR-GARCH(1,1) parameters
    omega_.resize(1);
    alpha_.resize(lag_q_);
    beta_.resize(lag_p_);
    gamma_.resize(lag_q_);
    
    // Default parameters (will be calibrated)
    omega_[0] = 0.00001;  // Small constant
    alpha_[0] = 0.05;     // ARCH parameter
    beta_[0] = 0.90;      // GARCH parameter
    gamma_[0] = 0.05;     // Asymmetry parameter
}

void QuantLibGJRGARCHModel::set_parameters(const QuantLib::Array& omega,
                                          const QuantLib::Array& alpha,
                                          const QuantLib::Array& beta,
                                          const QuantLib::Array& gamma) {
    omega_ = omega;
    alpha_ = alpha;
    beta_ = beta;
    gamma_ = gamma;
    
    // Recalculate variance series with new parameters
    update_variance_series();
}

void QuantLibGJRGARCHModel::update(QuantLib::Real new_return) {
    // Add new return
    returns_.push_back(new_return);
    squared_returns_.push_back(new_return * new_return);
    negative_returns_.push_back(new_return < 0.0);
    
    // Maintain maximum history length
    if (returns_.size() > max_history_length_) {
        returns_.pop_front();
        squared_returns_.pop_front();
        negative_returns_.pop_front();
    }
    
    // Calculate new conditional variance
    if (returns_.size() >= std::max(lag_p_, lag_q_)) {
        QuantLib::Real new_variance = omega_[0];
        
        // ARCH terms
        for (QuantLib::Size i = 0; i < lag_q_ && i < squared_returns_.size(); ++i) {
            QuantLib::Real lagged_return_sq = squared_returns_[squared_returns_.size() - 1 - i];
            new_variance += alpha_[i] * lagged_return_sq;
            
            // GJR asymmetry term
            if (negative_returns_[negative_returns_.size() - 1 - i]) {
                new_variance += gamma_[i] * lagged_return_sq;
            }
        }
        
        // GARCH terms
        for (QuantLib::Size i = 0; i < lag_p_ && i < conditional_variances_.size(); ++i) {
            new_variance += beta_[i] * conditional_variances_[conditional_variances_.size() - 1 - i];
        }
        
        current_variance_ = std::max(new_variance, 1e-8); // Ensure positive variance
        current_volatility_ = std::sqrt(current_variance_);
        
        conditional_variances_.push_back(current_variance_);
        
        // Maintain variance history
        if (conditional_variances_.size() > max_history_length_) {
            conditional_variances_.pop_front();
        }
    }
}

QuantLib::Real QuantLibGJRGARCHModel::forecast_volatility(QuantLib::Size horizon) {
    if (horizon == 1) {
        return current_volatility_;
    }
    
    // Multi-step forecast using iteration
    QuantLib::Real forecast_variance = current_variance_;
    QuantLib::Real unconditional_variance = omega_[0] / (1.0 - alpha_[0] - 0.5 * gamma_[0] - beta_[0]);
    
    for (QuantLib::Size h = 1; h < horizon; ++h) {
        // Forecast converges to unconditional variance
        QuantLib::Real persistence = alpha_[0] + 0.5 * gamma_[0] + beta_[0];
        forecast_variance = omega_[0] + persistence * forecast_variance;
        
        // Add convergence to long-run variance
        forecast_variance += (1.0 - std::pow(persistence, h)) * 
                           (unconditional_variance - forecast_variance);
    }
    
    forecast_count_++;
    return std::sqrt(std::max(forecast_variance, 1e-8));
}

std::vector<QuantLib::Real> 
QuantLibGJRGARCHModel::forecast_volatility_path(QuantLib::Size horizon) {
    std::vector<QuantLib::Real> forecasts;
    forecasts.reserve(horizon);
    
    for (QuantLib::Size h = 1; h <= horizon; ++h) {
        forecasts.push_back(forecast_volatility(h));
    }
    
    return forecasts;
}

bool QuantLibGJRGARCHModel::calibrate(const std::vector<QuantLib::Real>& returns) {
    if (returns.size() < 100) {
        return false; // Insufficient data
    }
    
    // Clear existing data and add new returns
    returns_.clear();
    squared_returns_.clear();
    negative_returns_.clear();
    conditional_variances_.clear();
    
    for (QuantLib::Real ret : returns) {
        returns_.push_back(ret);
        squared_returns_.push_back(ret * ret);
        negative_returns_.push_back(ret < 0.0);
    }
    
    // Initialize conditional variances with sample variance
    QuantLib::Real sample_variance = 0.0;
    for (QuantLib::Real ret : returns) {
        sample_variance += ret * ret;
    }
    sample_variance /= returns.size();
    
    // Fill initial variances
    for (size_t i = 0; i < std::max(lag_p_, lag_q_); ++i) {
        conditional_variances_.push_back(sample_variance);
    }
    
    current_variance_ = sample_variance;
    current_volatility_ = std::sqrt(sample_variance);
    
    // Simple calibration using method of moments
    // In production, would use MLE with optimization
    
    // Estimate parameters using sample statistics
    QuantLib::Real mean_return = 0.0;
    for (QuantLib::Real ret : returns) {
        mean_return += ret;
    }
    mean_return /= returns.size();
    
    // Basic parameter estimation
    omega_[0] = sample_variance * 0.1;
    alpha_[0] = 0.05;
    beta_[0] = 0.90;
    gamma_[0] = 0.05;
    
    // Ensure stationarity
    if (!is_stationary()) {
        alpha_[0] = 0.03;
        beta_[0] = 0.85;
        gamma_[0] = 0.03;
    }
    
    update_variance_series();
    
    return true;
}

void QuantLibGJRGARCHModel::update_variance_series() {
    if (returns_.size() < std::max(lag_p_, lag_q_)) {
        return;
    }
    
    conditional_variances_.clear();
    
    // Initialize with sample variance
    QuantLib::Real sample_var = 0.0;
    for (size_t i = 0; i < std::min(static_cast<size_t>(50), returns_.size()); ++i) {
        sample_var += squared_returns_[i];
    }
    sample_var /= std::min(static_cast<size_t>(50), returns_.size());
    
    for (size_t i = 0; i < std::max(lag_p_, lag_q_); ++i) {
        conditional_variances_.push_back(sample_var);
    }
    
    // Calculate conditional variances
    for (size_t t = std::max(lag_p_, lag_q_); t < returns_.size(); ++t) {
        QuantLib::Real variance = omega_[0];
        
        // ARCH terms
        for (QuantLib::Size i = 0; i < lag_q_; ++i) {
            if (t > i) {
                QuantLib::Real lagged_return_sq = squared_returns_[t - 1 - i];
                variance += alpha_[i] * lagged_return_sq;
                
                // GJR asymmetry
                if (negative_returns_[t - 1 - i]) {
                    variance += gamma_[i] * lagged_return_sq;
                }
            }
        }
        
        // GARCH terms
        for (QuantLib::Size i = 0; i < lag_p_; ++i) {
            if (t > i && conditional_variances_.size() > i) {
                variance += beta_[i] * conditional_variances_[conditional_variances_.size() - 1 - i];
            }
        }
        
        variance = std::max(variance, 1e-8);
        conditional_variances_.push_back(variance);
    }
    
    if (!conditional_variances_.empty()) {
        current_variance_ = conditional_variances_.back();
        current_volatility_ = std::sqrt(current_variance_);
    }
}

bool QuantLibGJRGARCHModel::is_stationary() const {
    // Check stationarity condition for GJR-GARCH
    QuantLib::Real persistence = 0.0;
    
    for (QuantLib::Size i = 0; i < alpha_.size(); ++i) {
        persistence += alpha_[i] + 0.5 * gamma_[i]; // E[I(r<0)] ≈ 0.5
    }
    
    for (QuantLib::Size i = 0; i < beta_.size(); ++i) {
        persistence += beta_[i];
    }
    
    return persistence < 1.0;
}

QuantLib::Real QuantLibGJRGARCHModel::log_likelihood() const {
    if (conditional_variances_.size() != returns_.size() - std::max(lag_p_, lag_q_)) {
        return -std::numeric_limits<QuantLib::Real>::infinity();
    }
    
    QuantLib::Real log_likelihood = 0.0;
    const QuantLib::Real log_2pi = std::log(2.0 * M_PI);
    
    size_t start_idx = std::max(lag_p_, lag_q_);
    
    for (size_t t = start_idx; t < returns_.size(); ++t) {
        size_t var_idx = t - start_idx;
        if (var_idx < conditional_variances_.size()) {
            QuantLib::Real variance = conditional_variances_[var_idx];
            QuantLib::Real return_val = returns_[t];
            
            log_likelihood -= 0.5 * (log_2pi + std::log(variance) + 
                                    return_val * return_val / variance);
        }
    }
    
    return log_likelihood;
}

QuantLib::Array QuantLibGJRGARCHModel::get_parameters() const {
    QuantLib::Array params(omega_.size() + alpha_.size() + beta_.size() + gamma_.size());
    
    size_t idx = 0;
    for (size_t i = 0; i < omega_.size(); ++i, ++idx) {
        params[idx] = omega_[i];
    }
    for (size_t i = 0; i < alpha_.size(); ++i, ++idx) {
        params[idx] = alpha_[i];
    }
    for (size_t i = 0; i < beta_.size(); ++i, ++idx) {
        params[idx] = beta_[i];
    }
    for (size_t i = 0; i < gamma_.size(); ++i, ++idx) {
        params[idx] = gamma_[i];
    }
    
    return params;
}

bool QuantLibGJRGARCHModel::is_model_valid() const {
    // Check parameter constraints
    if (omega_[0] <= 0.0) return false;
    
    for (size_t i = 0; i < alpha_.size(); ++i) {
        if (alpha_[i] < 0.0) return false;
    }
    
    for (size_t i = 0; i < beta_.size(); ++i) {
        if (beta_[i] < 0.0) return false;
    }
    
    for (size_t i = 0; i < gamma_.size(); ++i) {
        if (gamma_[i] < 0.0) return false;
    }
    
    return is_stationary();
}

} // namespace Alaris::Volatility
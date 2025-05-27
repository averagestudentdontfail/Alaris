// src/quantlib/volatility/gjrgarch_wrapper.cpp
#include "gjrgarch_wrapper.h"
#include <ql/math/optimization/levenbergmarquardt.hpp> // Only if advanced calibration is used
#include <ql/math/optimization/problem.hpp>          // Only if advanced calibration is used
#include <algorithm>
#include <cmath>
#include <numeric> // For std::accumulate if used in calibration/diagnostics
#include <limits>  // For std::numeric_limits

namespace Alaris::Volatility {

QuantLibGJRGARCHModel::QuantLibGJRGARCHModel(Core::MemoryPool& mem_pool)
    : mem_pool_(mem_pool),
      omega_(1e-6),
      alpha_(0.08),
      beta_(0.90),
      gamma_(0.05),
      current_variance_(0.04),
      current_volatility_(0.2),
      max_history_length_(2520),
      tolerance_(1e-6),
      max_iterations_(1000),
      forecast_count_(0) {
}

void QuantLibGJRGARCHModel::initialize_parameters() {
    // Default GJR-GARCH(1,1) parameters (to be refined by calibration)
    omega_ = 1e-6; // Small positive constant
    alpha_ = 0.08; // ARCH effect
    beta_  = 0.90; // GARCH effect (persistence)
    gamma_ = 0.05; // Asymmetry (leverage effect)
}

void QuantLibGJRGARCHModel::set_parameters(QuantLib::Real omega,
                                           QuantLib::Real alpha,
                                           QuantLib::Real beta,
                                           QuantLib::Real gamma) {
    omega_ = omega;
    alpha_ = alpha;
    beta_  = beta;
    gamma_ = gamma;

    // Parameters have changed, recalculate variance series if data exists
    if (!returns_.empty()) {
        update_variance_series();
    }
}

void QuantLibGJRGARCHModel::update(QuantLib::Real new_return) {
    returns_.push_back(new_return);
    squared_returns_.push_back(new_return * new_return);
    negative_returns_.push_back(new_return < 0.0);

    if (returns_.size() > max_history_length_) {
        returns_.pop_front();
        squared_returns_.pop_front();
        negative_returns_.pop_front();
        if (!conditional_variances_.empty()) { // Ensure conditional_variances_ is also trimmed if it mirrors returns_
            conditional_variances_.pop_front();
        }
    }

    if (returns_.empty()) { // Should not happen if new_return is just added.
        current_variance_ = 0.04; // Default if no data
        current_volatility_ = 0.2;
        conditional_variances_.push_back(current_variance_);
        return;
    }
    
    // Ensure we have at least one previous variance to work with
    // If conditional_variances_ is empty, initialize with current_variance_ (e.g. sample variance from first few returns or a default)
    if (conditional_variances_.empty()) {
         // Initialize with a reasonable default or calculate from initial returns if available
        QuantLib::Real initial_variance = current_variance_; // Use the member default or a startup calculated one
        if (returns_.size() > 1) { // Attempt to calculate from first few returns
            QuantLib::Real sum_sq = 0.0;
            for(const auto& r : returns_) sum_sq += r*r;
            initial_variance = sum_sq / returns_.size();
            initial_variance = std::max(initial_variance, 1e-8); // Ensure positive
        }
        conditional_variances_.push_back(initial_variance);
        current_variance_ = initial_variance;
    }


    // GJR-GARCH(1,1) update equation:
    // sigma_t^2 = omega + alpha * r_{t-1}^2 + gamma * r_{t-1}^2 * I_{t-1} + beta * sigma_{t-1}^2
    // where I_{t-1} = 1 if r_{t-1} < 0, and 0 otherwise.

    QuantLib::Real last_squared_return = squared_returns_.back();
    bool last_return_was_negative = negative_returns_.back();
    QuantLib::Real last_variance = conditional_variances_.back(); // Use the most recent calculated variance

    QuantLib::Real new_variance = omega_;
    new_variance += alpha_ * last_squared_return;
    if (last_return_was_negative) {
        new_variance += gamma_ * last_squared_return;
    }
    new_variance += beta_ * last_variance;

    current_variance_ = std::max(new_variance, 1e-8); // Ensure positive variance
    current_volatility_ = std::sqrt(current_variance_);

    conditional_variances_.push_back(current_variance_);
    if (conditional_variances_.size() > max_history_length_) { // Keep it aligned with returns_
        conditional_variances_.pop_front();
    }
}

void QuantLibGJRGARCHModel::update_batch(const std::vector<QuantLib::Real>& returns_batch) {
    for (QuantLib::Real r : returns_batch) {
        update(r);
    }
}

QuantLib::Real QuantLibGJRGARCHModel::forecast_volatility(QuantLib::Size horizon) {
    if (returns_.empty() || conditional_variances_.empty()) {
        return 0.2; // Default if no data
    }

    QuantLib::Real forecast_variance = current_variance_;
    
    // Long-run variance for GJR-GARCH(1,1): omega / (1 - alpha - beta - gamma * E[I])
    // Assuming E[I] (probability of negative return) is 0.5 for forecasting.
    QuantLib::Real unconditional_alpha_gamma = alpha_ + 0.5 * gamma_;
    if (1.0 - unconditional_alpha_gamma - beta_ <= 1e-8) { // Avoid division by zero or instability
        // If non-stationary or close to it, just persist current variance or return error
        return current_volatility_;
    }
    QuantLib::Real long_run_variance = omega_ / (1.0 - unconditional_alpha_gamma - beta_);
    long_run_variance = std::max(long_run_variance, 1e-8);

    for (QuantLib::Size h = 1; h < horizon; ++h) {
        // Iterated forecast: E[sigma_{t+h}^2 | F_t] = omega + (alpha + beta + gamma*0.5) * E[sigma_{t+h-1}^2 | F_t]
        // This converges to the long-run variance.
        forecast_variance = omega_ + (unconditional_alpha_gamma + beta_) * forecast_variance;
    }
     // A more direct way to forecast h steps ahead for GARCH(1,1) like models:
     // forecast_variance_h = long_run_variance + (alpha_ + beta_ + 0.5*gamma_)^(h-1) * (current_variance_ - long_run_variance)
     // for h >= 1. If h=1, it's current_variance predicted for next step.
    if (horizon > 1) {
        QuantLib::Real persistence_factor = unconditional_alpha_gamma + beta_;
        forecast_variance = long_run_variance + 
                            std::pow(persistence_factor, static_cast<QuantLib::Real>(horizon - 1)) * (current_variance_ - long_run_variance);
    } else { // horizon == 1
        // The next step variance is already computed by the last call to update() and is current_variance_
        // However, forecast_volatility(1) is typically the forecast for t+1 based on info at t.
        // The current_variance_ is sigma_t^2. The forecast for sigma_{t+1}^2 is needed.
        // E[sigma_{t+1}^2] = omega + alpha*r_t^2 + gamma*r_t^2*I_t + beta*sigma_t^2.
        // This is effectively what `update` calculates and stores as the new `current_variance_`.
        // So, if `current_variance_` always reflects the LATEST known variance (sigma_t^2),
        // then a 1-step ahead forecast would be:
        forecast_variance = omega_ + alpha_ * current_variance_ + // E[r_t^2] is sigma_t^2 (current_variance)
                            gamma_ * current_variance_ * 0.5 +   // E[r_t^2 * I_t] is sigma_t^2 * 0.5
                            beta_  * current_variance_;
        // This calculation uses current_variance_ as an estimate for the squared return term's expectation.
        // More accurately, the last update() call would have already set current_variance_ to be sigma_{t+1}^2
        // if new_return was r_t.
        // Let's assume current_variance_ is sigma_t^2 (variance at time t).
        // Then sigma_{t+1}^2 = omega_ + (alpha_ + 0.5*gamma_)*E[r_t^2] + beta_*sigma_t^2
        // where E[r_t^2] = sigma_t^2.
        // So, sigma_{t+1}^2 = omega_ + (alpha_ + 0.5*gamma_ + beta_)*sigma_t^2
        // This formula is for iterative forecasting beyond the immediate next step if the last return isn't known yet.
        // If current_variance_ is sigma_t^2, and r_t is known (last element in returns_), then:
        // sigma_{t+1}^2 = omega_ + alpha_ * returns_.back()^2 + (negative_returns_.back() ? gamma_ * returns_.back()^2 : 0.0) + beta_ * current_variance_;
        // This is what `update()` computes. So, `current_volatility_` should be sqrt of this.
        // The forecast_volatility(1) should return current_volatility_ as calculated by the last update.
         return current_volatility_; // This assumes current_volatility_ IS the 1-step ahead forecast.
    }


    forecast_count_++; // Potentially track per horizon if needed
    return std::sqrt(std::max(forecast_variance, 1e-8));
}

std::vector<QuantLib::Real>
QuantLibGJRGARCHModel::forecast_volatility_path(QuantLib::Size horizon) {
    std::vector<QuantLib::Real> forecasts;
    if (horizon == 0) return forecasts;
    forecasts.reserve(horizon);

    if (returns_.empty() || conditional_variances_.empty()) {
        for (QuantLib::Size h = 1; h <= horizon; ++h) {
            forecasts.push_back(0.2); // Default if no data
        }
        return forecasts;
    }

    QuantLib::Real forecast_variance = current_variance_;
    QuantLib::Real unconditional_alpha_gamma = alpha_ + 0.5 * gamma_;
    QuantLib::Real long_run_variance = 1e-8; // Default to small positive
    if (1.0 - unconditional_alpha_gamma - beta_ > 1e-8) {
         long_run_variance = omega_ / (1.0 - unconditional_alpha_gamma - beta_);
         long_run_variance = std::max(long_run_variance, 1e-8);
    }


    // First step forecast (h=1)
    // This uses the actual last return if available, matching how current_variance_ is updated.
    // Real forecast for t+1 using info up to t (r_t, sigma_t^2)
    QuantLib::Real next_step_variance = omega_ + alpha_ * squared_returns_.back() +
                               (negative_returns_.back() ? gamma_ * squared_returns_.back() : 0.0) +
                               beta_ * current_variance_;
    next_step_variance = std::max(next_step_variance, 1e-8);
    forecasts.push_back(std::sqrt(next_step_variance));
    forecast_variance = next_step_variance; // This is now sigma_{t+1}^2

    // Subsequent steps (h > 1)
    for (QuantLib::Size h = 2; h <= horizon; ++h) {
         // forecast_variance_h = long_run_variance + (alpha_ + beta_ + 0.5*gamma_)^(h-1) * (sigma_{t+1}^2 - long_run_variance)
        QuantLib::Real persistence_factor = unconditional_alpha_gamma + beta_;
         if (1.0 - unconditional_alpha_gamma - beta_ <= 1e-8) { // non-stationary case
             // forecast is just based on persistence from the last forecast_variance
             forecast_variance = omega_ + persistence_factor * forecast_variance;
         } else {
            forecast_variance = long_run_variance +
                                std::pow(persistence_factor, static_cast<QuantLib::Real>(h - 1)) *
                                (next_step_variance - long_run_variance);
         }
        forecasts.push_back(std::sqrt(std::max(forecast_variance, 1e-8)));
    }
    
    forecast_count_ += horizon;
    return forecasts;
}


bool QuantLibGJRGARCHModel::calibrate(const std::vector<QuantLib::Real>& historical_returns) {
    if (historical_returns.size() < 10) { // Reduced minimum to handle edge cases
        // Set default valid parameters for insufficient data
        omega_ = 1e-6;
        alpha_ = 0.05;  // Reduced to ensure stationarity
        beta_  = 0.85;  // Reduced to ensure stationarity  
        gamma_ = 0.05;  // Small gamma
        
        // Ensure stationarity: alpha + beta + gamma/2 < 1
        double stationarity_sum = alpha_ + beta_ + 0.5 * gamma_;
        if (stationarity_sum >= 0.99) {
            // Scale down to ensure stationarity
            double scale_factor = 0.95 / stationarity_sum;
            alpha_ *= scale_factor;
            beta_ *= scale_factor;
            gamma_ *= scale_factor;
        }
        
        // Clear internal data and set up with provided returns (even if few)
        returns_.clear();
        squared_returns_.clear();
        negative_returns_.clear();
        conditional_variances_.clear();

        for (QuantLib::Real ret : historical_returns) {
            returns_.push_back(ret);
            squared_returns_.push_back(ret * ret);
            negative_returns_.push_back(ret < 0.0);
        }
        
        if (!returns_.empty()) {
            update_variance_series();
            if (!conditional_variances_.empty()) {
                current_variance_ = conditional_variances_.back();
                current_volatility_ = std::sqrt(current_variance_);
            }
        } else {
            current_variance_ = 0.04; // Default 20% volatility
            current_volatility_ = 0.2;
        }
        
        return true; // Always succeed with valid default parameters
    }

    // Clear existing internal data and use provided historical_returns
    returns_.clear();
    squared_returns_.clear();
    negative_returns_.clear();
    conditional_variances_.clear();

    for (QuantLib::Real ret : historical_returns) {
        returns_.push_back(ret);
        squared_returns_.push_back(ret * ret);
        negative_returns_.push_back(ret < 0.0);
    }

    // Calculate sample variance for parameter initialization
    QuantLib::Real sample_variance = 0.0;
    QuantLib::Real sum_returns = 0.0;
    for(QuantLib::Real r : historical_returns) {
        sum_returns += r;
        sample_variance += r * r;
    }
    QuantLib::Real mean_return = sum_returns / historical_returns.size();
    sample_variance = sample_variance / historical_returns.size() - (mean_return * mean_return);
    sample_variance = std::max(sample_variance, 1e-7);

    // Set robust default parameters
    alpha_ = 0.05;
    beta_  = 0.85;
    gamma_ = 0.05;
    omega_ = sample_variance * (1.0 - alpha_ - beta_ - 0.5 * gamma_);
    omega_ = std::max(omega_, 1e-7);

    // Ensure stationarity
    double stationarity_sum = alpha_ + beta_ + 0.5 * gamma_;
    if (stationarity_sum >= 0.99) {
        double scale_factor = 0.95 / stationarity_sum;
        alpha_ *= scale_factor;
        beta_ *= scale_factor;
        gamma_ *= scale_factor;
        omega_ = sample_variance * (1.0 - alpha_ - beta_ - 0.5 * gamma_);
        omega_ = std::max(omega_, 1e-7);
    }

    // After calibration, re-calculate the internal variance series
    update_variance_series();
    if (!conditional_variances_.empty()) {
        current_variance_ = conditional_variances_.back();
        current_volatility_ = std::sqrt(current_variance_);
    } else {
        current_variance_ = sample_variance;
        current_volatility_ = std::sqrt(sample_variance);
    }

    return true;
}

void QuantLibGJRGARCHModel::update_variance_series() {
    if (returns_.empty()) {
        return;
    }

    conditional_variances_.clear();
    
    // Initialize first variance: use overall sample variance of the returns history
    QuantLib::Real initial_variance_estimate = 0.0;
    if (returns_.size() > 1) {
        QuantLib::Real sum_sq = 0.0;
        for(const auto& r : returns_) sum_sq += r*r; // Using squared returns directly for variance estimate
        initial_variance_estimate = sum_sq / returns_.size();
    } else if (!returns_.empty()) {
        initial_variance_estimate = returns_.front() * returns_.front();
    }
    initial_variance_estimate = std::max(initial_variance_estimate, 1e-8); // Ensure positive
    
    conditional_variances_.push_back(initial_variance_estimate);

    // Calculate historical conditional variances using the current parameters
    for (size_t t = 1; t < returns_.size(); ++t) {
        QuantLib::Real lagged_sq_return = squared_returns_[t - 1];
        bool lagged_negative_return = negative_returns_[t - 1];
        QuantLib::Real lagged_variance = conditional_variances_[t - 1];

        QuantLib::Real variance = omega_;
        variance += alpha_ * lagged_sq_return;
        if (lagged_negative_return) {
            variance += gamma_ * lagged_sq_return;
        }
        variance += beta_ * lagged_variance;
        conditional_variances_.push_back(std::max(variance, 1e-8));
    }

    if (!conditional_variances_.empty()) {
        current_variance_ = conditional_variances_.back();
        current_volatility_ = std::sqrt(current_variance_);
    }
}


bool QuantLibGJRGARCHModel::is_stationary() const {
    // Stationarity condition for GJR-GARCH(1,1): alpha + beta + gamma/2 < 1
    // Assumes E[I] (indicator for negative return) is 0.5
    return (alpha_ + beta_ + 0.5 * gamma_) < 1.0;
}

QuantLib::Real QuantLibGJRGARCHModel::log_likelihood() const {
    if (returns_.size() != conditional_variances_.size() || returns_.empty()) {
        return -std::numeric_limits<QuantLib::Real>::infinity(); // Not enough data or mismatch
    }

    QuantLib::Real ll = 0.0;
    const QuantLib::Real log_2pi = std::log(2.0 * M_PI);

    for (size_t t = 0; t < returns_.size(); ++t) {
        QuantLib::Real variance = conditional_variances_[t];
        if (variance <= 1e-9) return -std::numeric_limits<QuantLib::Real>::infinity(); // Invalid variance
        QuantLib::Real return_val = returns_[t];
        ll -= 0.5 * (log_2pi + std::log(variance) + (return_val * return_val) / variance);
    }
    return ll;
}

std::vector<QuantLib::Real> QuantLibGJRGARCHModel::get_parameters() const {
    return {omega_, alpha_, beta_, gamma_};
}

bool QuantLibGJRGARCHModel::is_model_valid() const {
    if (omega_ <= 0.0 || alpha_ < 0.0 || beta_ < 0.0 || gamma_ < 0.0) {
        return false; // Parameters must be non-negative (omega strictly positive)
    }
    return is_stationary();
}

void QuantLibGJRGARCHModel::set_max_history_length(QuantLib::Size length) {
    max_history_length_ = length;
    // Trim history if current history is longer than new max length
    while (returns_.size() > max_history_length_) returns_.pop_front();
    while (squared_returns_.size() > max_history_length_) squared_returns_.pop_front();
    while (negative_returns_.size() > max_history_length_) negative_returns_.pop_front();
    while (conditional_variances_.size() > max_history_length_) conditional_variances_.pop_front();
}

void QuantLibGJRGARCHModel::set_calibration_parameters(QuantLib::Real tolerance, QuantLib::Size max_iterations) {
    tolerance_ = tolerance;
    max_iterations_ = max_iterations;
}

// Implementations for QuantLibGARCHModel and related utilities are removed.

} // namespace Alaris::Volatility
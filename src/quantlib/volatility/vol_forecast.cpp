#include "vol_forecast.h"
#include "garch_wrapper.h"  // Changed from gjrgarch_wrapper.h to garch_wrapper.h
#include "../core/memory_pool.h"
#include <algorithm>
#include <numeric>
#include <cmath>
#include <memory>
#include <stdexcept>
#include <mutex>
#include <limits>

namespace Alaris::Volatility {

// Definition for VolatilityForecaster class
class VolatilityForecaster {
private:
    QuantLibGARCHModel& garch_model_;    // Changed from GJR-GARCH to standard GARCH
    Core::MemoryPool& mem_pool_;
    
    // Ensemble weights: [0] for GARCH, [1] for Historical
    std::vector<double> model_weights_;
    std::vector<double> forecast_accuracy_tracking_;
    
    // Thread safety
    mutable std::mutex forecaster_mutex_;
    
    // Configuration
    static constexpr double DEFAULT_VOLATILITY = 0.20;
    static constexpr size_t MAX_RETURNS_FOR_HISTORICAL = 252;
    static constexpr size_t MIN_RETURNS_FOR_HISTORICAL = 5;
    
public:
    explicit VolatilityForecaster(QuantLibGARCHModel& garch_model, 
                                 Core::MemoryPool& mem_pool)
        : garch_model_(garch_model), mem_pool_(mem_pool) {
        
        // Default initial weights for the ensemble
        model_weights_ = {0.6, 0.4}; // 60% GARCH, 40% Historical
        forecast_accuracy_tracking_ = {0.5, 0.5};
    }
    
    double generate_forecast(size_t horizon, const std::vector<double>& returns) {
        std::lock_guard<std::mutex> lock(forecaster_mutex_);
        
        try {
            if (horizon == 0) {
                throw std::invalid_argument("Horizon must be greater than 0");
            }
            
            double garch_forecast = garch_model_.forecast_volatility(horizon);
            double hist_forecast = calculate_historical_volatility(returns, 30);
    
            if (model_weights_.size() >= 2) {
                return model_weights_[0] * garch_forecast + 
                       model_weights_[1] * hist_forecast;
            }
            return garch_forecast; 
        } catch (const std::exception& e) {
            return DEFAULT_VOLATILITY;
        }
    }
    
    std::vector<double> generate_forecast_path(size_t horizon, 
                                              const std::vector<double>& returns) {
        std::lock_guard<std::mutex> lock(forecaster_mutex_);
        
        std::vector<double> forecast_path;
        if (horizon == 0) {
            return forecast_path;
        }
        
        try {
            forecast_path.reserve(horizon);
            
            for (size_t h = 1; h <= horizon; ++h) {
                forecast_path.push_back(generate_forecast_unlocked(h, returns));
            }
            return forecast_path;
        } catch (const std::exception& e) {
            return std::vector<double>(horizon, DEFAULT_VOLATILITY);
        }
    }
    
    void update_model_weights(const std::vector<double>& recent_accuracy_inputs) {
        std::lock_guard<std::mutex> lock(forecaster_mutex_);
        
        if (recent_accuracy_inputs.size() >= 2 && model_weights_.size() >= 2 && 
            forecast_accuracy_tracking_.size() >= 2) {
            
            double garch_acc = std::max(0.0, std::min(1.0, recent_accuracy_inputs[0]));
            double hist_acc = std::max(0.0, std::min(1.0, recent_accuracy_inputs[1]));
            
            forecast_accuracy_tracking_[0] = garch_acc;
            forecast_accuracy_tracking_[1] = hist_acc;

            double total_accuracy = forecast_accuracy_tracking_[0] + forecast_accuracy_tracking_[1];
            
            if (total_accuracy > 1e-6) {
                model_weights_[0] = forecast_accuracy_tracking_[0] / total_accuracy;
                model_weights_[1] = forecast_accuracy_tracking_[1] / total_accuracy;
            } else {
                model_weights_[0] = 0.5;
                model_weights_[1] = 0.5;
            }
        }
    }
    
    std::vector<double> get_model_weights() const {
        std::lock_guard<std::mutex> lock(forecaster_mutex_);
        return model_weights_;
    }
    
    bool is_healthy() const {
        std::lock_guard<std::mutex> lock(forecaster_mutex_);
        
        if (model_weights_.size() != 2) return false;
        
        double total_weight = model_weights_[0] + model_weights_[1];
        if (std::abs(total_weight - 1.0) > 0.1) return false;
        
        if (!garch_model_.is_model_valid()) return false;
        
        return true;
    }
    
private:
    double generate_forecast_unlocked(size_t horizon, const std::vector<double>& returns) {
        double garch_forecast = garch_model_.forecast_volatility(horizon);
        double hist_forecast = calculate_historical_volatility(returns, 30);

        if (model_weights_.size() >= 2) {
            return model_weights_[0] * garch_forecast + 
                   model_weights_[1] * hist_forecast;
        }
        return garch_forecast;
    }
    
    double calculate_historical_volatility(const std::vector<double>& returns, 
                                         size_t lookback_days = 30) {
        if (returns.empty()) {
            return DEFAULT_VOLATILITY;
        }
        
        lookback_days = std::min(lookback_days, MAX_RETURNS_FOR_HISTORICAL);
        size_t actual_lookback = std::min(returns.size(), lookback_days);
        
        if (actual_lookback < MIN_RETURNS_FOR_HISTORICAL) {
            return DEFAULT_VOLATILITY; 
        }
        
        size_t start_idx = returns.size() - actual_lookback;
        
        // Calculate mean
        double sum = 0.0;
        for (size_t i = start_idx; i < returns.size(); ++i) {
            if (!std::isfinite(returns[i])) {
                continue;
            }
            sum += returns[i];
        }
        double mean = sum / actual_lookback;
        
        // Calculate variance
        double sum_sq_diff = 0.0;
        size_t valid_count = 0;
        for (size_t i = start_idx; i < returns.size(); ++i) {
            if (!std::isfinite(returns[i])) {
                continue;
            }
            double diff = returns[i] - mean;
            sum_sq_diff += diff * diff;
            valid_count++;
        }
        
        if (valid_count < MIN_RETURNS_FOR_HISTORICAL) {
            return DEFAULT_VOLATILITY;
        }
        
        double variance = sum_sq_diff / (valid_count - 1); 
        
        if (!std::isfinite(variance) || variance <= 0) {
            return DEFAULT_VOLATILITY;
        }
        
        return std::sqrt(variance) * std::sqrt(252.0);
    }
};

// --- GlobalVolatilityForecaster Implementation ---
GlobalVolatilityForecaster::GlobalVolatilityForecaster(QuantLibGARCHModel& garch_model,
                                                       Core::MemoryPool& mem_pool)
    : garch_model_(garch_model), mem_pool_(mem_pool) {
    
    try {
        internal_forecaster_ = std::make_unique<VolatilityForecaster>(garch_model_, mem_pool_);
    } catch (const std::exception& e) {
        throw std::runtime_error("Failed to initialize GlobalVolatilityForecaster: " + std::string(e.what()));
    }
}

GlobalVolatilityForecaster::~GlobalVolatilityForecaster() = default;

double GlobalVolatilityForecaster::generate_ensemble_forecast(size_t horizon, 
                                                             const std::vector<double>& returns) const {
    if (!internal_forecaster_) {
        forecast_errors_++;
        throw std::runtime_error("GlobalVolatilityForecaster not properly initialized");
    }
    
    try {
        forecast_calls_++;
        return internal_forecaster_->generate_forecast(horizon, returns);
    } catch (const std::exception& e) {
        forecast_errors_++;
        throw std::runtime_error("Ensemble forecast failed: " + std::string(e.what()));
    }
}

std::vector<double> GlobalVolatilityForecaster::generate_ensemble_forecast_path(size_t horizon,
                                                                               const std::vector<double>& returns) const {
    if (!internal_forecaster_) {
        forecast_errors_++;
        throw std::runtime_error("GlobalVolatilityForecaster not properly initialized");
    }
    
    try {
        forecast_calls_++;
        return internal_forecaster_->generate_forecast_path(horizon, returns);
    } catch (const std::exception& e) {
        forecast_errors_++;
        throw std::runtime_error("Ensemble forecast path failed: " + std::string(e.what()));
    }
}

void GlobalVolatilityForecaster::update_ensemble_weights(double garch_accuracy, double historical_accuracy) {
    if (!internal_forecaster_) {
        throw std::runtime_error("GlobalVolatilityForecaster not properly initialized");
    }
    
    std::vector<double> accuracies = {garch_accuracy, historical_accuracy};
    internal_forecaster_->update_model_weights(accuracies);
}

std::pair<size_t, double> GlobalVolatilityForecaster::get_performance_stats() const {
    double error_rate = (forecast_calls_ > 0) ? static_cast<double>(forecast_errors_) / forecast_calls_ : 0.0;
    return {forecast_calls_, error_rate};
}

void GlobalVolatilityForecaster::reset_performance_stats() {
    forecast_calls_ = 0;
    forecast_errors_ = 0;
}

bool GlobalVolatilityForecaster::is_healthy() const {
    if (!internal_forecaster_) {
        return false;
    }
    
    if (forecast_calls_ > 10) {
        double error_rate = static_cast<double>(forecast_errors_) / forecast_calls_;
        if (error_rate > 0.1) {
            return false;
        }
    }
    
    return internal_forecaster_->is_healthy();
}

// --- Global Forecaster Instance and Management ---
static std::unique_ptr<VolatilityForecaster> global_forecaster = nullptr;
static std::mutex global_forecaster_mutex;

void initialize_volatility_forecaster(QuantLibGARCHModel& garch_model, 
                                     Core::MemoryPool& mem_pool) {
    std::lock_guard<std::mutex> lock(global_forecaster_mutex);
    
    try {
        global_forecaster = std::make_unique<VolatilityForecaster>(garch_model, mem_pool);
    } catch (const std::exception& e) {
        throw std::runtime_error("Failed to initialize global volatility forecaster: " + std::string(e.what()));
    }
}

// --- Public API Functions ---
bool validate_forecast_parameters(size_t horizon, const std::vector<double>& returns) {
    if (horizon == 0 || horizon > 1000) {
        return false;
    }
    
    if (returns.size() > 10000) {
        return false;
    }
    
    for (double ret : returns) {
        if (!std::isfinite(ret)) {
            return false;
        }
        if (std::abs(ret) > 1.0) {
            return false;
        }
    }
    
    return true;
}

double forecast_volatility_ensemble(size_t horizon, const std::vector<double>& returns) {
    std::lock_guard<std::mutex> lock(global_forecaster_mutex);
    
    if (!validate_forecast_parameters(horizon, returns)) {
        throw std::invalid_argument("Invalid forecast parameters");
    }
    
    if (!global_forecaster) {
        if (returns.empty()) return 0.20;
        size_t lookback = std::min(returns.size(), static_cast<size_t>(30));
        if (lookback <= 1) return 0.20;
        
        double temp_sum = 0.0;
        size_t temp_start_idx = returns.size() - lookback;
        for (size_t i = temp_start_idx; i < returns.size(); ++i) temp_sum += returns[i];
        double temp_mean = temp_sum / lookback;
        double temp_sum_sq_diff = 0.0;
        for (size_t i = temp_start_idx; i < returns.size(); ++i) {
            double diff = returns[i] - temp_mean;
            temp_sum_sq_diff += diff * diff;
        }
        double temp_variance = temp_sum_sq_diff / (lookback - 1);
        return std::sqrt(std::max(temp_variance, 1e-8)) * std::sqrt(252.0);
    }
    
    return global_forecaster->generate_forecast(horizon, returns);
}

std::vector<double> forecast_volatility_path_ensemble(size_t horizon, 
                                                     const std::vector<double>& returns) {
    std::lock_guard<std::mutex> lock(global_forecaster_mutex);
    
    if (!validate_forecast_parameters(horizon, returns)) {
        throw std::invalid_argument("Invalid forecast parameters");
    }
    
    if (!global_forecaster) {
        double flat_vol = 0.20;
        if (!returns.empty()) {
            try {
                flat_vol = forecast_volatility_ensemble(1, returns);
            } catch (...) {
                flat_vol = 0.20;
            }
        }
        return std::vector<double>(horizon, flat_vol);
    }
    
    return global_forecaster->generate_forecast_path(horizon, returns);
}

double calculate_forecast_confidence(const std::vector<double>& recent_forecasts,
                                   const std::vector<double>& realized_values) {
    if (recent_forecasts.size() != realized_values.size()) {
        throw std::invalid_argument("Forecast and realized value vectors must have the same size");
    }
    
    if (recent_forecasts.empty()) {
        return 0.5;
    }
    
    double total_abs_percentage_error = 0.0;
    size_t valid_observations = 0;
    
    for (size_t i = 0; i < recent_forecasts.size(); ++i) {
        if (!std::isfinite(recent_forecasts[i]) || !std::isfinite(realized_values[i])) {
            continue;
        }
        
        if (std::abs(realized_values[i]) > 1e-9) {
            double error = std::abs((recent_forecasts[i] - realized_values[i]) / realized_values[i]);
            error = std::min(error, 2.0);
            total_abs_percentage_error += error;
            valid_observations++;
        }
    }
    
    if (valid_observations == 0) {
        return 0.5;
    }
    
    double mape = total_abs_percentage_error / valid_observations;
    double confidence = 1.0 - mape;
    confidence = std::max(0.1, std::min(0.95, confidence));
    
    return confidence;
}

bool is_forecasting_system_healthy() {
    std::lock_guard<std::mutex> lock(global_forecaster_mutex);
    
    if (!global_forecaster) {
        return false;
    }
    
    return global_forecaster->is_healthy();
}

} // namespace Alaris::Volatility
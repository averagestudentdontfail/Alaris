#include "vol_forecast.h"
#include "garch_wrapper.h" 
#include "../core/memory_pool.h"
#include <algorithm>
#include <numeric>
#include <cmath>
#include <memory>
#include <stdexcept>
#include <mutex>
#include <limits>

namespace Alaris::Volatility {

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
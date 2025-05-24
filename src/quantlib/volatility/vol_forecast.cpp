#include "gjrgarch_wrapper.h"
#include <algorithm>
#include <numeric>
#include <cmath>

namespace Alaris::Volatility {

class VolatilityForecaster {
private:
    QuantLibGJRGARCHModel& gjr_model_;
    Core::MemoryPool& mem_pool_;
    
    // Ensemble weights
    std::vector<double> model_weights_;
    std::vector<double> forecast_accuracy_;
    
public:
    explicit VolatilityForecaster(QuantLibGJRGARCHModel& gjr_model, 
                                 Core::MemoryPool& mem_pool)
        : gjr_model_(gjr_model), mem_pool_(mem_pool) {
        
        // Initialize with equal weights
        model_weights_ = {0.6, 0.4}; // GJR-GARCH, Historical
        forecast_accuracy_ = {0.5, 0.5};
    }
    
    double generate_forecast(size_t horizon, const std::vector<double>& returns) {
        // GJR-GARCH forecast
        double gjr_forecast = gjr_model_.forecast_volatility(horizon);
        
        // Historical volatility forecast
        double hist_forecast = calculate_historical_volatility(returns, horizon);
        
        // Ensemble forecast
        double ensemble_forecast = model_weights_[0] * gjr_forecast + 
                                  model_weights_[1] * hist_forecast;
        
        return ensemble_forecast;
    }
    
    std::vector<double> generate_forecast_path(size_t horizon, 
                                              const std::vector<double>& returns) {
        std::vector<double> forecast_path;
        forecast_path.reserve(horizon);
        
        for (size_t h = 1; h <= horizon; ++h) {
            double forecast = generate_forecast(h, returns);
            forecast_path.push_back(forecast);
        }
        
        return forecast_path;
    }
    
    void update_model_weights(const std::vector<double>& recent_accuracy) {
        if (recent_accuracy.size() >= 2) {
            double total_accuracy = std::accumulate(recent_accuracy.begin(), 
                                                   recent_accuracy.end(), 0.0);
            
            if (total_accuracy > 0) {
                for (size_t i = 0; i < model_weights_.size() && i < recent_accuracy.size(); ++i) {
                    model_weights_[i] = recent_accuracy[i] / total_accuracy;
                }
            }
        }
    }
    
    double calculate_forecast_confidence(const std::vector<double>& recent_forecasts,
                                       const std::vector<double>& realized_values) {
        if (recent_forecasts.size() != realized_values.size() || 
            recent_forecasts.empty()) {
            return 0.5; // Default confidence
        }
        
        // Calculate Mean Absolute Percentage Error (MAPE)
        double total_error = 0.0;
        size_t valid_observations = 0;
        
        for (size_t i = 0; i < recent_forecasts.size(); ++i) {
            if (realized_values[i] != 0.0) {
                double error = std::abs(recent_forecasts[i] - realized_values[i]) / 
                              std::abs(realized_values[i]);
                total_error += error;
                valid_observations++;
            }
        }
        
        if (valid_observations == 0) return 0.5;
        
        double mape = total_error / valid_observations;
        
        // Convert MAPE to confidence (lower error = higher confidence)
        double confidence = std::max(0.1, 1.0 - mape);
        return std::min(0.95, confidence);
    }
    
private:
    double calculate_historical_volatility(const std::vector<double>& returns, 
                                         size_t lookback_days = 30) {
        if (returns.empty()) return 0.2; // Default volatility
        
        size_t start_idx = returns.size() > lookback_days ? 
                          returns.size() - lookback_days : 0;
        
        // Calculate sample standard deviation
        double sum = 0.0, sum_sq = 0.0;
        size_t count = 0;
        
        for (size_t i = start_idx; i < returns.size(); ++i) {
            sum += returns[i];
            sum_sq += returns[i] * returns[i];
            count++;
        }
        
        if (count <= 1) return 0.2;
        
        double mean = sum / count;
        double variance = (sum_sq / count) - (mean * mean);
        
        // Annualized volatility (assuming daily returns)
        return std::sqrt(std::max(variance, 1e-8) * 252.0);
    }
};

// Global forecaster instance management
static std::unique_ptr<VolatilityForecaster> global_forecaster = nullptr;

void initialize_volatility_forecaster(QuantLibGJRGARCHModel& gjr_model, 
                                     Core::MemoryPool& mem_pool) {
    global_forecaster = std::make_unique<VolatilityForecaster>(gjr_model, mem_pool);
}

double forecast_volatility_ensemble(size_t horizon, const std::vector<double>& returns) {
    if (!global_forecaster) {
        // Return simple historical volatility as fallback
        if (returns.empty()) return 0.2;
        
        double sum_sq = 0.0;
        for (double r : returns) {
            sum_sq += r * r;
        }
        return std::sqrt(sum_sq / returns.size() * 252.0);
    }
    
    return global_forecaster->generate_forecast(horizon, returns);
}

std::vector<double> forecast_volatility_path_ensemble(size_t horizon, 
                                                     const std::vector<double>& returns) {
    if (!global_forecaster) {
        // Return flat forecast as fallback
        double flat_vol = forecast_volatility_ensemble(1, returns);
        return std::vector<double>(horizon, flat_vol);
    }
    
    return global_forecaster->generate_forecast_path(horizon, returns);
}

} // namespace Alaris::Volatility

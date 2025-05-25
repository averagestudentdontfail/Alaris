#include "vol_forecast.h"
#include "gjrgarch_wrapper.h" // For Alaris::Volatility::QuantLibGJRGARCHModel
#include "../core/memory_pool.h" // For Alaris::Core::MemoryPool
#include <algorithm>
#include <numeric>
#include <cmath>
#include <memory> // For std::unique_ptr

namespace Alaris::Volatility {

// Definition for VolatilityForecaster class
class VolatilityForecaster {
private:
    QuantLibGJRGARCHModel& gjr_model_;    // Reference to the GJR-GARCH model
    Core::MemoryPool& mem_pool_;          // Reference to a memory pool (if needed for internal allocations)
    
    // Ensemble weights: [0] for GJR-GARCH, [1] for Historical
    std::vector<double> model_weights_;
    // Stores the recent accuracy measures for GJR-GARCH and Historical components
    std::vector<double> forecast_accuracy_tracking_;
    
public:
    explicit VolatilityForecaster(QuantLibGJRGARCHModel& gjr_model, 
                                 Core::MemoryPool& mem_pool)
        : gjr_model_(gjr_model), mem_pool_(mem_pool) {
        
        // Default initial weights for the ensemble
        model_weights_ = {0.6, 0.4}; // e.g., 60% GJR-GARCH, 40% Historical
        // Default initial accuracy (can be updated dynamically)
        forecast_accuracy_tracking_ = {0.5, 0.5}; // Neutral initial accuracy
    }
    
    double generate_forecast(size_t horizon, const std::vector<double>& returns) {
        double gjr_forecast = gjr_model_.forecast_volatility(horizon);
        double hist_forecast = calculate_historical_volatility(returns, 30); // 30-day lookback for historical

        if (model_weights_.size() >= 2) {
            return model_weights_[0] * gjr_forecast + 
                   model_weights_[1] * hist_forecast;
        }
        // Fallback if model_weights_ is not properly sized (should not happen with proper construction)
        return gjr_forecast; 
    }
    
    std::vector<double> generate_forecast_path(size_t horizon, 
                                              const std::vector<double>& returns) {
        std::vector<double> forecast_path;
        if (horizon == 0) {
            return forecast_path;
        }
        forecast_path.reserve(horizon);
        
        // Generate forecast for each step in the horizon
        for (size_t h = 1; h <= horizon; ++h) {
            forecast_path.push_back(generate_forecast(h, returns));
        }
        return forecast_path;
    }
    
    /**
     * @brief Updates the weights of the ensemble components based on recent accuracy.
     * @param recent_accuracy A vector where recent_accuracy[0] is GJR-GARCH accuracy,
     * and recent_accuracy[1] is Historical accuracy.
     */
    void update_model_weights(const std::vector<double>& recent_accuracy_inputs) {
        if (recent_accuracy_inputs.size() >= 2 && model_weights_.size() >= 2 && forecast_accuracy_tracking_.size() >=2) {
            // Update internal tracking of accuracies
            forecast_accuracy_tracking_[0] = recent_accuracy_inputs[0]; // GJR Accuracy
            forecast_accuracy_tracking_[1] = recent_accuracy_inputs[1]; // Historical Accuracy

            double total_accuracy = forecast_accuracy_tracking_[0] + forecast_accuracy_tracking_[1];
            
            if (total_accuracy > 1e-6) { // Avoid division by zero if both accuracies are zero
                model_weights_[0] = forecast_accuracy_tracking_[0] / total_accuracy;
                model_weights_[1] = forecast_accuracy_tracking_[1] / total_accuracy;
            } else {
                // Fallback to equal weights if total accuracy is zero
                model_weights_[0] = 0.5;
                model_weights_[1] = 0.5;
            }
        }
    }
    
private:
    /**
     * @brief Calculates historical volatility from a series of returns.
     * @param returns Vector of (typically daily) returns.
     * @param lookback_days Number of days to include in the calculation.
     * @return Annualized historical volatility.
     */
    double calculate_historical_volatility(const std::vector<double>& returns, 
                                         size_t lookback_days = 30) {
        if (returns.empty()) {
            return 0.20; // Default volatility (20% annualized) if no returns data
        }
        
        size_t actual_lookback = std::min(returns.size(), lookback_days);
        
        if (actual_lookback <= 1) {
            // Not enough data for standard deviation; could return last return's magnitude or default
            return 0.20; 
        }
        
        size_t start_idx = returns.size() - actual_lookback;
        
        double sum = 0.0;
        for (size_t i = start_idx; i < returns.size(); ++i) {
            sum += returns[i];
        }
        double mean = sum / actual_lookback;
        
        double sum_sq_diff = 0.0;
        for (size_t i = start_idx; i < returns.size(); ++i) {
            sum_sq_diff += (returns[i] - mean) * (returns[i] - mean);
        }
        
        // Use (N-1) for sample variance, which is standard for historical volatility estimation
        double variance = sum_sq_diff / (actual_lookback - 1); 
        
        // Annualized volatility (assuming daily returns, 252 trading days a year)
        return std::sqrt(std::max(variance, 1e-8)) * std::sqrt(252.0);
    }
};

// --- Global Forecaster Instance and Management ---
static std::unique_ptr<VolatilityForecaster> global_forecaster = nullptr;

void initialize_volatility_forecaster(QuantLibGJRGARCHModel& gjr_model, 
                                     Core::MemoryPool& mem_pool) {
    // Create or replace the global forecaster instance
    global_forecaster = std::make_unique<VolatilityForecaster>(gjr_model, mem_pool);
}

// --- Public API Functions (using the global forecaster) ---
double forecast_volatility_ensemble(size_t horizon, const std::vector<double>& returns) {
    if (!global_forecaster) {
        // Fallback if forecaster not initialized: calculate simple historical volatility.
        // This logic is a simplified version of VolatilityForecaster::calculate_historical_volatility
        if (returns.empty()) return 0.20;
        size_t lookback = std::min(returns.size(), static_cast<size_t>(30));
        if (lookback <= 1) return 0.20;
        
        double temp_sum = 0.0;
        size_t temp_start_idx = returns.size() - lookback;
        for (size_t i = temp_start_idx; i < returns.size(); ++i) temp_sum += returns[i];
        double temp_mean = temp_sum / lookback;
        double temp_sum_sq_diff = 0.0;
        for (size_t i = temp_start_idx; i < returns.size(); ++i) temp_sum_sq_diff += (returns[i] - temp_mean) * (returns[i] - temp_mean);
        double temp_variance = temp_sum_sq_diff / (lookback - 1);
        return std::sqrt(std::max(temp_variance, 1e-8)) * std::sqrt(252.0);
    }
    return global_forecaster->generate_forecast(horizon, returns);
}

std::vector<double> forecast_volatility_path_ensemble(size_t horizon, 
                                                     const std::vector<double>& returns) {
    if (!global_forecaster) {
        // Fallback: flat forecast path based on the simple historical volatility fallback
        double flat_vol = forecast_volatility_ensemble(1, returns); // Horizon 1 for single point
        return std::vector<double>(horizon, flat_vol);
    }
    return global_forecaster->generate_forecast_path(horizon, returns);
}

double calculate_forecast_confidence(const std::vector<double>& recent_forecasts,
                                   const std::vector<double>& realized_values) {
    if (recent_forecasts.size() != realized_values.size() || recent_forecasts.empty()) {
        return 0.5; // Default confidence for invalid input
    }
    
    double total_abs_percentage_error = 0.0;
    size_t valid_observations = 0;
    
    for (size_t i = 0; i < recent_forecasts.size(); ++i) {
        if (std::abs(realized_values[i]) > 1e-9) { // Avoid division by zero or near-zero
            total_abs_percentage_error += std::abs((recent_forecasts[i] - realized_values[i]) / realized_values[i]);
            valid_observations++;
        }
    }
    
    if (valid_observations == 0) {
        return 0.5; // Default if no valid observations for MAPE
    }
    
    double mape = total_abs_percentage_error / valid_observations;
    
    // Convert MAPE to a confidence score (e.g., 1 - MAPE, bounded)
    // A lower MAPE indicates higher confidence.
    double confidence = 1.0 - mape;
    
    // Bound confidence to a reasonable range, e.g., [0.1, 0.95]
    confidence = std::max(0.1, std::min(0.95, confidence));
    
    return confidence;
}

} // namespace Alaris::Volatility
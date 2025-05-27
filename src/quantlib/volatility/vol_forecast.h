#pragma once

#include "garch_wrapper.h" // Now uses standard GARCH instead of GJR-GARCH
#include "../core/memory_pool.h"
#include <vector>
#include <memory>

namespace Alaris::Volatility {

// Forward declaration for the VolatilityForecaster class.
class VolatilityForecaster;

/**
 * @brief Global volatility forecaster wrapper class for strategy integration.
 * 
 * This class provides a high-level interface for volatility forecasting
 * that combines multiple models and provides production-grade error handling.
 */
class GlobalVolatilityForecaster {
private:
    std::unique_ptr<VolatilityForecaster> internal_forecaster_;
    QuantLibGARCHModel& garch_model_;  // Changed from GJR-GARCH to standard GARCH
    Core::MemoryPool& mem_pool_;
    
    // Performance tracking
    mutable size_t forecast_calls_ = 0;
    mutable size_t forecast_errors_ = 0;

public:
    /**
     * @brief Constructs a global volatility forecaster.
     * @param garch_model Reference to a configured GARCH model.
     * @param mem_pool Reference to a memory pool for allocations.
     */
    explicit GlobalVolatilityForecaster(QuantLibGARCHModel& garch_model,
                                       Core::MemoryPool& mem_pool);
    
    ~GlobalVolatilityForecaster();
    
    // Non-copyable, non-movable for safety
    GlobalVolatilityForecaster(const GlobalVolatilityForecaster&) = delete;
    GlobalVolatilityForecaster& operator=(const GlobalVolatilityForecaster&) = delete;
    GlobalVolatilityForecaster(GlobalVolatilityForecaster&&) = delete;
    GlobalVolatilityForecaster& operator=(GlobalVolatilityForecaster&&) = delete;
    
    /**
     * @brief Generates an ensemble volatility forecast.
     */
    double generate_ensemble_forecast(size_t horizon, const std::vector<double>& returns) const;
    
    /**
     * @brief Generates a path of ensemble volatility forecasts.
     */
    std::vector<double> generate_ensemble_forecast_path(size_t horizon,
                                                       const std::vector<double>& returns) const;
    
    /**
     * @brief Updates the ensemble weights based on recent performance.
     */
    void update_ensemble_weights(double garch_accuracy, double historical_accuracy);
    
    /**
     * @brief Gets performance statistics for the forecaster.
     */
    std::pair<size_t, double> get_performance_stats() const;
    
    /**
     * @brief Resets performance statistics.
     */
    void reset_performance_stats();
    
    /**
     * @brief Checks if the forecaster is in a healthy state.
     */
    bool is_healthy() const;
};

/**
 * @brief Initializes the global volatility forecaster with standard GARCH.
 */
void initialize_volatility_forecaster(QuantLibGARCHModel& garch_model,
                                     Core::MemoryPool& mem_pool);

/**
 * @brief Forecasts volatility using an ensemble approach (GARCH + Historical).
 */
double forecast_volatility_ensemble(size_t horizon, const std::vector<double>& returns);

/**
 * @brief Forecasts a path of volatilities using an ensemble approach.
 */
std::vector<double> forecast_volatility_path_ensemble(size_t horizon,
                                                     const std::vector<double>& returns);

/**
 * @brief Calculates a confidence score for volatility forecasts.
 */
double calculate_forecast_confidence(const std::vector<double>& recent_forecasts,
                                   const std::vector<double>& realized_values);

/**
 * @brief Validates forecast input parameters.
 */
bool validate_forecast_parameters(size_t horizon, const std::vector<double>& returns);

/**
 * @brief Gets the health status of the global volatility forecasting system.
 */
bool is_forecasting_system_healthy();

} // namespace Alaris::Volatility
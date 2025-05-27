#pragma once

#include "gjrgarch_wrapper.h" // Provides Alaris::Volatility::QuantLibGJRGARCHModel
                              // and transitively Alaris::Core::MemoryPool
#include <vector>
#include <memory>

namespace Alaris::Volatility {

// Forward declaration for the VolatilityForecaster class.
// The actual definition is in vol_forecast.cpp.
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
    QuantLibGJRGARCHModel& gjr_model_;
    Alaris::Core::MemoryPool& mem_pool_;
    
    // Performance tracking
    mutable size_t forecast_calls_ = 0;
    mutable size_t forecast_errors_ = 0;

public:
    /**
     * @brief Constructs a global volatility forecaster.
     * @param gjr_model Reference to a configured GJR-GARCH model.
     * @param mem_pool Reference to a memory pool for allocations.
     */
    explicit GlobalVolatilityForecaster(QuantLibGJRGARCHModel& gjr_model,
                                       Alaris::Core::MemoryPool& mem_pool);
    
    /**
     * @brief Destructor.
     */
    ~GlobalVolatilityForecaster();
    
    // Non-copyable, non-movable for safety
    GlobalVolatilityForecaster(const GlobalVolatilityForecaster&) = delete;
    GlobalVolatilityForecaster& operator=(const GlobalVolatilityForecaster&) = delete;
    GlobalVolatilityForecaster(GlobalVolatilityForecaster&&) = delete;
    GlobalVolatilityForecaster& operator=(GlobalVolatilityForecaster&&) = delete;
    
    /**
     * @brief Generates an ensemble volatility forecast.
     * @param horizon The forecast horizon (number of steps ahead).
     * @param returns A vector of recent returns data for the historical component.
     * @return The ensemble volatility forecast, or a default value on error.
     * @throws std::runtime_error on critical failures.
     */
    double generate_ensemble_forecast(size_t horizon, const std::vector<double>& returns) const;
    
    /**
     * @brief Generates a path of ensemble volatility forecasts.
     * @param horizon The total forecast horizon for the path.
     * @param returns A vector of recent returns data.
     * @return A vector containing the volatility forecast for each step up to the horizon.
     * @throws std::runtime_error on critical failures.
     */
    std::vector<double> generate_ensemble_forecast_path(size_t horizon,
                                                       const std::vector<double>& returns) const;
    
    /**
     * @brief Updates the ensemble weights based on recent performance.
     * @param gjr_accuracy Recent accuracy of the GJR-GARCH model (0.0 to 1.0).
     * @param historical_accuracy Recent accuracy of the historical model (0.0 to 1.0).
     */
    void update_ensemble_weights(double gjr_accuracy, double historical_accuracy);
    
    /**
     * @brief Gets performance statistics for the forecaster.
     * @return A pair containing (total_calls, error_rate).
     */
    std::pair<size_t, double> get_performance_stats() const;
    
    /**
     * @brief Resets performance statistics.
     */
    void reset_performance_stats();
    
    /**
     * @brief Checks if the forecaster is in a healthy state.
     * @return True if the forecaster is operational, false otherwise.
     */
    bool is_healthy() const;
};

/**
 * @brief Initializes the global volatility forecaster.
 * @param gjr_model Reference to a configured GJR-GARCH model.
 * @param mem_pool Reference to a memory pool for allocations within the forecaster.
 */
void initialize_volatility_forecaster(QuantLibGJRGARCHModel& gjr_model,
                                     Alaris::Core::MemoryPool& mem_pool);

/**
 * @brief Forecasts volatility using an ensemble approach (GJR-GARCH + Historical).
 * @param horizon The forecast horizon (number of steps ahead).
 * @param returns A vector of recent returns data for the historical component.
 * @return The ensemble volatility forecast.
 * @throws std::runtime_error if the global forecaster is not initialized.
 */
double forecast_volatility_ensemble(size_t horizon, const std::vector<double>& returns);

/**
 * @brief Forecasts a path of volatilities using an ensemble approach.
 * @param horizon The total forecast horizon for the path.
 * @param returns A vector of recent returns data.
 * @return A vector containing the volatility forecast for each step up to the horizon.
 * @throws std::runtime_error if the global forecaster is not initialized.
 */
std::vector<double> forecast_volatility_path_ensemble(size_t horizon,
                                                     const std::vector<double>& returns);

/**
 * @brief Calculates a confidence score for volatility forecasts.
 * @param recent_forecasts A vector of recent volatility forecasts.
 * @param realized_values A vector of corresponding realized volatility values.
 * @return A confidence score (between 0.0 and 1.0).
 * @throws std::invalid_argument if input vectors have different sizes.
 */
double calculate_forecast_confidence(const std::vector<double>& recent_forecasts,
                                   const std::vector<double>& realized_values);

/**
 * @brief Validates forecast input parameters.
 * @param horizon The forecast horizon to validate.
 * @param returns The returns vector to validate.
 * @return True if parameters are valid, false otherwise.
 */
bool validate_forecast_parameters(size_t horizon, const std::vector<double>& returns);

/**
 * @brief Gets the health status of the global volatility forecasting system.
 * @return True if the system is healthy and operational, false otherwise.
 */
bool is_forecasting_system_healthy();

} // namespace Alaris::Volatility
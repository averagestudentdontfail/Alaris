#pragma once

#include "gjrgarch_wrapper.h" // Provides Alaris::Volatility::QuantLibGJRGARCHModel
                              // and transitively Alaris::Core::MemoryPool
#include <vector>

namespace Alaris::Volatility {

// Forward declaration for the VolatilityForecaster class.
// The actual definition is in vol_forecast.cpp.
class VolatilityForecaster;

/**
 * @brief Initializes the global volatility forecaster.
 * @param gjr_model Reference to a configured GJR-GARCH model.
 * @param mem_pool Reference to a memory pool for allocations within the forecaster.
 */
void initialize_volatility_forecaster(QuantLibGJRGARCHModel& gjr_model,
                                     Alaris::Core::MemoryPool& mem_pool); // Ensured Alaris::Core namespace

/**
 * @brief Forecasts volatility using an ensemble approach (GJR-GARCH + Historical).
 * @param horizon The forecast horizon (number of steps ahead).
 * @param returns A vector of recent returns data for the historical component.
 * @return The ensemble volatility forecast.
 */
double forecast_volatility_ensemble(size_t horizon, const std::vector<double>& returns);

/**
 * @brief Forecasts a path of volatilities using an ensemble approach.
 * @param horizon The total forecast horizon for the path.
 * @param returns A vector of recent returns data.
 * @return A vector containing the volatility forecast for each step up to the horizon.
 */
std::vector<double> forecast_volatility_path_ensemble(size_t horizon,
                                                     const std::vector<double>& returns);

/**
 * @brief Calculates a confidence score for volatility forecasts.
 * @param recent_forecasts A vector of recent volatility forecasts.
 * @param realized_values A vector of corresponding realized volatility values.
 * @return A confidence score (e.g., between 0.0 and 1.0).
 */
double calculate_forecast_confidence(const std::vector<double>& recent_forecasts,
                                   const std::vector<double>& realized_values);

} // namespace Alaris::Volatility
#pragma once

#include "gjrgarch_wrapper.h"
#include <vector>

namespace Alaris::Volatility {

class VolatilityForecaster;

// Initialization
void initialize_volatility_forecaster(QuantLibGJRGARCHModel& gjr_model, 
                                     Core::MemoryPool& mem_pool);

// Ensemble forecasting functions
double forecast_volatility_ensemble(size_t horizon, const std::vector<double>& returns);

std::vector<double> forecast_volatility_path_ensemble(size_t horizon, 
                                                     const std::vector<double>& returns);

// Utility functions
double calculate_forecast_confidence(const std::vector<double>& recent_forecasts,
                                   const std::vector<double>& realized_values);

} // namespace Alaris::Volatility

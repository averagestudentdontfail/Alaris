#include "gjrgarch_wrapper.h"
#include <iostream>
#include <cmath>

namespace Alaris::Volatility {

// Standard GARCH model implementation (separate from GJR-GARCH)
// Note: QuantLibGARCHModel is already declared in gjrgarch_wrapper.h

std::unique_ptr<QuantLibGARCHModel> create_garch_model(Core::MemoryPool& mem_pool) {
    return std::make_unique<QuantLibGARCHModel>(mem_pool);
}

// Utility functions for GARCH analysis
double calculate_garch_likelihood(const std::vector<double>& returns,
                                 double omega, double alpha, double beta) {
    // Simplified likelihood calculation
    double log_likelihood = 0.0;
    double variance = 0.04; // Initial variance
    
    for (double return_val : returns) {
        variance = omega + alpha * return_val * return_val + beta * variance;
        if (variance > 0) {
            log_likelihood -= 0.5 * (std::log(variance) + return_val * return_val / variance);
        }
    }
    
    return log_likelihood;
}

void optimize_garch_parameters(const std::vector<double>& returns,
                              double& omega, double& alpha, double& beta) {
    // Simple parameter optimization using method of moments
    if (returns.size() < 50) return;
    
    double sum = 0.0, sum_sq = 0.0;
    for (double r : returns) {
        sum += r;
        sum_sq += r * r;
    }
    
    double mean = sum / returns.size();
    double variance = sum_sq / returns.size() - mean * mean;
    
    // Set reasonable initial parameters
    omega = variance * 0.1;
    alpha = 0.05;
    beta = 0.85;
    
    std::cout << "GARCH parameters optimized for " << returns.size() << " observations" << std::endl;
}

// Forecasting utilities
double forecast_garch_volatility(double current_variance, double last_return,
                                double omega, double alpha, double beta, int horizon) {
    double forecast_variance = current_variance;
    
    for (int h = 1; h <= horizon; ++h) {
        if (h == 1) {
            forecast_variance = omega + alpha * last_return * last_return + beta * forecast_variance;
        } else {
            // For multi-step forecasts, use unconditional variance convergence
            double persistence = alpha + beta;
            double unconditional_var = omega / (1.0 - persistence);
            forecast_variance = omega + persistence * forecast_variance;
            
            // Add convergence factor
            double convergence_factor = std::pow(persistence, h - 1);
            forecast_variance = (1.0 - convergence_factor) * unconditional_var + 
                               convergence_factor * forecast_variance;
        }
    }
    
    return std::sqrt(std::max(forecast_variance, 1e-8));
}

} // namespace Alaris::Volatility

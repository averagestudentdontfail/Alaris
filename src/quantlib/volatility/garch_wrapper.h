#pragma once

#include <memory>
#include <vector>
#include <string>
#include <stdexcept>

namespace alaris {
namespace volatility {

/**
 * @brief GARCH(1,1) model wrapper for volatility forecasting
 * 
 * This class provides a wrapper around QuantLib's GARCH implementation
 * for volatility forecasting with deterministic execution guarantees.
 */
class GarchWrapper {
public:
    /**
     * @brief Construct a new GARCH model
     * 
     * @param omega Constant term (unconditional variance)
     * @param alpha ARCH term coefficient
     * @param beta GARCH term coefficient
     */
    GarchWrapper(double omega, double alpha, double beta);

    /**
     * @brief Update the model with new return data
     * 
     * @param returns Vector of returns to update the model
     */
    void update(const std::vector<double>& returns);

    /**
     * @brief Forecast volatility for the next period
     * 
     * @return double Forecasted volatility
     */
    double forecast() const;

    /**
     * @brief Get the current model parameters
     * 
     * @return std::vector<double> [omega, alpha, beta]
     */
    std::vector<double> getParameters() const;

    /**
     * @brief Set new model parameters
     * 
     * @param omega New constant term
     * @param alpha New ARCH term coefficient
     * @param beta New GARCH term coefficient
     */
    void setParameters(double omega, double alpha, double beta);

    /**
     * @brief Get the model's current conditional variance
     * 
     * @return double Current conditional variance
     */
    double getConditionalVariance() const;

    /**
     * @brief Reset the model to its initial state
     */
    void reset();

private:
    class Impl;
    std::unique_ptr<Impl> pimpl_;  // PIMPL idiom for ABI stability
};

} // namespace volatility
} // namespace alaris 
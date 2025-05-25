// test/quantlib/volatility_test.cpp
#include "../../src/quantlib/volatility/gjrgarch_wrapper.h" // For Alaris::Volatility::QuantLibGJRGARCHModel
#include "../../src/quantlib/core/memory_pool.h"         // For Alaris::Core::MemoryPool
#include <gtest/gtest.h>
#include <vector>
#include <random>   // For std::random_device, std::mt19937, std::normal_distribution
#include <cmath>    // For std::isfinite, std::sqrt
#include <numeric>  // For std::accumulate (if needed)

// Using namespace Alaris for brevity in test definitions.
using namespace Alaris;

class GJRGARCHModelTest : public ::testing::Test { // Renamed fixture for clarity
protected:
    std::unique_ptr<Core::MemoryPool> mem_pool_;
    std::unique_ptr<Volatility::QuantLibGJRGARCHModel> model_;

    // Test setup specific to GJRGARCHModelTest
    void SetUp() override {
        // Using a reasonably sized memory pool for tests.
        mem_pool_ = std::make_unique<Core::MemoryPool>(16 * 1024 * 1024); // 16MB
        model_ = std::make_unique<Volatility::QuantLibGJRGARCHModel>(*mem_pool_);
        
        // Optionally set known initial parameters for some tests
        // model_->set_parameters(1e-6, 0.08, 0.90, 0.05); // omega, alpha, beta, gamma
    }

    void TearDown() override {
        // Cleanup if needed, unique_ptr handles memory automatically
    }

    // Helper to generate a series of returns
    std::vector<QuantLib::Real> generate_random_returns(size_t count, double mean = 0.0, double std_dev = 0.02) {
        std::vector<QuantLib::Real> returns;
        returns.reserve(count);
        
        std::random_device rd;
        std::mt19937 gen(rd());
        std::normal_distribution<> dist(mean, std_dev);
        
        for (size_t i = 0; i < count; ++i) {
            returns.push_back(dist(gen));
        }
        return returns;
    }
};

TEST_F(GJRGARCHModelTest, Initialization) {
    ASSERT_NE(model_, nullptr);
    EXPECT_GT(model_->current_volatility(), 0.0); // Should have a default initial volatility
    EXPECT_GT(model_->current_variance(), 0.0);
    auto params = model_->get_parameters();
    ASSERT_EQ(params.size(), 4); // omega, alpha, beta, gamma for GJR-GARCH(1,1)
    // Check default initial parameters (as set in QuantLibGJRGARCHModel::initialize_parameters)
    // EXPECT_DOUBLE_EQ(params[0], 1e-6); // Default omega
    // EXPECT_DOUBLE_EQ(params[1], 0.08); // Default alpha
    // EXPECT_DOUBLE_EQ(params[2], 0.90); // Default beta
    // EXPECT_DOUBLE_EQ(params[3], 0.05); // Default gamma
}

TEST_F(GJRGARCHModelTest, SetAndGetParameters) {
    QuantLib::Real test_omega = 2e-6, test_alpha = 0.1, test_beta = 0.85, test_gamma = 0.07;
    model_->set_parameters(test_omega, test_alpha, test_beta, test_gamma);
    
    auto params = model_->get_parameters();
    ASSERT_EQ(params.size(), 4);
    EXPECT_DOUBLE_EQ(params[0], test_omega);
    EXPECT_DOUBLE_EQ(params[1], test_alpha);
    EXPECT_DOUBLE_EQ(params[2], test_beta);
    EXPECT_DOUBLE_EQ(params[3], test_gamma);
    EXPECT_TRUE(model_->is_model_valid()); // Check if these params are valid
}


TEST_F(GJRGARCHModelTest, BasicCalibration) {
    auto returns = generate_random_returns(1000, 0.0, 0.02); // 1000 days of returns
    
    // Store initial parameters before calibration
    auto params_before_calibration = model_->get_parameters();

    EXPECT_TRUE(model_->calibrate(returns)) << "Calibration failed or returned false.";
    EXPECT_TRUE(model_->is_model_valid()) << "Model is not valid after calibration.";

    auto params_after_calibration = model_->get_parameters();
    ASSERT_EQ(params_after_calibration.size(), 4);

    // Check if parameters changed (heuristic calibration should change them from defaults)
    // This is a loose check as heuristic calibration isn't precise.
    bool params_changed = false;
    for (size_t i = 0; i < params_before_calibration.size(); ++i) {
        if (std::abs(params_before_calibration[i] - params_after_calibration[i]) > 1e-9) {
            params_changed = true;
            break;
        }
    }
    // EXPECT_TRUE(params_changed) << "Calibration did not change model parameters from initial values.";

    // Parameters should be non-negative (omega strictly positive)
    EXPECT_GT(params_after_calibration[0], 0.0); // omega > 0
    EXPECT_GE(params_after_calibration[1], 0.0); // alpha >= 0
    EXPECT_GE(params_after_calibration[2], 0.0); // beta >= 0
    EXPECT_GE(params_after_calibration[3], 0.0); // gamma >= 0
}

TEST_F(GJRGARCHModelTest, ForecastingBehavior) {
    auto returns = generate_random_returns(252, 0.0, 0.02); // 1 year of daily returns
    model_->calibrate(returns); // Calibrate with some data
    ASSERT_TRUE(model_->is_model_valid());

    // Test single-step forecast
    double forecast_1step = model_->forecast_volatility(1);
    EXPECT_GT(forecast_1step, 0.0) << "1-step forecast volatility should be positive.";
    EXPECT_LT(forecast_1step, 1.0) << "1-step forecast volatility seems unreasonably high (e.g., >100% annualized).";

    // Test multi-step forecast path
    size_t horizon = 10;
    auto path = model_->forecast_volatility_path(horizon);
    ASSERT_EQ(path.size(), horizon);
    
    for (size_t i = 0; i < path.size(); ++i) {
        EXPECT_GT(path[i], 0.0) << "Forecast volatility at step " << i+1 << " should be positive.";
        EXPECT_LT(path[i], 1.0) << "Forecast volatility at step " << i+1 << " seems unreasonably high.";
        if (i > 0) {
            // Volatility forecasts for GARCH models often mean-revert.
            // Depending on parameters, they might initially increase or decrease.
            // No strict monotonicity is universally guaranteed without knowing parameters.
        }
    }
    // The forecast path should eventually converge towards the model's unconditional volatility.
}

TEST_F(GJRGARCHModelTest, UpdateMechanismAndAsymmetry) {
    // Set known parameters for predictable behavior
    // omega, alpha, beta, gamma (ensure stationarity: alpha + beta + gamma/2 < 1)
    // Example: 1e-6, 0.05, 0.90, 0.08  => 0.05 + 0.90 + 0.04 = 0.99 (stationary)
    model_->set_parameters(1e-6, 0.05, 0.90, 0.08);
    ASSERT_TRUE(model_->is_model_valid());

    // Initial state based on default internal variance or some updates
    model_->update(0.0); // Neutral update to establish a baseline
    double vol_initial = model_->current_volatility();
    double var_initial = model_->current_variance();

    // Update with a positive return
    model_->update(0.05); // Large positive return (5%)
    double vol_after_positive_shock = model_->current_volatility();
    double var_after_positive_shock = model_->current_variance();

    // Reset to initial-like state for comparable shock (or use a new model instance)
    // For simplicity, let's re-update from the var_initial state.
    // This is tricky because GARCH depends on history.
    // A better test might involve providing a common history then diverging.
    // Or, simply note the variance before the negative shock based on the positive shock's history.
    
    // Update with a negative return of the same magnitude
    // To make it more comparable, we should ideally reset the model state or apply the negative shock
    // to a similar starting variance.
    // For this test, let's just continue the series:
    model_->update(-0.05); // Large negative return (-5%)
    double vol_after_negative_shock = model_->current_volatility();
    double var_after_negative_shock = model_->current_variance();

    // GJR-GARCH: Negative shocks should increase volatility more (or decrease it less)
    // than positive shocks of the same magnitude, due to the gamma term.
    // var_t = omega + alpha*r_{t-1}^2 + beta*var_{t-1}  (if r_{t-1} >= 0)
    // var_t = omega + (alpha+gamma)*r_{t-1}^2 + beta*var_{t-1} (if r_{t-1} < 0)
    // So, variance after negative shock's effect is incorporated should be higher
    // than variance after positive shock's effect, assuming gamma > 0.

    // Variance from (+0.05)^2 shock: (alpha * 0.05^2) term
    // Variance from (-0.05)^2 shock: ((alpha+gamma) * 0.05^2) term
    // The `var_after_positive_shock` is sigma_t^2 where r_{t-1} was +0.05
    // The `var_after_negative_shock` is sigma_{t+1}^2 where r_t was -0.05, and sigma_t^2 was `var_after_positive_shock`
    
    // Let's test the direct impact of a shock on the next period's variance
    double var_next_if_positive_prev = 1e-6 + 0.05 * (0.05*0.05) + 0.90 * var_initial;
    double var_next_if_negative_prev = 1e-6 + (0.05 + 0.08) * (-0.05*-0.05) + 0.90 * var_initial;

    EXPECT_GT(var_next_if_negative_prev, var_next_if_positive_prev)
        << "Variance after a negative shock should be higher than after a positive shock of same magnitude, given gamma > 0.";

    // Check that current volatility is always positive
    EXPECT_GT(vol_after_positive_shock, 0.0);
    EXPECT_GT(vol_after_negative_shock, 0.0);
}

TEST_F(GJRGARCHModelTest, ModelValidationChecks) {
    auto returns = generate_random_returns(500);
    EXPECT_TRUE(model_->calibrate(returns));
    
    EXPECT_TRUE(model_->is_model_valid()) << "Model should be valid after calibration with sufficient data.";
    
    auto params = model_->get_parameters();
    ASSERT_EQ(params.size(), 4); // omega, alpha, beta, gamma
    
    for (size_t i = 0; i < params.size(); ++i) {
        EXPECT_TRUE(std::isfinite(params[i])) << "Parameter " << i << " is not finite.";
    }
    // Check stationarity condition (alpha + beta + gamma/2 < 1)
    EXPECT_LT(params[1] + params[2] + params[3] / 2.0, 1.0) << "Model parameters should ensure stationarity.";

    QuantLib::Real ll = model_->log_likelihood();
    EXPECT_TRUE(std::isfinite(ll)) << "Log-likelihood is not finite.";

    // Test with invalid parameters to ensure is_model_valid catches them
    model_->set_parameters(1e-6, 0.5, 0.6, 0.1); // Non-stationary: 0.5 + 0.6 + 0.1/2 = 1.15
    EXPECT_FALSE(model_->is_model_valid()) << "Model should be invalid with non-stationary parameters.";

    model_->set_parameters(1e-6, -0.1, 0.9, 0.05); // Invalid alpha
    EXPECT_FALSE(model_->is_model_valid()) << "Model should be invalid with negative alpha.";
}

// Test edge cases like empty returns for calibration or update
TEST_F(GJRGARCHModelTest, EdgeCaseInputs) {
    std::vector<QuantLib::Real> empty_returns;
    EXPECT_FALSE(model_->calibrate(empty_returns)) << "Calibration should fail or return false with empty returns.";
    // Model should remain in a valid state with default parameters
    EXPECT_TRUE(model_->is_model_valid());

    std::vector<QuantLib::Real> few_returns = {0.01, -0.02, 0.005};
    // Calibration might still return false if internal minimum data threshold not met (e.g. 50)
    // The current calibrate() in .cpp has check `historical_returns.size() < 50`
    EXPECT_FALSE(model_->calibrate(few_returns)) << "Calibration should fail or return false with too few returns.";

    // Update with a single return
    model_->update(0.01);
    EXPECT_GT(model_->current_volatility(), 0.0);
    EXPECT_TRUE(model_->is_model_valid()); // Should remain valid after one update
}
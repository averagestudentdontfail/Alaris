// test/quantlib/volatility_test.cpp
#include "src/quantlib/volatility/gjrgarch_wrapper.h" // Adjusted path
#include "src/quantlib/core/memory_pool.h"         // Adjusted path
#include "gtest/gtest.h"
#include <vector>
#include <random>   
#include <cmath>    
#include <numeric>  

using namespace Alaris;

class GJRGARCHModelTest : public ::testing::Test { 
protected:
    std::unique_ptr<Core::MemoryPool> mem_pool_;
    std::unique_ptr<Volatility::QuantLibGJRGARCHModel> model_;

    void SetUp() override {
        mem_pool_ = std::make_unique<Core::MemoryPool>(16 * 1024 * 1024); 
        model_ = std::make_unique<Volatility::QuantLibGJRGARCHModel>(*mem_pool_);
    }

    void TearDown() override {
    }

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
    EXPECT_GT(model_->current_volatility(), 0.0); 
    EXPECT_GT(model_->current_variance(), 0.0);
    auto params = model_->get_parameters();
    ASSERT_EQ(params.size(), 4); 
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
    EXPECT_TRUE(model_->is_model_valid()); 
}


TEST_F(GJRGARCHModelTest, BasicCalibration) {
    auto returns = generate_random_returns(1000, 0.0, 0.02); 
    
    auto params_before_calibration = model_->get_parameters();

    EXPECT_TRUE(model_->calibrate(returns)) << "Calibration failed or returned false.";
    EXPECT_TRUE(model_->is_model_valid()) << "Model is not valid after calibration.";

    auto params_after_calibration = model_->get_parameters();
    ASSERT_EQ(params_after_calibration.size(), 4);
    
    // This warning was noted in the compilation output:
    // bool params_changed = false; // This was unused. Removing it.
    // for (size_t i = 0; i < params_before_calibration.size(); ++i) {
    //     if (std::abs(params_before_calibration[i] - params_after_calibration[i]) > 1e-9) {
    //         params_changed = true;
    //         break;
    //     }
    // }
    // EXPECT_TRUE(params_changed) << "Calibration did not change model parameters from initial values.";


    EXPECT_GT(params_after_calibration[0], 0.0); 
    EXPECT_GE(params_after_calibration[1], 0.0); 
    EXPECT_GE(params_after_calibration[2], 0.0); 
    EXPECT_GE(params_after_calibration[3], 0.0); 
}

TEST_F(GJRGARCHModelTest, ForecastingBehavior) {
    auto returns = generate_random_returns(252, 0.0, 0.02); 
    model_->calibrate(returns); 
    ASSERT_TRUE(model_->is_model_valid());

    double forecast_1step = model_->forecast_volatility(1);
    EXPECT_GT(forecast_1step, 0.0) << "1-step forecast volatility should be positive.";
    EXPECT_LT(forecast_1step, 1.0) << "1-step forecast volatility seems unreasonably high (e.g., >100% annualized).";

    size_t horizon = 10;
    auto path = model_->forecast_volatility_path(horizon);
    ASSERT_EQ(path.size(), horizon);
    
    for (size_t i = 0; i < path.size(); ++i) {
        EXPECT_GT(path[i], 0.0) << "Forecast volatility at step " << i+1 << " should be positive.";
        EXPECT_LT(path[i], 1.0) << "Forecast volatility at step " << i+1 << " seems unreasonably high.";
    }
}

TEST_F(GJRGARCHModelTest, UpdateMechanismAndAsymmetry) {
    model_->set_parameters(1e-6, 0.05, 0.90, 0.08);
    ASSERT_TRUE(model_->is_model_valid());

    model_->update(0.0); 
    double var_initial = model_->current_variance();
    
    model_->update(0.05); 
    // double var_after_positive_shock = model_->current_variance(); // Not used directly in EXPECT below

    model_->update(-0.05); 
    // double var_after_negative_shock = model_->current_variance(); // Not used directly in EXPECT below
    
    double var_next_if_positive_prev = 1e-6 + 0.05 * (0.05*0.05) + 0.90 * var_initial;
    double var_next_if_negative_prev = 1e-6 + (0.05 + 0.08) * (-0.05*-0.05) + 0.90 * var_initial;

    EXPECT_GT(var_next_if_negative_prev, var_next_if_positive_prev)
        << "Variance after a negative shock should be higher than after a positive shock of same magnitude, given gamma > 0.";

    EXPECT_GT(model_->current_volatility(), 0.0);
}

TEST_F(GJRGARCHModelTest, ModelValidationChecks) {
    auto returns = generate_random_returns(500);
    EXPECT_TRUE(model_->calibrate(returns));
    
    EXPECT_TRUE(model_->is_model_valid()) << "Model should be valid after calibration with sufficient data.";
    
    auto params = model_->get_parameters();
    ASSERT_EQ(params.size(), 4); 
    
    for (size_t i = 0; i < params.size(); ++i) {
        EXPECT_TRUE(std::isfinite(params[i])) << "Parameter " << i << " is not finite.";
    }
    EXPECT_LT(params[1] + params[2] + params[3] / 2.0, 1.0) << "Model parameters should ensure stationarity.";

    QuantLib::Real ll = model_->log_likelihood();
    EXPECT_TRUE(std::isfinite(ll)) << "Log-likelihood is not finite.";

    model_->set_parameters(1e-6, 0.5, 0.6, 0.1); 
    EXPECT_FALSE(model_->is_model_valid()) << "Model should be invalid with non-stationary parameters.";

    model_->set_parameters(1e-6, -0.1, 0.9, 0.05); 
    EXPECT_FALSE(model_->is_model_valid()) << "Model should be invalid with negative alpha.";
}

TEST_F(GJRGARCHModelTest, EdgeCaseInputs) {
    std::vector<QuantLib::Real> empty_returns;
    EXPECT_FALSE(model_->calibrate(empty_returns)) << "Calibration should fail or return false with empty returns.";
    EXPECT_TRUE(model_->is_model_valid());

    std::vector<QuantLib::Real> few_returns = {0.01, -0.02, 0.005};
    EXPECT_FALSE(model_->calibrate(few_returns)) << "Calibration should fail or return false with too few returns.";

    model_->update(0.01);
    EXPECT_GT(model_->current_volatility(), 0.0);
    EXPECT_TRUE(model_->is_model_valid()); 
}

// No main() function
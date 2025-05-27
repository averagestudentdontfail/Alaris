// test/quantlib/volatility_test.cpp
#include "src/quantlib/volatility/garch_wrapper.h"
#include "src/quantlib/core/memory_pool.h"
#include "gtest/gtest.h"
#include <vector>
#include <random>
#include <cmath>
#include <numeric>

using namespace Alaris;

class QuantLibGARCHModelTest : public ::testing::Test {
protected:
    std::unique_ptr<Core::MemoryPool> mem_pool_;
    std::unique_ptr<Volatility::QuantLibGARCHModel> model_;

    void SetUp() override {
        mem_pool_ = std::make_unique<Core::MemoryPool>(16 * 1024 * 1024);
        model_ = std::make_unique<Volatility::QuantLibGARCHModel>(*mem_pool_);
    }

    std::vector<QuantLib::Real> generate_garch_returns(size_t count, 
                                                      double omega = 1e-6, 
                                                      double alpha = 0.1, 
                                                      double beta = 0.85) {
        std::vector<QuantLib::Real> returns;
        returns.reserve(count);
        
        std::random_device rd;
        std::mt19937 gen(42);  // Fixed seed for reproducibility
        std::normal_distribution<> normal(0.0, 1.0);
        
        double variance = omega / (1.0 - alpha - beta);  // Unconditional variance
        
        for (size_t i = 0; i < count; ++i) {
            double epsilon = normal(gen);
            double return_val = std::sqrt(variance) * epsilon;
            returns.push_back(return_val);
            
            // Update variance for next period using GARCH equation
            variance = omega + alpha * return_val * return_val + beta * variance;
        }
        
        return returns;
    }
    
    std::vector<QuantLib::Real> generate_market_like_returns(size_t count) {
        std::vector<QuantLib::Real> returns;
        returns.reserve(count);
        
        std::random_device rd;
        std::mt19937 gen(123);  // Different seed
        std::normal_distribution<> normal(0.0, 0.015);  // ~15% annual vol
        
        for (size_t i = 0; i < count; ++i) {
            double base_return = normal(gen);
            
            // Add some volatility clustering
            if (i > 0 && std::abs(returns.back()) > 0.02) {
                base_return *= 1.5;  // Higher vol after high vol
            }
            
            returns.push_back(base_return);
        }
        
        return returns;
    }
};

TEST_F(QuantLibGARCHModelTest, InitializationAndBasicProperties) {
    ASSERT_NE(model_, nullptr);
    
    // Check initial state
    EXPECT_GT(model_->current_volatility(), 0.0);
    EXPECT_GT(model_->current_variance(), 0.0);
    EXPECT_EQ(model_->sample_size(), 0);
    EXPECT_FALSE(model_->is_calibrated());
    
    // Check initial parameters
    auto params = model_->get_parameters();
    ASSERT_EQ(params.size(), 3);  // omega, alpha, beta
    EXPECT_GT(params[0], 0.0);    // omega > 0
    EXPECT_GE(params[1], 0.0);    // alpha >= 0
    EXPECT_GE(params[2], 0.0);    // beta >= 0
    EXPECT_LT(params[1] + params[2], 1.0);  // Stationarity
    
    std::cout << "Initial parameters - Omega: " << params[0] 
              << ", Alpha: " << params[1] 
              << ", Beta: " << params[2] << std::endl;
}

TEST_F(QuantLibGARCHModelTest, ParameterValidationAndConstraints) {
    // Test valid parameters
    model_->set_parameters(2e-6, 0.08, 0.87);
    EXPECT_TRUE(model_->is_model_valid());
    EXPECT_TRUE(model_->is_stationary());
    
    auto params = model_->get_parameters();
    EXPECT_DOUBLE_EQ(params[0], 2e-6);
    EXPECT_DOUBLE_EQ(params[1], 0.08);
    EXPECT_DOUBLE_EQ(params[2], 0.87);
    
    // Test invalid parameters (non-stationary)
    model_->set_parameters(1e-6, 0.6, 0.5);  // alpha + beta > 1
    EXPECT_FALSE(model_->is_stationary());
    // Model should revert to safe defaults
    params = model_->get_parameters();
    EXPECT_LT(params[1] + params[2], 1.0);
    
    // Test negative parameters
    model_->set_parameters(-1e-6, 0.1, 0.8);  // Negative omega
    params = model_->get_parameters();
    EXPECT_GT(params[0], 0.0);  // Should be corrected
    
    std::cout << "Parameter validation passed" << std::endl;
}

TEST_F(QuantLibGARCHModelTest, CalibrationWithSyntheticData) {
    // Generate synthetic GARCH data with known parameters
    auto synthetic_returns = generate_garch_returns(500, 2e-6, 0.1, 0.85);
    
    EXPECT_TRUE(model_->calibrate(synthetic_returns));
    EXPECT_TRUE(model_->is_calibrated());
    EXPECT_TRUE(model_->is_model_valid());
    
    auto calibrated_params = model_->get_parameters();
    ASSERT_EQ(calibrated_params.size(), 3);
    
    // Parameters should be reasonable (not exact due to finite sample)
    EXPECT_GT(calibrated_params[0], 1e-8);    // Omega
    EXPECT_GT(calibrated_params[1], 0.0);     // Alpha
    EXPECT_GT(calibrated_params[2], 0.0);     // Beta
    EXPECT_LT(calibrated_params[1] + calibrated_params[2], 0.99);  // Stationary
    
    // Check model diagnostics
    double log_likelihood = model_->log_likelihood();
    EXPECT_TRUE(std::isfinite(log_likelihood));
    EXPECT_LT(log_likelihood, 0.0);  // Log-likelihood should be negative
    
    double aic = model_->aic();
    double bic = model_->bic();
    EXPECT_TRUE(std::isfinite(aic));
    EXPECT_TRUE(std::isfinite(bic));
    EXPECT_GT(bic, aic);  // BIC penalizes complexity more
    
    std::cout << "Calibrated parameters - Omega: " << calibrated_params[0] 
              << ", Alpha: " << calibrated_params[1] 
              << ", Beta: " << calibrated_params[2] << std::endl;
    std::cout << "Model fit - LogL: " << log_likelihood 
              << ", AIC: " << aic << ", BIC: " << bic << std::endl;
}

TEST_F(QuantLibGARCHModelTest, CalibrationWithMarketData) {
    // Generate more realistic market-like returns
    auto market_returns = generate_market_like_returns(1000);
    
    EXPECT_TRUE(model_->calibrate(market_returns));
    EXPECT_TRUE(model_->is_calibrated());
    
    // Verify model captured volatility clustering
    auto residuals = model_->calculate_standardized_residuals();
    EXPECT_EQ(residuals.size(), market_returns.size());
    
    // Calculate sample statistics of standardized residuals
    double mean_residual = std::accumulate(residuals.begin(), residuals.end(), 0.0) / residuals.size();
    double var_residual = 0.0;
    for (double r : residuals) {
        var_residual += (r - mean_residual) * (r - mean_residual);
    }
    var_residual /= (residuals.size() - 1);
    
    // Residuals should be approximately mean 0, variance 1
    EXPECT_NEAR(mean_residual, 0.0, 0.1);
    EXPECT_NEAR(var_residual, 1.0, 0.2);
    
    // Ljung-Box test for residual autocorrelation
    double ljung_box = model_->ljung_box_test(10);
    EXPECT_TRUE(std::isfinite(ljung_box));
    EXPECT_GE(ljung_box, 0.0);
    
    std::cout << "Residual analysis - Mean: " << mean_residual 
              << ", Variance: " << var_residual 
              << ", Ljung-Box: " << ljung_box << std::endl;
}

TEST_F(QuantLibGARCHModelTest, RealTimeUpdatesAndForecasting) {
    // Calibrate model first
    auto returns = generate_garch_returns(200);
    ASSERT_TRUE(model_->calibrate(returns));
    
    // Test real-time updates
    double initial_variance = model_->current_variance();
    double initial_volatility = model_->current_volatility();
    
    // Update with a large shock
    model_->update(0.05);  // 5% return shock
    
    double post_shock_variance = model_->current_variance();
    double post_shock_volatility = model_->current_volatility();
    
    // Variance should increase after large shock
    EXPECT_GT(post_shock_variance, initial_variance);
    EXPECT_GT(post_shock_volatility, initial_volatility);
    
    // Test forecasting
    double one_step_forecast = model_->forecast_volatility(1);
    double five_step_forecast = model_->forecast_volatility(5);
    double twenty_step_forecast = model_->forecast_volatility(20);
    
    EXPECT_GT(one_step_forecast, 0.0);
    EXPECT_GT(five_step_forecast, 0.0);
    EXPECT_GT(twenty_step_forecast, 0.0);
    
    // Long-term forecasts should converge to unconditional volatility
    auto params = model_->get_parameters();
    double unconditional_vol = std::sqrt(params[0] / (1.0 - params[1] - params[2]));
    
    // 20-step forecast should be closer to unconditional than 1-step
    EXPECT_LT(std::abs(twenty_step_forecast - unconditional_vol),
              std::abs(one_step_forecast - unconditional_vol));
    
    std::cout << "Forecasts - 1-step: " << one_step_forecast 
              << ", 5-step: " << five_step_forecast 
              << ", 20-step: " << twenty_step_forecast 
              << ", Unconditional: " << unconditional_vol << std::endl;
}

TEST_F(QuantLibGARCHModelTest, ForecastPathConsistency) {
    auto returns = generate_market_like_returns(300);
    ASSERT_TRUE(model_->calibrate(returns));
    
    size_t horizon = 10;
    auto vol_path = model_->forecast_volatility_path(horizon);
    auto var_path = model_->forecast_variance_path(horizon);
    
    ASSERT_EQ(vol_path.size(), horizon);
    ASSERT_EQ(var_path.size(), horizon);
    
    // Check consistency between volatility and variance paths
    for (size_t i = 0; i < horizon; ++i) {
        EXPECT_NEAR(vol_path[i], std::sqrt(var_path[i]), 1e-10);
        EXPECT_GT(vol_path[i], 0.0);
        
        // Individual forecasts should match path forecasts
        double individual_forecast = model_->forecast_volatility(i + 1);
        EXPECT_NEAR(vol_path[i], individual_forecast, 1e-10);
    }
    
    std::cout << "Forecast path verification passed" << std::endl;
}

TEST_F(QuantLibGARCHModelTest, BatchUpdatePerformance) {
    auto returns = generate_market_like_returns(500);
    
    // Test batch update
    auto start = std::chrono::high_resolution_clock::now();
    model_->update_batch(returns);
    auto end = std::chrono::high_resolution_clock::now();
    
    auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
    double avg_time = static_cast<double>(duration.count()) / returns.size();
    
    EXPECT_EQ(model_->sample_size(), returns.size());
    EXPECT_GT(model_->current_volatility(), 0.0);
    
    // Performance should be reasonable
    EXPECT_LT(avg_time, 50.0);  // <50 microseconds per update
    
    std::cout << "Batch update performance: " << avg_time << " μs per update" << std::endl;
}

TEST_F(QuantLibGARCHModelTest, CalibrationWithInitialGuess) {
    auto returns = generate_garch_returns(400, 3e-6, 0.12, 0.83);
    
    // Calibrate with good initial guess
    bool success = model_->calibrate_with_initial_guess(returns, 3e-6, 0.12, 0.83);
    EXPECT_TRUE(success);
    EXPECT_TRUE(model_->is_calibrated());
    
    auto params = model_->get_parameters();
    
    // Should be close to true parameters
    EXPECT_NEAR(params[0], 3e-6, 5e-6);   // Within reasonable tolerance
    EXPECT_NEAR(params[1], 0.12, 0.05);
    EXPECT_NEAR(params[2], 0.83, 0.05);
    
    // Test with poor initial guess
    model_->clear_history();
    success = model_->calibrate_with_initial_guess(returns, 1e-5, 0.5, 0.4);
    EXPECT_TRUE(success);  // Should still succeed but with different params
    
    std::cout << "Calibration with initial guess successful" << std::endl;
}

TEST_F(QuantLibGARCHModelTest, ModelComparisonAndSelection) {
    auto returns = generate_market_like_returns(600);
    
    // Calibrate model
    ASSERT_TRUE(model_->calibrate(returns));
    
    // Get comprehensive fit statistics
    auto fit_stats = model_->get_fit_statistics();
    
    EXPECT_TRUE(std::isfinite(fit_stats.log_likelihood));
    EXPECT_TRUE(std::isfinite(fit_stats.aic));
    EXPECT_TRUE(std::isfinite(fit_stats.bic));
    EXPECT_TRUE(std::isfinite(fit_stats.ljung_box_p_value));
    EXPECT_TRUE(fit_stats.is_stationary);
    EXPECT_EQ(fit_stats.sample_size, returns.size());
    
    // BIC should penalize more than AIC
    EXPECT_GT(fit_stats.bic, fit_stats.aic);
    
    std::cout << "Model fit statistics:" << std::endl;
    std::cout << "  Log-likelihood: " << fit_stats.log_likelihood << std::endl;
    std::cout << "  AIC: " << fit_stats.aic << std::endl;
    std::cout << "  BIC: " << fit_stats.bic << std::endl;
    std::cout << "  Ljung-Box: " << fit_stats.ljung_box_p_value << std::endl;
    std::cout << "  Stationary: " << fit_stats.is_stationary << std::endl;
}

TEST_F(QuantLibGARCHModelTest, MemoryManagementAndLimits) {
    // Test history length limits
    model_->set_max_history_length(100);
    
    auto returns = generate_market_like_returns(200);
    model_->update_batch(returns);
    
    // Should truncate to max length
    EXPECT_EQ(model_->sample_size(), 100);
    
    // Verify model still works correctly
    EXPECT_GT(model_->current_volatility(), 0.0);
    double forecast = model_->forecast_volatility(1);
    EXPECT_GT(forecast, 0.0);
    
    // Test clear history
    model_->clear_history();
    EXPECT_EQ(model_->sample_size(), 0);
    EXPECT_FALSE(model_->is_calibrated());
    
    std::cout << "Memory management tests passed" << std::endl;
}

TEST_F(QuantLibGARCHModelTest, EdgeCasesAndErrorHandling) {
    // Test with insufficient data
    std::vector<QuantLib::Real> few_returns = {0.01, -0.02, 0.005};
    EXPECT_TRUE(model_->calibrate(few_returns));  // Should succeed with defaults
    EXPECT_TRUE(model_->is_model_valid());
    
    // Test with empty data
    std::vector<QuantLib::Real> empty_returns;
    EXPECT_TRUE(model_->calibrate(empty_returns));  // Should use defaults
    
    // Test with extreme returns
    std::vector<QuantLib::Real> extreme_returns;
    for (int i = 0; i < 100; ++i) {
        extreme_returns.push_back((i % 2 == 0) ? 0.5 : -0.5);  // ±50% returns
    }
    
    EXPECT_TRUE(model_->calibrate(extreme_returns));
    EXPECT_TRUE(model_->is_model_valid());
    
    // Model should handle extreme data gracefully
    double forecast = model_->forecast_volatility(1);
    EXPECT_GT(forecast, 0.0);
    EXPECT_LT(forecast, 10.0);  // Should be reasonable even with extreme data
    
    std::cout << "Edge case handling tests passed" << std::endl;
}

class VolatilityForecasterTest : public ::testing::Test {
protected:
    std::unique_ptr<Core::MemoryPool> mem_pool_;
    std::unique_ptr<Volatility::QuantLibGARCHModel> garch_model_;
    std::unique_ptr<Volatility::VolatilityForecaster> forecaster_;

    void SetUp() override {
        mem_pool_ = std::make_unique<Core::MemoryPool>(16 * 1024 * 1024);
        garch_model_ = std::make_unique<Volatility::QuantLibGARCHModel>(*mem_pool_);
        forecaster_ = std::make_unique<Volatility::VolatilityForecaster>(*garch_model_, *mem_pool_);
        
        // Calibrate GARCH model for testing
        std::vector<QuantLib::Real> returns;
        std::random_device rd;
        std::mt19937 gen(42);
        std::normal_distribution<> normal(0.0, 0.02);
        
        for (int i = 0; i < 252; ++i) {
            returns.push_back(normal(gen));
        }
        
        garch_model_->calibrate(returns);
    }
};

TEST_F(VolatilityForecasterTest, EnsembleForecastingFunctionality) {
    std::vector<double> recent_returns;
    for (int i = 0; i < 30; ++i) {
        recent_returns.push_back(0.01 * (i % 2 == 0 ? 1 : -1));  // Alternating returns
    }
    
    // Test ensemble forecast
    double ensemble_forecast = forecaster_->generate_ensemble_forecast(1, recent_returns);
    EXPECT_GT(ensemble_forecast, 0.0);
    EXPECT_LT(ensemble_forecast, 2.0);  // Reasonable upper bound
    
    // Test individual model forecasts
    double garch_forecast = forecaster_->generate_garch_forecast(1);
    double hist_forecast = forecaster_->generate_historical_forecast(recent_returns);
    double ewma_forecast = forecaster_->generate_ewma_forecast(recent_returns);
    
    EXPECT_GT(garch_forecast, 0.0);
    EXPECT_GT(hist_forecast, 0.0);
    EXPECT_GT(ewma_forecast, 0.0);
    
    // Ensemble should be weighted combination
    auto weights = forecaster_->get_model_weights();
    ASSERT_EQ(weights.size(), 3);
    
    double expected_ensemble = weights[0] * garch_forecast + 
                              weights[1] * hist_forecast + 
                              weights[2] * ewma_forecast;
    EXPECT_NEAR(ensemble_forecast, expected_ensemble, 1e-10);
    
    std::cout << "Individual forecasts - GARCH: " << garch_forecast 
              << ", Historical: " << hist_forecast 
              << ", EWMA: " << ewma_forecast 
              << ", Ensemble: " << ensemble_forecast << std::endl;
}

TEST_F(VolatilityForecasterTest, AdaptiveWeightUpdating) {
    std::vector<double> returns = {0.01, -0.015, 0.02, -0.008, 0.012};
    
    auto initial_weights = forecaster_->get_model_weights();
    
    // Simulate forecast errors and weight updates
    for (int i = 0; i < 10; ++i) {
        double error = 0.001 * (i % 2 == 0 ? 1 : -1);  // Alternating small errors
        forecaster_->update_forecast_accuracy(error);
    }
    
    auto updated_weights = forecaster_->get_model_weights();
    
    // Weights should sum to approximately 1
    double weight_sum = std::accumulate(updated_weights.begin(), updated_weights.end(), 0.0);
    EXPECT_NEAR(weight_sum, 1.0, 0.01);
    
    // Some adaptation should have occurred
    bool weights_changed = false;
    for (size_t i = 0; i < initial_weights.size() && i < updated_weights.size(); ++i) {
        if (std::abs(initial_weights[i] - updated_weights[i]) > 0.001) {
            weights_changed = true;
            break;
        }
    }
    EXPECT_TRUE(weights_changed);
    
    std::cout << "Weight adaptation test passed" << std::endl;
}

TEST_F(VolatilityForecasterTest, HealthAndPerformanceMonitoring) {
    EXPECT_TRUE(forecaster_->is_healthy());
    
    std::vector<double> test_returns = {0.005, -0.01, 0.008, -0.003};
    
    // Generate some forecasts
    for (int i = 0; i < 5; ++i) {
        double forecast = forecaster_->generate_ensemble_forecast(1, test_returns);
        EXPECT_GT(forecast, 0.0);
    }
    
    EXPECT_EQ(forecaster_->get_total_forecasts(), 5);
    EXPECT_GE(forecaster_->get_average_forecast_error(), 0.0);
    
    // Reset and verify
    forecaster_->reset_performance_stats();
    EXPECT_EQ(forecaster_->get_total_forecasts(), 0);
    
    std::cout << "Performance monitoring test passed" << std::endl;
}
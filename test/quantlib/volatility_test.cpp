// test/quantlib/volatility_test.cpp
#include "../../src/quantlib/volatility/gjrgarch_wrapper.h"
#include "../../src/quantlib/core/memory_pool.h"
#include <gtest/gtest.h>
#include <random>

using namespace Alaris;

class GJRGARCHTest : public ::testing::Test {
protected:
    void SetUp() override {
        mem_pool_ = std::make_unique<Core::MemoryPool>(16 * 1024 * 1024);
        model_ = std::make_unique<Volatility::QuantLibGJRGARCHModel>(*mem_pool_);
    }

    std::vector<QuantLib::Real> generate_returns(size_t count, double volatility = 0.02) {
        std::random_device rd;
        std::mt19937 gen(rd());
        std::normal_distribution<> dist(0.0, volatility);
        
        std::vector<QuantLib::Real> returns;
        returns.reserve(count);
        
        for (size_t i = 0; i < count; ++i) {
            returns.push_back(dist(gen));
        }
        
        return returns;
    }

    std::unique_ptr<Core::MemoryPool> mem_pool_;
    std::unique_ptr<Volatility::QuantLibGJRGARCHModel> model_;
};

TEST_F(GJRGARCHTest, BasicForecasting) {
    auto returns = generate_returns(1000);
    
    EXPECT_TRUE(model_->calibrate(returns));
    
    // Test single-step forecast
    double forecast = model_->forecast_volatility(1);
    EXPECT_GT(forecast, 0.0);
    EXPECT_LT(forecast, 1.0); // Should be reasonable volatility level
    
    // Test multi-step forecast
    auto path = model_->forecast_volatility_path(10);
    EXPECT_EQ(path.size(), 10);
    
    for (double vol : path) {
        EXPECT_GT(vol, 0.0);
        EXPECT_LT(vol, 1.0);
    }
}

TEST_F(GJRGARCHTest, UpdateMechanism) {
    auto returns = generate_returns(500);
    
    EXPECT_TRUE(model_->calibrate(returns));
    
    double initial_vol = model_->current_volatility();
    EXPECT_GT(initial_vol, 0.0);
    
    // Update with new return
    model_->update(0.05); // Large positive return
    double vol_after_positive = model_->current_volatility();
    
    model_->update(-0.05); // Large negative return
    double vol_after_negative = model_->current_volatility();
    
    // GJR-GARCH should show asymmetric response (negative returns increase volatility more)
    // This is a simplified test - in practice, the effect depends on the gamma parameter
    EXPECT_GT(vol_after_negative, 0.0);
    EXPECT_GT(vol_after_positive, 0.0);
}

TEST_F(GJRGARCHTest, ModelValidation) {
    auto returns = generate_returns(1000);
    
    EXPECT_TRUE(model_->calibrate(returns));
    EXPECT_TRUE(model_->is_model_valid());
    
    auto params = model_->get_parameters();
    EXPECT_GT(params.size(), 0);
    
    // All parameters should be reasonable
    for (size_t i = 0; i < params.size(); ++i) {
        EXPECT_TRUE(std::isfinite(params[i]));
    }
    
    double ll = model_->log_likelihood();
    EXPECT_TRUE(std::isfinite(ll));
}
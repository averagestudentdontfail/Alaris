#include "signal_gen.h"
// src/quantlib/strategy/vol_arb.h
#pragma once

#include "../pricing/alo_engine.h"
#include "../volatility/gjrgarch_wrapper.h"
#include "../core/memory_pool.h"
#include "../core/event_log.h"
#include "../ipc/message_types.h"
#include <vector>
#include <unordered_map>

namespace Alaris::Strategy {

struct StrategyParameters {
    double entry_threshold;      // Volatility difference threshold for entry
    double exit_threshold;       // Threshold for position exit
    double risk_limit;          // Maximum risk per position
    double confidence_threshold; // Minimum confidence for signal generation
    double max_position_size;   // Maximum position size per symbol
    
    StrategyParameters() 
        : entry_threshold(0.05), exit_threshold(0.02), risk_limit(0.10),
          confidence_threshold(0.7), max_position_size(0.05) {}
};

enum class VolatilityModel {
    GJR_GARCH = 0,
    STANDARD_GARCH = 1,
    ENSEMBLE = 2
};

struct PositionInfo {
    uint32_t symbol_id;
    double quantity;
    double entry_price;
    double current_price;
    double unrealized_pnl;
    double entry_implied_vol;
    double current_implied_vol;
    uint64_t entry_timestamp;
    
    PositionInfo() : symbol_id(0), quantity(0), entry_price(0), current_price(0),
                    unrealized_pnl(0), entry_implied_vol(0), current_implied_vol(0),
                    entry_timestamp(0) {}
};

class VolatilityArbitrageStrategy {
private:
    // Core components
    Pricing::QuantLibALOEngine& pricer_;
    Volatility::QuantLibGJRGARCHModel gjr_garch_model_;
    Volatility::QuantLibGARCHModel standard_garch_model_;
    Core::PerCycleAllocator& allocator_;
    Core::EventLogger& event_logger_;
    
    // Strategy configuration
    StrategyParameters params_;
    VolatilityModel active_model_;
    
    // Market state
    std::unordered_map<uint32_t, IPC::MarketDataMessage> latest_market_data_;
    std::unordered_map<uint32_t, PositionInfo> current_positions_;
    
    // Model performance tracking
    struct ModelPerformance {
        double accuracy;
        double avg_error;
        size_t prediction_count;
        double sharpe_ratio;
    };
    
    std::array<ModelPerformance, 3> model_performance_;
    
    // Signal generation state
    size_t signals_generated_;
    size_t successful_signals_;
    double total_pnl_;
    
    // Helper methods
    double calculate_theoretical_price(const IPC::MarketDataMessage& market_data,
                                     double strike, QuantLib::Option::Type option_type,
                                     double forecast_volatility);
    
    double get_volatility_forecast(uint32_t symbol_id, size_t horizon = 1);
    double get_ensemble_forecast(uint32_t symbol_id, size_t horizon = 1);
    
    bool should_enter_position(const IPC::MarketDataMessage& market_data,
                              double strike, QuantLib::Option::Type option_type);
    
    bool should_exit_position(const PositionInfo& position,
                             const IPC::MarketDataMessage& market_data);
    
    double calculate_position_size(const IPC::MarketDataMessage& market_data,
                                  double confidence);
    
    double calculate_confidence(double vol_diff, double historical_accuracy);
    
    void update_model_performance(VolatilityModel model, double prediction_error);
    
    VolatilityModel select_best_model() const;
    
public:
    VolatilityArbitrageStrategy(Pricing::QuantLibALOEngine& pricer,
                               Core::PerCycleAllocator& allocator,
                               Core::EventLogger& event_logger,
                               Core::MemoryPool& mem_pool);
    
    ~VolatilityArbitrageStrategy() = default;
    
    // Non-copyable
    VolatilityArbitrageStrategy(const VolatilityArbitrageStrategy&) = delete;
    VolatilityArbitrageStrategy& operator=(const VolatilityArbitrageStrategy&) = delete;
    
    // Main strategy interface
    void on_market_data(const IPC::MarketDataMessage& data);
    
    void scan_options(const std::vector<Pricing::OptionData>& options,
                     std::vector<IPC::TradingSignalMessage>& signals);
    
    void generate_signals(std::vector<IPC::TradingSignalMessage>& signals);
    
    // Configuration
    void set_parameters(const StrategyParameters& params);
    void set_volatility_model(VolatilityModel model);
    
    // Position management
    void update_position(uint32_t symbol_id, double quantity, double price);
    void close_position(uint32_t symbol_id);
    void close_all_positions();
    
    // Model management
    void calibrate_models(const std::vector<QuantLib::Real>& returns);
    void update_volatility_models(const std::vector<QuantLib::Real>& returns);
    
    // Performance metrics
    struct PerformanceMetrics {
        double total_pnl;
        double sharpe_ratio;
        double win_rate;
        double avg_signal_confidence;
        size_t signals_generated;
        size_t successful_signals;
        std::array<ModelPerformance, 3> model_performance;
    };
    
    PerformanceMetrics get_performance_metrics() const;
    void reset_performance_metrics();
    
    // Status
    size_t active_positions() const { return current_positions_.size(); }
    double total_unrealized_pnl() const;
    VolatilityModel get_active_model() const { return active_model_; }
};

} // namespace Alaris::Strategy
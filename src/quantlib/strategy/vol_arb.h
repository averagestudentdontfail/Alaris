// src/quantlib/strategy/vol_arb.h

#pragma once

#include "../pricing/alo_engine.h"
#include "../volatility/gjrgarch_wrapper.h"
#include "../volatility/vol_forecast.h"
#include "../core/memory_pool.h"
#include "../core/event_log.h"
#include "../ipc/message_types.h"
#include <vector>
#include <unordered_map>
#include <memory>
#include <atomic>
#include <array>

namespace Alaris {
namespace Strategy {

// Strategy parameters structure
struct StrategyParameters {
    double entry_threshold = 0.05;      // Volatility difference threshold for entry
    double exit_threshold = 0.02;       // Volatility difference threshold for exit
    double risk_limit = 0.10;           // Maximum portfolio risk limit
    double confidence_threshold = 0.7;   // Minimum confidence for signal generation
    double max_position_size = 0.05;    // Maximum position size as % of portfolio
};

// Volatility model selection
enum class VolatilityModelType {
    GJR_GARCH_DIRECT = 0,           // Use GJR-GARCH model directly
    ENSEMBLE_GJR_HISTORICAL = 1     // Use ensemble of GJR-GARCH and historical models
};

// Position tracking - renamed for consistency with implementation
struct PositionInfo {
    uint32_t symbol_id = 0;
    double quantity = 0.0;              // Signed quantity (positive = long, negative = short)
    double entry_price = 0.0;
    double current_price = 0.0;
    double entry_implied_vol = 0.0;
    double current_implied_vol = 0.0;
    double unrealized_pnl = 0.0;
    uint64_t entry_timestamp = 0;
    uint64_t last_update_timestamp = 0;
};

// Model performance tracking
struct ModelPerformance {
    double accuracy = 0.5;              // Model accuracy (0.0 to 1.0)
    double avg_error = 0.0;             // Average prediction error
    size_t prediction_count = 0;        // Number of predictions made
    
    ModelPerformance() = default;
};

// Strategy performance metrics
struct StrategyPerformanceMetrics {
    double total_pnl = 0.0;
    size_t total_signals_generated = 0;
    size_t total_trades_entered = 0;
    std::array<ModelPerformance, 2> model_performance_stats;
};

class VolatilityArbitrageStrategy {
private:
    // Core components - initialization order matters
    Pricing::QuantLibALOEngine& pricer_;
    Core::PerCycleAllocator& allocator_;
    Core::EventLogger& event_logger_;
    Core::MemoryPool& mem_pool_;
    
    // Volatility models
    Volatility::QuantLibGJRGARCHModel gjr_garch_model_;
    std::unique_ptr<Volatility::GlobalVolatilityForecaster> global_forecaster_;
    
    // Strategy configuration - aligned with implementation
    StrategyParameters params_;
    VolatilityModelType active_model_type_;
    
    // Market data tracking - aligned with implementation
    std::unordered_map<uint32_t, IPC::MarketDataMessage> latest_market_data_;
    
    // Position tracking - aligned with implementation
    std::unordered_map<uint32_t, PositionInfo> current_positions_;
    
    // Performance tracking - aligned with implementation
    std::array<ModelPerformance, 2> model_performance_tracking_;
    double total_realized_pnl_ = 0.0;
    size_t signals_generated_total_ = 0;
    size_t trades_entered_ = 0;
    
    // Internal state
    std::atomic<bool> is_initialized_{false};

    // Private methods - aligned with implementation
    double get_volatility_forecast(uint32_t underlying_symbol_id, size_t horizon = 1);
    double calculate_theoretical_price(const IPC::MarketDataMessage& underlying_market_data,
                                     const Pricing::OptionData& option_to_price,
                                     double forecast_volatility);
    bool should_enter_position(const IPC::MarketDataMessage& underlying_md,
                              const Pricing::OptionData& option_details,
                              double current_option_market_price,
                              double current_option_implied_vol);
    bool should_exit_position(const PositionInfo& position,
                             const IPC::MarketDataMessage& underlying_md,
                             double current_option_market_price,
                             double current_option_implied_vol);
    double calculate_position_size(double underlying_price, double option_price, double confidence);
    double calculate_signal_confidence(double vol_difference, VolatilityModelType model_used);
    void update_model_performance_tracking(VolatilityModelType model_type_used, 
                                         double prediction_error, bool trade_successful);
    VolatilityModelType select_active_model_type();
    void on_position_closed(uint32_t symbol_id, double pnl);

public:
    VolatilityArbitrageStrategy(
        Pricing::QuantLibALOEngine& pricer,
        Core::PerCycleAllocator& allocator,
        Core::EventLogger& event_logger,
        Core::MemoryPool& mem_pool
    );
    
    ~VolatilityArbitrageStrategy() = default;
    
    // Non-copyable, non-movable for safety
    VolatilityArbitrageStrategy(const VolatilityArbitrageStrategy&) = delete;
    VolatilityArbitrageStrategy& operator=(const VolatilityArbitrageStrategy&) = delete;
    VolatilityArbitrageStrategy(VolatilityArbitrageStrategy&&) = delete;
    VolatilityArbitrageStrategy& operator=(VolatilityArbitrageStrategy&&) = delete;
    
    // Configuration methods
    void set_parameters(const StrategyParameters& params);
    void set_active_volatility_model_type(VolatilityModelType model_type);
    
    // Market data processing
    void on_market_data(const IPC::MarketDataMessage& market_data);
    
    // Option chain scanning and signal generation
    void scan_option_chain(
        uint32_t underlying_symbol_id,
        const std::vector<Pricing::OptionData>& option_chain,
        const std::vector<IPC::MarketDataMessage>& option_market_data,
        std::vector<IPC::TradingSignalMessage>& signals
    );
    
    // Position management
    void on_fill(const IPC::TradingSignalMessage& signal, double fill_price, int fill_quantity_signed);
    void close_all_positions(std::vector<IPC::TradingSignalMessage>& out_exit_signals);
    
    // Model calibration
    bool calibrate_gjr_model(const std::vector<QuantLib::Real>& historical_returns);
    
    // Performance and status queries
    size_t active_positions_count() const { return current_positions_.size(); }
    double total_unrealized_pnl() const;
    VolatilityModelType get_active_model_type() const { return active_model_type_; }
    StrategyPerformanceMetrics get_performance_metrics() const;
    void reset_performance_metrics();
    
    // **PUBLIC TESTING INTERFACE** - For unit/integration tests
    #ifdef ALARIS_ENABLE_TESTING
    // Access to internal volatility models for testing
    const Volatility::QuantLibGJRGARCHModel& get_gjr_garch_model_for_testing() const { 
        return gjr_garch_model_; 
    }
    
    // Access to volatility forecast for testing
    double get_volatility_forecast_for_testing(uint32_t underlying_symbol_id, size_t horizon = 1) const {
        return const_cast<VolatilityArbitrageStrategy*>(this)->get_volatility_forecast(underlying_symbol_id, horizon);
    }
    
    // Access to position for testing
    const PositionInfo* get_position_for_testing(uint32_t symbol_id) const {
        auto it = current_positions_.find(symbol_id);
        return (it != current_positions_.end()) ? &it->second : nullptr;
    }
    
    // Access to latest market data for testing
    const std::unordered_map<uint32_t, IPC::MarketDataMessage>& get_latest_market_data_for_testing() const {
        return latest_market_data_;
    }
    #endif
};

} // namespace Strategy
} // namespace Alaris
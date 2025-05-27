// src/quantlib/strategy/vol_arb.h
// Volatility Arbitrage Strategy - Fixed for testing

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
    GJR_GARCH_DIRECT,           // Use GJR-GARCH model directly
    ENSEMBLE_GJR_HISTORICAL     // Use ensemble of GJR-GARCH and historical models
};

// Position tracking
struct Position {
    uint32_t symbol_id;
    int32_t quantity;           // Signed quantity (positive = long, negative = short)
    double entry_price;
    double current_price;
    double unrealized_pnl;
    uint64_t entry_timestamp;
    uint64_t last_update_timestamp;
};

// Performance metrics
struct StrategyPerformanceMetrics {
    double total_pnl = 0.0;
    double unrealized_pnl = 0.0;
    double realized_pnl = 0.0;
    size_t total_trades = 0;
    size_t winning_trades = 0;
    size_t losing_trades = 0;
    double max_drawdown = 0.0;
    double sharpe_ratio = 0.0;
    size_t signals_generated = 0;
    size_t signals_executed = 0;
};

class VolatilityArbitrageStrategy {
private:
    // Core components
    Pricing::QuantLibALOEngine& pricer_;
    Core::PerCycleAllocator& allocator_;
    Core::EventLogger& event_logger_;
    Core::MemoryPool& mem_pool_;
    
    // Volatility models
    Volatility::QuantLibGJRGARCHModel gjr_garch_model_;
    std::unique_ptr<Volatility::GlobalVolatilityForecaster> global_forecaster_;
    
    // Strategy configuration
    StrategyParameters parameters_;
    VolatilityModelType active_model_type_;
    
    // Market data tracking
    std::unordered_map<uint32_t, IPC::MarketDataMessage> latest_market_data_;
    
    // Position tracking
    std::unordered_map<uint32_t, Position> active_positions_;
    
    // Performance tracking
    StrategyPerformanceMetrics performance_metrics_;
    
    // Internal state
    std::atomic<bool> is_initialized_{false};

public:
    VolatilityArbitrageStrategy(
        Pricing::QuantLibALOEngine& pricer,
        Core::PerCycleAllocator& allocator,
        Core::EventLogger& event_logger,
        Core::MemoryPool& mem_pool
    );
    
    ~VolatilityArbitrageStrategy() = default;
    
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
    void on_fill(const IPC::TradingSignalMessage& signal, double fill_price, int32_t fill_quantity_signed);
    
    // Model calibration
    bool calibrate_gjr_model(const std::vector<QuantLib::Real>& historical_returns);
    
    // Performance and status queries
    size_t active_positions_count() const { return active_positions_.size(); }
    double total_unrealized_pnl() const;
    VolatilityModelType get_active_model_type() const { return active_model_type_; }
    StrategyPerformanceMetrics get_performance_metrics() const { return performance_metrics_; }
    
    // **PUBLIC TESTING INTERFACE** - For unit/integration tests
    #ifdef ALARIS_ENABLE_TESTING
    // Access to internal volatility models for testing
    const Volatility::QuantLibGJRGARCHModel& get_gjr_garch_model_for_testing() const { 
        return gjr_garch_model_; 
    }
    
    // Access to volatility forecast for testing
    double get_volatility_forecast_for_testing(uint32_t underlying_symbol_id, size_t horizon = 1) const {
        return get_volatility_forecast(underlying_symbol_id, horizon);
    }
    
    // Access to position for testing
    const Position* get_position_for_testing(uint32_t symbol_id) const {
        auto it = active_positions_.find(symbol_id);
        return (it != active_positions_.end()) ? &it->second : nullptr;
    }
    
    // Access to latest market data for testing
    const std::unordered_map<uint32_t, IPC::MarketDataMessage>& get_latest_market_data_for_testing() const {
        return latest_market_data_;
    }
    #endif

private:
    // Internal volatility forecasting
    double get_volatility_forecast(uint32_t underlying_symbol_id, size_t horizon = 1) const;
    
    // Signal generation helpers
    IPC::TradingSignalMessage create_entry_signal(
        const Pricing::OptionData& option,
        const IPC::MarketDataMessage& option_md,
        double theoretical_price,
        double forecast_volatility,
        double confidence
    );
    
    IPC::TradingSignalMessage create_exit_signal(
        const Position& position,
        double current_market_price,
        const std::string& exit_reason
    );
    
    // Position management helpers
    void update_position_pnl(Position& position, double current_price);
    void update_performance_metrics();
    
    // Risk management
    bool check_risk_limits(const IPC::TradingSignalMessage& signal) const;
    double calculate_position_size(const Pricing::OptionData& option, double confidence) const;
    
    // Internal initialization
    void initialize_volatility_models();
};

} // namespace Strategy
} // namespace Alaris
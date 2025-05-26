#pragma once

#include "../pricing/alo_engine.h"         // For Alaris::Pricing::QuantLibALOEngine
#include "../volatility/gjrgarch_wrapper.h" // For Alaris::Volatility::QuantLibGJRGARCHModel
#include "../volatility/vol_forecast.h"     // For ensemble forecasting functions
#include "../core/memory_pool.h"          // For Alaris::Core::MemoryPool and PerCycleAllocator
#include "../core/event_log.h"            // For Alaris::Core::EventLogger
#include "../ipc/message_types.h"           // For Alaris::IPC messages
#include <vector>
#include <string> // For performance metric names, status messages
#include <unordered_map>
#include <array> // For model_performance_

namespace Alaris::Strategy {

struct StrategyParameters {
    double entry_threshold{0.05};      // Volatility difference threshold for entry
    double exit_threshold{0.02};       // Threshold for position exit
    double risk_limit{0.10};          // Maximum risk per position (e.g., % of entry price or fixed amount)
    double confidence_threshold{0.7}; // Minimum confidence from vol_forecast for signal generation
    double max_position_size{0.05};   // Maximum position size as a fraction of portfolio or fixed units

    // Default constructor initializes with some sensible defaults
    StrategyParameters() = default;
};

// Simplified VolatilityModel enum
enum class VolatilityModelType {
    GJR_GARCH_DIRECT = 0,    // Use raw forecast from QuantLibGJRGARCHModel
    ENSEMBLE_GJR_HISTORICAL = 1 // Use ensemble (GJR-GARCH + Historical) from VolatilityForecaster
};

struct PositionInfo {
    uint32_t symbol_id{0};
    double quantity{0.0};
    double entry_price{0.0};
    double current_price{0.0};
    double unrealized_pnl{0.0};
    double entry_implied_vol{0.0};
    double current_implied_vol{0.0}; // Updated with market data
    uint64_t entry_timestamp{0};
    // Potentially add option contract details if managing specific options
};

class VolatilityArbitrageStrategy {
private:
    // Core components (references to externally managed lifetime objects)
    Core::MemoryPool& mem_pool_; // General memory pool reference
    Pricing::QuantLibALOEngine& pricer_;
    Core::PerCycleAllocator& allocator_; // For per-cycle allocations if needed by strategy logic
    Core::EventLogger& event_logger_;

    // Owned components
    Volatility::QuantLibGJRGARCHModel gjr_garch_model_; // Strategy's own GJR-GARCH model instance

    // Strategy configuration
    StrategyParameters params_;
    VolatilityModelType active_model_type_;

    // Market state
    std::unordered_map<uint32_t, IPC::MarketDataMessage> latest_market_data_; // Keyed by underlying symbol_id
    std::unordered_map<uint32_t, PositionInfo> current_positions_;          // Keyed by option_symbol_id or underlying_symbol_id

    // Model performance tracking for the two types of forecasts the strategy can use
    struct ModelPerformance {
        double accuracy{0.5};      // e.g., 1 - MAPE, or hit rate
        double avg_error{0.0};     // e.g., average forecast error
        size_t prediction_count{0};
        // Potentially other metrics like Sharpe ratio if backtesting signals from this model type
        // double sharpe_ratio{0.0};
    };
    
    // Index 0 for GJR_GARCH_DIRECT, Index 1 for ENSEMBLE_GJR_HISTORICAL
    std::array<ModelPerformance, 2> model_performance_tracking_;

    // Signal generation state
    size_t signals_generated_total_{0};
    size_t trades_entered_{0}; // Or use portfolio feedback for successful trades
    double total_realized_pnl_{0.0};

    // Helper methods
    double calculate_theoretical_price(const IPC::MarketDataMessage& underlying_market_data,
                                     const Pricing::OptionData& option_to_price, // Pass full option details
                                     double forecast_volatility);

    double get_volatility_forecast(uint32_t underlying_symbol_id, size_t horizon = 1);
    
    bool should_enter_position(const IPC::MarketDataMessage& underlying_market_data,
                              const Pricing::OptionData& option_details, // Use for strike, type, expiry
                              double current_option_market_price,
                              double current_option_implied_vol);
    
    bool should_exit_position(const PositionInfo& position,
                             const IPC::MarketDataMessage& underlying_market_data,
                             double current_option_market_price,
                             double current_option_implied_vol);
    
    double calculate_position_size(double underlying_price, double option_price, double confidence);
    
    // Confidence calculation might use output from vol_forecast::calculate_forecast_confidence
    // or have its own logic based on internal model performance.
    double calculate_signal_confidence(double vol_difference, VolatilityModelType model_used);
    
    void update_model_performance_tracking(VolatilityModelType model_type_used, double prediction_error, bool trade_successful);
    VolatilityModelType select_active_model_type(); // Logic to select between direct GJR and Ensemble

public:
    VolatilityArbitrageStrategy(Pricing::QuantLibALOEngine& pricer,
                               Core::PerCycleAllocator& allocator,
                               Core::EventLogger& event_logger,
                               Core::MemoryPool& mem_pool); // Pass general mem_pool
    
    ~VolatilityArbitrageStrategy() = default;

    // Non-copyable
    VolatilityArbitrageStrategy(const VolatilityArbitrageStrategy&) = delete;
    VolatilityArbitrageStrategy& operator=(const VolatilityArbitrageStrategy&) = delete;

    // Main strategy interface
    void on_market_data(const IPC::MarketDataMessage& data); // Can be for underlying or option
    
    // Scans a list of options and generates trading signals
    void scan_option_chain(uint32_t underlying_symbol_id,
                           const std::vector<Pricing::OptionData>& options_in_chain,
                           const std::vector<IPC::MarketDataMessage>& option_market_data, // Corresponding market data for options
                           std::vector<IPC::TradingSignalMessage>& out_signals);
    
    // Alternative: a more generic generate_signals that internally manages what to scan
    // void generate_signals(std::vector<IPC::TradingSignalMessage>& signals);

    // Configuration
    void set_parameters(const StrategyParameters& params);
    void set_active_volatility_model_type(VolatilityModelType model_type);

    // Position management (feedback from execution system)
    void on_fill(const IPC::TradingSignalMessage& original_signal, double fill_price, int fill_quantity);
    void on_position_closed(uint32_t symbol_id, double pnl); // symbol_id of the closed position

    void close_all_positions(std::vector<IPC::TradingSignalMessage>& out_exit_signals); // Generates exit signals

    // Model management
    void calibrate_gjr_model(const std::vector<QuantLib::Real>& returns_data);
    // update_volatility_models is essentially on_market_data + calibrate_gjr_model
    
    // Performance metrics
    struct StrategyPerformanceMetrics {
        double total_pnl;
        // double sharpe_ratio; // Requires more complex calculation
        double win_rate; // Based on trades_entered_ vs successful_trades
        double avg_signal_confidence;
        size_t total_signals_generated;
        size_t total_trades_entered;
        std::array<ModelPerformance, 2> model_performance_stats;
    };
    
    StrategyPerformanceMetrics get_performance_metrics() const;
    void reset_performance_metrics();

    // Status
    size_t active_positions_count() const { return current_positions_.size(); }
    double total_unrealized_pnl() const;
    VolatilityModelType get_active_model_type() const { return active_model_type_; }
};

} // namespace Alaris::Strategy
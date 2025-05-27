// src/quantlib/strategy/vol_arb.h
#pragma once

#include "../pricing/alo_engine.h"
#include "../volatility/garch_wrapper.h"  // Changed from gjrgarch_wrapper.h
#include "../core/memory_pool.h"
#include "../core/event_log.h"
#include "../ipc/message_types.h"
#include <vector>
#include <unordered_map>
#include <memory>
#include <atomic>
#include <array>
#include <deque>

namespace Alaris::Strategy {

// Enhanced strategy parameters for production volatility arbitrage
struct StrategyParameters {
    // Core volatility thresholds
    double vol_difference_threshold = 0.03;
    double vol_exit_threshold = 0.01;
    double confidence_threshold = 0.75;
    
    // Risk management
    double max_portfolio_delta = 0.1;
    double max_portfolio_gamma = 0.05;
    double max_portfolio_vega = 1.0;
    double max_position_size = 0.02;
    double max_correlation_exposure = 0.3;
    
    // Position sizing (Kelly criterion parameters)
    double kelly_fraction = 0.02;
    double max_kelly_position = 0.05;
    double min_edge_ratio = 1.5;
    
    // Stop loss and profit taking
    double stop_loss_percent = 0.15;
    double profit_target_percent = 0.30;
    double trailing_stop_percent = 0.08;
    
    // Strategy modes
    enum class Mode {
        DELTA_NEUTRAL,
        GAMMA_SCALPING,
        VOLATILITY_TIMING,
        RELATIVE_VALUE
    } strategy_mode = Mode::DELTA_NEUTRAL;
    
    // Hedging parameters
    double hedge_threshold_delta = 0.05;
    double hedge_threshold_gamma = 0.03;
    bool auto_hedge_enabled = true;
    double hedge_frequency_minutes = 15.0;
    
    // Market regime detection
    double low_vol_threshold = 0.12;
    double high_vol_threshold = 0.30;
    size_t regime_lookback_days = 30;
};

// Enhanced position tracking with Greeks and risk metrics
struct EnhancedPosition {
    uint32_t symbol_id = 0;
    double quantity = 0.0;
    double entry_price = 0.0;
    double current_price = 0.0;
    double entry_implied_vol = 0.0;
    double current_implied_vol = 0.0;
    uint64_t entry_timestamp = 0;
    uint64_t last_update_timestamp = 0;
    
    // Greeks at position level
    Pricing::OptionGreeks entry_greeks;
    Pricing::OptionGreeks current_greeks;
    
    // Risk metrics
    double unrealized_pnl = 0.0;
    double realized_pnl = 0.0;
    double max_unrealized_pnl = 0.0;
    double max_drawdown = 0.0;
    double initial_margin_requirement = 0.0;
    
    // Strategy-specific data
    double vol_forecast_at_entry = 0.0;
    double confidence_at_entry = 0.0;
    double kelly_size_at_entry = 0.0;
    bool is_hedge_position = false;
    uint32_t hedge_target_symbol = 0;
    
    // Position state
    enum class State {
        ACTIVE,
        PROFIT_TARGET_HIT,
        STOP_LOSS_HIT,
        TRAILING_STOP_HIT,
        TIME_DECAY_EXIT,
        VOLATILITY_CONVERGED
    } state = State::ACTIVE;
};

// Portfolio-level risk metrics
struct PortfolioRiskMetrics {
    double total_delta = 0.0;
    double total_gamma = 0.0;
    double total_vega = 0.0;
    double total_theta = 0.0;
    double total_rho = 0.0;
    
    double portfolio_var_1day = 0.0;
    double portfolio_var_10day = 0.0;
    double max_correlation_exposure = 0.0;
    double liquidity_score = 1.0;
    
    double total_notional = 0.0;
    double margin_utilization = 0.0;
    size_t active_positions = 0;
    double sharpe_ratio_mtd = 0.0;
};

// Market regime detection and analysis
struct MarketRegime {
    enum class VolRegime { LOW, MEDIUM, HIGH, TRANSITIONING };
    enum class TrendRegime { TRENDING_UP, TRENDING_DOWN, SIDEWAYS };
    enum class LiquidityRegime { HIGH_LIQUIDITY, NORMAL, LOW_LIQUIDITY };
    
    VolRegime vol_regime = VolRegime::MEDIUM;
    TrendRegime trend_regime = TrendRegime::SIDEWAYS;
    LiquidityRegime liquidity_regime = LiquidityRegime::NORMAL;
    
    double current_realized_vol = 0.0;
    double current_implied_vol = 0.0;
    double vol_risk_premium = 0.0;
    double regime_confidence = 0.5;
    uint64_t regime_start_time = 0;
    
    // Forward-looking indicators
    double expected_vol_next_week = 0.0;
    double vol_clustering_strength = 0.0;
    double mean_reversion_speed = 0.0;
};

// Volatility model type enum - updated to remove GJR-GARCH
enum class VolatilityModelType {
    GARCH_DIRECT,           // Changed from GJR_GARCH_DIRECT
    ENSEMBLE_GARCH_HISTORICAL  // Changed from ENSEMBLE_GJR_HISTORICAL
};

// Sophisticated volatility arbitrage strategy class
class VolatilityArbitrageStrategy {
private:
    // Core components (order matters for initialization)
    Pricing::QuantLibALOEngine& pricer_;
    Core::PerCycleAllocator& allocator_;
    Core::EventLogger& event_logger_;
    Core::MemoryPool& mem_pool_;
    
    // Volatility models and forecasting - changed to standard GARCH
    std::unique_ptr<Volatility::QuantLibGARCHModel> garch_model_;
    std::unique_ptr<Volatility::VolatilityForecaster> vol_forecaster_;
    
    // Strategy configuration
    StrategyParameters params_;
    VolatilityModelType active_model_type_ = VolatilityModelType::ENSEMBLE_GARCH_HISTORICAL;
    
    // Market data and state
    std::unordered_map<uint32_t, IPC::MarketDataMessage> latest_market_data_;
    std::unordered_map<uint32_t, std::deque<double>> price_history_;
    std::unordered_map<uint32_t, std::deque<double>> vol_history_;
    
    // Position and portfolio management
    std::unordered_map<uint32_t, EnhancedPosition> positions_;
    PortfolioRiskMetrics portfolio_metrics_;
    MarketRegime current_regime_;
    
    // Risk management state
    std::unordered_map<uint32_t, std::vector<uint32_t>> correlation_buckets_;
    std::deque<double> daily_pnl_history_;
    double total_realized_pnl_ = 0.0;
    double total_unrealized_pnl_ = 0.0;
    
    // Performance tracking
    std::atomic<size_t> signals_generated_{0};
    std::atomic<size_t> trades_executed_{0};
    std::atomic<size_t> hedge_trades_{0};
    uint64_t last_portfolio_rebalance_ = 0;
    uint64_t last_regime_update_ = 0;
    
    // Strategy-specific analytics
    struct VolSurfacePoint {
        double strike_ratio;
        double time_to_expiry;
        double implied_vol;
        double model_vol;
        double arbitrage_score;
        uint64_t timestamp;
    };
    std::vector<VolSurfacePoint> vol_surface_analysis_;
    
    // Private methods for strategy logic
    void update_market_regime(uint32_t underlying_symbol);
    void update_portfolio_metrics();
    void update_correlation_analysis();
    
    // Signal generation
    std::vector<IPC::TradingSignalMessage> generate_delta_neutral_signals(
        uint32_t underlying_symbol,
        const std::vector<Pricing::OptionData>& option_chain,
        const std::vector<IPC::MarketDataMessage>& option_market_data);
    
    std::vector<IPC::TradingSignalMessage> generate_gamma_scalping_signals(
        uint32_t underlying_symbol,
        const std::vector<Pricing::OptionData>& option_chain,
        const std::vector<IPC::MarketDataMessage>& option_market_data);
    
    std::vector<IPC::TradingSignalMessage> generate_volatility_timing_signals(
        uint32_t underlying_symbol,
        const std::vector<Pricing::OptionData>& option_chain,
        const std::vector<IPC::MarketDataMessage>& option_market_data);
    
    // Position sizing and risk management
    double calculate_kelly_position_size(double edge, double volatility, 
                                       double win_probability, double avg_win_loss_ratio);
    double calculate_var_adjusted_size(const Pricing::OptionData& option, 
                                     double base_size);
    bool check_position_limits(const IPC::TradingSignalMessage& signal);
    bool check_correlation_limits(uint32_t symbol_id, double position_size);
    
    // Dynamic hedging
    std::vector<IPC::TradingSignalMessage> generate_hedge_signals();
    double calculate_hedge_ratio(const EnhancedPosition& position, 
                                const Pricing::OptionData& hedge_instrument);
    
    // Risk metrics calculations
    double calculate_portfolio_var(double confidence_level = 0.05, size_t horizon_days = 1);
    double calculate_position_correlation(uint32_t symbol1, uint32_t symbol2);
    double calculate_liquidity_score(const Pricing::OptionData& option);
    
    // Strategy analytics
    void analyze_volatility_surface(uint32_t underlying_symbol,
                                  const std::vector<Pricing::OptionData>& options,
                                  const std::vector<IPC::MarketDataMessage>& market_data);
    double calculate_volatility_risk_premium(uint32_t underlying_symbol);
    double calculate_vol_of_vol(uint32_t underlying_symbol);
    
    // Utility methods
    bool should_enter_position(const Pricing::OptionData& option,
                              const IPC::MarketDataMessage& market_data,
                              double forecast_vol, double confidence);
    bool should_exit_position(const EnhancedPosition& position,
                             const IPC::MarketDataMessage& current_market_data);
    bool should_hedge_position(const EnhancedPosition& position);
    
    void update_position_greeks(EnhancedPosition& position,
                               const Pricing::OptionData& option_data);
    void apply_risk_limits();

public:
    explicit VolatilityArbitrageStrategy(
        Pricing::QuantLibALOEngine& pricer,
        Core::PerCycleAllocator& allocator,
        Core::EventLogger& event_logger,
        Core::MemoryPool& mem_pool
    );
    
    ~VolatilityArbitrageStrategy() = default;
    
    // Non-copyable, non-movable for safety
    VolatilityArbitrageStrategy(const VolatilityArbitrageStrategy&) = delete;
    VolatilityArbitrageStrategy& operator=(const VolatilityArbitrageStrategy&) = delete;
    
    // Configuration
    void set_parameters(const StrategyParameters& params);
    void set_strategy_mode(StrategyParameters::Mode mode);
    void set_active_volatility_model_type(VolatilityModelType model_type) { 
        active_model_type_ = model_type; 
    }
    StrategyParameters get_parameters() const { return params_; }
    
    // Market data processing
    void on_market_data(const IPC::MarketDataMessage& market_data);
    void on_option_chain_update(uint32_t underlying_symbol,
                               const std::vector<Pricing::OptionData>& option_chain,
                               const std::vector<IPC::MarketDataMessage>& option_market_data);
    
    // Main strategy execution
    void scan_and_generate_signals(uint32_t underlying_symbol,
                                  const std::vector<Pricing::OptionData>& option_chain,
                                  const std::vector<IPC::MarketDataMessage>& option_market_data,
                                  std::vector<IPC::TradingSignalMessage>& out_signals);
    
    // Position management
    void on_fill(const IPC::TradingSignalMessage& signal, double fill_price, 
                 int fill_quantity, uint64_t fill_timestamp);
    void on_partial_fill(const IPC::TradingSignalMessage& signal, double fill_price,
                        int fill_quantity, int remaining_quantity, uint64_t fill_timestamp);
    
    // Portfolio management
    void rebalance_portfolio();
    void close_all_positions(std::vector<IPC::TradingSignalMessage>& out_signals);
    void emergency_liquidation(std::vector<IPC::TradingSignalMessage>& out_signals);
    
    // Risk management
    void apply_stop_losses(std::vector<IPC::TradingSignalMessage>& out_signals);
    void apply_profit_targets(std::vector<IPC::TradingSignalMessage>& out_signals);
    void check_margin_requirements();
    
    // Model management
    bool calibrate_volatility_models(const std::unordered_map<uint32_t, std::vector<double>>& returns_by_asset);
    void update_model_performance(uint32_t symbol_id, double forecast_error);
    
    // Analytics and reporting
    PortfolioRiskMetrics get_portfolio_metrics() const { return portfolio_metrics_; }
    MarketRegime get_current_regime() const { return current_regime_; }
    std::vector<VolSurfacePoint> get_volatility_surface_analysis() const { return vol_surface_analysis_; }
    
    // Performance metrics
    struct PerformanceMetrics {
        double total_pnl;
        double sharpe_ratio;
        double max_drawdown;
        double win_rate;
        double average_trade_duration_hours;
        double portfolio_turnover;
        size_t total_trades;
        size_t winning_trades;
        double largest_win;
        double largest_loss;
        double vol_forecast_accuracy;
        double average_edge_captured;
    };
    
    PerformanceMetrics get_performance_metrics() const;
    void reset_performance_metrics();
    
    // Status and diagnostics
    size_t active_positions_count() const { return positions_.size(); }
    double get_total_pnl() const { return total_realized_pnl_ + total_unrealized_pnl_; }
    bool is_healthy() const;
    
    // Advanced features
    void enable_machine_learning_enhancements();
    void set_regime_override(MarketRegime::VolRegime regime);
    
    // Testing and simulation support
#ifdef ALARIS_ENABLE_TESTING
    const EnhancedPosition* get_position_for_testing(uint32_t symbol_id) const {
        auto it = positions_.find(symbol_id);
        return (it != positions_.end()) ? &it->second : nullptr;
    }
    
    const std::unordered_map<uint32_t, IPC::MarketDataMessage>& 
    get_latest_market_data_for_testing() const {
        return latest_market_data_;
    }
    
    void set_testing_regime(const MarketRegime& regime) {
        current_regime_ = regime;
    }
#endif
};

} // namespace Alaris::Strategy
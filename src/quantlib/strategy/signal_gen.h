#pragma once

#include "../ipc/message_types.h"
#include <chrono>

namespace Alaris::Strategy {

class SignalGenerator {
public:
    struct SignalParams {
        double entry_threshold = 0.05;
        double exit_threshold = 0.02;
        double confidence_threshold = 0.7;
        double max_position_size = 0.05;
        double risk_limit = 0.10;
    };
    
    SignalGenerator();
    
    bool should_generate_signal(double forecast_vol, double implied_vol, 
                               const SignalParams& params);
    
    IPC::TradingSignalMessage generate_signal(uint32_t symbol_id,
                                             double theoretical_price,
                                             double market_price,
                                             double forecast_vol,
                                             double implied_vol,
                                             const SignalParams& params);
    
    IPC::TradingSignalMessage generate_exit_signal(uint32_t symbol_id,
                                                  double current_price,
                                                  double entry_price,
                                                  double current_pnl,
                                                  const SignalParams& params);
    
    void update_parameters(const SignalParams& params);
    double get_signal_performance() const;

private:
    double confidence_threshold_;
    double min_vol_diff_;
    double max_signal_frequency_;
    std::vector<double> recent_signals_;
    std::chrono::steady_clock::time_point last_signal_time_;
    
    double calculate_signal_confidence(double vol_diff, const SignalParams& params);
};

// Global interface functions
void initialize_signal_generator();

IPC::TradingSignalMessage create_trading_signal(uint32_t symbol_id,
                                               double theoretical_price,
                                               double market_price,
                                               double forecast_vol,
                                               double implied_vol,
                                               const SignalGenerator::SignalParams& params);

bool should_generate_trading_signal(double forecast_vol, double implied_vol,
                                   const SignalGenerator::SignalParams& params);

} // namespace Alaris::Strategy

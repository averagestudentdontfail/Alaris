#include "signal_gen.h"
#include <algorithm>
#include <cmath>

namespace Alaris::Strategy {

// Global signal generator
static std::unique_ptr<SignalGenerator> global_signal_generator = nullptr;

void initialize_signal_generator() {
    global_signal_generator = std::make_unique<SignalGenerator>();
}

IPC::TradingSignalMessage create_trading_signal(uint32_t symbol_id,
                                               double theoretical_price,
                                               double market_price,
                                               double forecast_vol,
                                               double implied_vol,
                                               const SignalGenerator::SignalParams& params) {
    if (!global_signal_generator) {
        initialize_signal_generator();
    }
    
    return global_signal_generator->generate_signal(symbol_id, theoretical_price, 
                                                   market_price, forecast_vol, 
                                                   implied_vol, params);
}

bool should_generate_trading_signal(double forecast_vol, double implied_vol,
                                   const SignalGenerator::SignalParams& params) {
    if (!global_signal_generator) {
        initialize_signal_generator();
    }
    
    return global_signal_generator->should_generate_signal(forecast_vol, implied_vol, params);
}

} // namespace Alaris::Strategy

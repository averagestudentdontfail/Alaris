#include "vol_arb.h"
#include <algorithm>
#include <cmath>

namespace Alaris::Strategy {

class SignalGenerator {
private:
    double confidence_threshold_;
    double min_vol_diff_;
    double max_signal_frequency_;
    
    // Signal tracking
    std::vector<double> recent_signals_;
    std::chrono::steady_clock::time_point last_signal_time_;
    
public:
    SignalGenerator() 
        : confidence_threshold_(0.7), min_vol_diff_(0.02), max_signal_frequency_(10.0),
          last_signal_time_(std::chrono::steady_clock::now()) {}
    
    struct SignalParams {
        double entry_threshold = 0.05;
        double exit_threshold = 0.02;
        double confidence_threshold = 0.7;
        double max_position_size = 0.05;
        double risk_limit = 0.10;
    };
    
    bool should_generate_signal(double forecast_vol, double implied_vol, 
                               const SignalParams& params) {
        double vol_diff = std::abs(forecast_vol - implied_vol);
        
        // Check minimum volatility difference
        if (vol_diff < params.entry_threshold) {
            return false;
        }
        
        // Check signal frequency limits
        auto now = std::chrono::steady_clock::now();
        auto time_since_last = std::chrono::duration_cast<std::chrono::milliseconds>
                              (now - last_signal_time_).count();
        
        if (time_since_last < (1000.0 / max_signal_frequency_)) {
            return false; // Too frequent
        }
        
        return true;
    }
    
    IPC::TradingSignalMessage generate_signal(uint32_t symbol_id,
                                             double theoretical_price,
                                             double market_price,
                                             double forecast_vol,
                                             double implied_vol,
                                             const SignalParams& params) {
        
        IPC::TradingSignalMessage signal;
        
        signal.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::high_resolution_clock::now().time_since_epoch()).count();
        
        signal.symbol_id = symbol_id;
        signal.theoretical_price = theoretical_price;
        signal.market_price = market_price;
        signal.implied_volatility = implied_vol;
        signal.forecast_volatility = forecast_vol;
        
        double vol_diff = forecast_vol - implied_vol;
        signal.confidence = calculate_signal_confidence(vol_diff, params);
        
        // Determine position size based on confidence
        signal.quantity = static_cast<int32_t>(
            params.max_position_size * signal.confidence * 100); // Scale for contract size
        
        // Determine side: buy if forecast > implied, sell otherwise
        signal.side = vol_diff > 0 ? 0 : 1;
        
        // Set urgency based on volatility difference magnitude
        double urgency_factor = std::min(1.0, std::abs(vol_diff) / (2.0 * params.entry_threshold));
        signal.urgency = static_cast<uint8_t>(urgency_factor * 255);
        
        signal.signal_type = 0; // Entry signal
        
        // Update tracking
        recent_signals_.push_back(vol_diff);
        if (recent_signals_.size() > 100) {
            recent_signals_.erase(recent_signals_.begin());
        }
        last_signal_time_ = std::chrono::steady_clock::now();
        
        return signal;
    }
    
    IPC::TradingSignalMessage generate_exit_signal(uint32_t symbol_id,
                                                  double current_price,
                                                  double entry_price,
                                                  double current_pnl,
                                                  const SignalParams& params) {
        
        IPC::TradingSignalMessage signal;
        
        signal.timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::high_resolution_clock::now().time_since_epoch()).count();
        
        signal.symbol_id = symbol_id;
        signal.theoretical_price = current_price;
        signal.market_price = current_price;
        signal.confidence = 0.9; // High confidence for exit signals
        
        // Determine urgency based on P&L
        double loss_ratio = std::abs(current_pnl) / std::abs(entry_price);
        if (loss_ratio > params.risk_limit * 0.8) {
            signal.urgency = 255; // Maximum urgency near risk limit
        } else {
            signal.urgency = static_cast<uint8_t>(loss_ratio / params.risk_limit * 255);
        }
        
        signal.signal_type = 1; // Exit signal
        
        return signal;
    }
    
    void update_parameters(const SignalParams& params) {
        confidence_threshold_ = params.confidence_threshold;
        min_vol_diff_ = params.entry_threshold;
    }
    
    double get_signal_performance() const {
        if (recent_signals_.empty()) return 0.5;
        
        // Calculate average absolute signal strength
        double sum = 0.0;
        for (double signal : recent_signals_) {
            sum += std::abs(signal);
        }
        
        return std::min(1.0, sum / recent_signals_.size() / 0.1); // Normalize
    }
    
private:
    double calculate_signal_confidence(double vol_diff, const SignalParams& params) {
        // Base confidence from volatility difference magnitude
        double vol_confidence = std::min(1.0, std::abs(vol_diff) / (2.0 * params.entry_threshold));
        
        // Historical performance factor
        double performance_factor = get_signal_performance();
        
        // Combined confidence
        double confidence = std::sqrt(vol_confidence * performance_factor);
        
        return std::max(0.1, std::min(0.95, confidence));
    }
};

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

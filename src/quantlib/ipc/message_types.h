// src/quantlib/ipc/message_types.h
#pragma once

#include <cstdint>
#include <cstring>

namespace Alaris::IPC {

// Cache-aligned message structures for zero-copy communication
struct alignas(64) MarketDataMessage {
    uint64_t timestamp;
    uint32_t symbol_id;
    double bid;
    double ask;
    double underlying_price;
    double bid_iv;
    double ask_iv;
    uint32_t bid_size;
    uint32_t ask_size;
    char padding[8]; // Cache line alignment
    
    MarketDataMessage() : timestamp(0), symbol_id(0), bid(0), ask(0), 
                         underlying_price(0), bid_iv(0), ask_iv(0),
                         bid_size(0), ask_size(0) {
        std::memset(padding, 0, sizeof(padding));
    }
};

struct alignas(64) TradingSignalMessage {
    uint64_t timestamp;
    uint32_t symbol_id;
    double theoretical_price;
    double market_price;
    double implied_volatility;
    double forecast_volatility;
    double confidence;
    int32_t quantity;
    uint8_t side; // 0=buy, 1=sell
    uint8_t urgency; // 0-255, higher = more urgent
    uint8_t signal_type; // 0=entry, 1=exit, 2=adjustment
    char padding[25]; // Cache line alignment
    
    TradingSignalMessage() : timestamp(0), symbol_id(0), theoretical_price(0),
                           market_price(0), implied_volatility(0), 
                           forecast_volatility(0), confidence(0), quantity(0),
                           side(0), urgency(0), signal_type(0) {
        std::memset(padding, 0, sizeof(padding));
    }
};

struct alignas(64) ControlMessage {
    uint64_t timestamp;
    uint32_t message_type;
    uint32_t parameter1;
    uint32_t parameter2;
    double value1;
    double value2;
    char data[32];
    
    ControlMessage() : timestamp(0), message_type(0), parameter1(0),
                      parameter2(0), value1(0), value2(0) {
        std::memset(data, 0, sizeof(data));
    }

    explicit ControlMessage(uint32_t type) : timestamp(0), message_type(type), parameter1(0),
                                           parameter2(0), value1(0), value2(0) {
        std::memset(data, 0, sizeof(data));
    }
};

// Message types for control channel
enum class ControlMessageType : uint32_t {
    START_TRADING = 1,
    STOP_TRADING = 2,
    UPDATE_PARAMETERS = 3,
    RESET_MODELS = 4,
    SYSTEM_STATUS = 5,
    HEARTBEAT = 6
};

} // namespace Alaris::IPC
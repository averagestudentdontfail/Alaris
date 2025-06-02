#pragma once

#include <chrono>

namespace Alaris::Core {

/**
 * @brief Unified timing types for the Alaris system
 * 
 * All timing-related components should use these common types
 * to ensure consistency across the system.
 */
namespace Timing {
    using Clock = std::chrono::high_resolution_clock;
    using TimePoint = Clock::time_point;
    using Duration = Clock::duration;
    
    // Common duration literals for convenience
    constexpr Duration MICROSECONDS_100 = std::chrono::microseconds(100);
    constexpr Duration MILLISECOND_1 = std::chrono::milliseconds(1);
    constexpr Duration MILLISECONDS_5 = std::chrono::milliseconds(5);
    constexpr Duration MILLISECONDS_10 = std::chrono::milliseconds(10);
    constexpr Duration SECOND_1 = std::chrono::seconds(1);
    constexpr Duration SECONDS_10 = std::chrono::seconds(10);
}

} // namespace Alaris::Core
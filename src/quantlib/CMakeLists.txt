# Alaris QuantLib Process Components

# Core libraries
add_library(alaris_core STATIC
    core/memory_pool.cpp
    core/time_trigger.cpp
    core/event_log.cpp
    ipc/shared_memory.cpp
)

# Pricing engine
add_library(alaris_pricing STATIC
    pricing/alo_engine.cpp
    pricing/pricing_service.cpp
)

# Volatility models
add_library(alaris_volatility STATIC
    volatility/gjrgarch_wrapper.cpp
    volatility/garch_wrapper.cpp
    volatility/vol_forecast.cpp
)

# Trading strategy
add_library(alaris_strategy STATIC
    strategy/vol_arb.cpp
    strategy/signal_gen.cpp
)

# Main QuantLib process executable
add_executable(quantlib_process
    main.cpp
)

# Link libraries
target_link_libraries(alaris_core 
    ${QuantLib_LIBRARIES}
    ${RT_LIB}
    ${PTHREAD_LIB}
    Threads::Threads
)

target_link_libraries(alaris_pricing
    alaris_core
    ${QuantLib_LIBRARIES}
)

target_link_libraries(alaris_volatility
    alaris_core
    ${QuantLib_LIBRARIES}
)

target_link_libraries(alaris_strategy
    alaris_core
    alaris_pricing
    alaris_volatility
    ${QuantLib_LIBRARIES}
)

target_link_libraries(quantlib_process
    alaris_core
    alaris_pricing
    alaris_volatility
    alaris_strategy
    ${QuantLib_LIBRARIES}
    ${RT_LIB}
    ${PTHREAD_LIB}
    Threads::Threads
)

# Compiler flags for real-time performance
target_compile_options(quantlib_process PRIVATE
    -O3 -march=native -mtune=native
    -fno-omit-frame-pointer
)

# Real-time linker flags
target_link_options(quantlib_process PRIVATE
    -Wl,--no-as-needed -lrt -lpthread
)

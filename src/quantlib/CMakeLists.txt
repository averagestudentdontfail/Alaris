# src/quantlib/CMakeLists.txt
# Alaris QuantLib Process Components

message(STATUS "Configuring Alaris QuantLib C++ components...")

# Core utilities library
create_modern_library(alaris_core
    SOURCES
        core/memory_pool.cpp
        core/time_trigger.cpp
        core/event_log.cpp
        ipc/shared_memory.cpp        # Implements SharedRingBuffer
        ipc/shared_memory_manager.cpp # Implements SharedMemoryManager (was process_manager.cpp)
    HEADERS
        core/memory_pool.h
        core/time_trigger.h
        core/event_log.h
        ipc/shared_ring_buffer.h     # Definition for SharedRingBuffer
        ipc/shared_memory.h          # Definition for SharedMemoryManager
        ipc/message_types.h
    PUBLIC_DEPS
        ${YAML_CPP_TARGET}
    PRIVATE_DEPS
        ${ALARIS_COMMON_LINK_LIBRARIES}
)
message(STATUS "  + Configured alaris_core library")

# Pricing engine library
create_modern_library(alaris_pricing
    SOURCES
        pricing/alo_engine.cpp
    HEADERS
        pricing/alo_engine.h
    PUBLIC_DEPS
        alaris_core
    PRIVATE_DEPS
        ${ALARIS_COMMON_LINK_LIBRARIES}
)
message(STATUS "  + Configured alaris_pricing library")

# Volatility models library
create_modern_library(alaris_volatility
    SOURCES
        volatility/gjrgarch_wrapper.cpp
        volatility/vol_forecast.cpp
    HEADERS
        volatility/gjrgarch_wrapper.h
        volatility/vol_forecast.h
    PUBLIC_DEPS
        alaris_core
    PRIVATE_DEPS
        ${ALARIS_COMMON_LINK_LIBRARIES}
)
message(STATUS "  + Configured alaris_volatility library (GJR-GARCH focused)")

# Trading strategy library
# Removed strategy/signal_gen.cpp from sources
create_modern_library(alaris_strategy
    SOURCES
        strategy/vol_arb.cpp
    HEADERS
        strategy/vol_arb.h
        # strategy/signal_gen.h # This header is also removed from the project
    PUBLIC_DEPS
        alaris_core
        alaris_pricing
        alaris_volatility
    PRIVATE_DEPS
        ${ALARIS_COMMON_LINK_LIBRARIES}
)
message(STATUS "  + Configured alaris_strategy library")

# Main QuantLib process executable
create_modern_executable(quantlib_process
    SOURCES
        main.cpp
    DEPS
        alaris_core
        alaris_pricing
        alaris_volatility
        alaris_strategy
        ${YAML_CPP_TARGET}
        ${ALARIS_COMMON_LINK_LIBRARIES}
)
message(STATUS "  + Configured quantlib_process executable")

# Platform-specific optimizations and linker flags for the main executable
if(CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64|AMD64")
    target_compile_options(quantlib_process PRIVATE
        $<$<CONFIG:Release>:-msse4.2>
        $<$<CONFIG:Release>:-mavx>
        $<$<CONFIG:Release>:-mavx2>
    )
endif()

if(CMAKE_BUILD_TYPE STREQUAL "Release" AND NOT WIN32)
    target_link_options(quantlib_process PRIVATE
        -Wl,--no-as-needed
    )
endif()

# Debug-specific configurations for the main executable
if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    target_compile_definitions(quantlib_process PRIVATE
        ALARIS_DEBUG_BUILD
        ALARIS_ENABLE_PROFILING
    )
    if(ENABLE_SANITIZERS)
        target_compile_options(quantlib_process PRIVATE
            -fsanitize=address
            -fsanitize=undefined
            -fno-sanitize-recover=all
        )
        target_link_options(quantlib_process PRIVATE
            -fsanitize=address
            -fsanitize=undefined
        )
    endif()
endif()

# Tools: alaris_config_validator
create_modern_executable(alaris_config_validator
    SOURCES
        tools/config_validator.cpp
    DEPS
        alaris_core
        ${YAML_CPP_TARGET}
        ${ALARIS_COMMON_LINK_LIBRARIES}
)
message(STATUS "  + Configured alaris_config_validator tool")

# Tools: alaris_sysinfo
create_modern_executable(alaris_sysinfo
    SOURCES
        tools/system_info.cpp
    DEPS
        alaris_core
        ${ALARIS_COMMON_LINK_LIBRARIES}
)
message(STATUS "  + Configured alaris_sysinfo tool")

# Target include directories for generated headers (if any were to be re-added)
# target_include_directories(quantlib_process PRIVATE "${CMAKE_CURRENT_BINARY_DIR}")

# Set up folder organization for IDE
set_target_properties(
    alaris_core
    alaris_pricing
    alaris_volatility
    alaris_strategy
    PROPERTIES FOLDER "Alaris/QuantLib/Libraries"
)
set_target_properties(
    quantlib_process
    PROPERTIES FOLDER "Alaris/QuantLib/Executables"
)
set_target_properties(
    alaris_config_validator
    alaris_sysinfo
    PROPERTIES FOLDER "Alaris/QuantLib/Tools"
)

# Export targets to parent scope
set(ALARIS_QUANTLIB_LIBRARIES
    alaris_core
    alaris_pricing
    alaris_volatility
    alaris_strategy
    PARENT_SCOPE
)
set(ALARIS_QUANTLIB_EXECUTABLES
    quantlib_process
    PARENT_SCOPE
)
set(ALARIS_QUANTLIB_TOOLS
    alaris_config_validator
    alaris_sysinfo
    PARENT_SCOPE
)

message(STATUS "Alaris QuantLib C++ components configuration finished.")
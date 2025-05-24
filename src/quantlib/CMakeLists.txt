# Alaris QuantLib Process Components

# Set common include directories
set(COMMON_INCLUDE_DIRS
    ${CMAKE_SOURCE_DIR}/src
    ${CMAKE_SOURCE_DIR}/src/quantlib
    ${QuantLib_INCLUDE_DIRS}
    ${yaml-cpp_INCLUDE_DIRS}
)

# Common compile features and options
set(COMMON_COMPILE_OPTIONS
    -Wall
    -Wextra
    -Wpedantic
    -Wno-unused-parameter
    -fno-omit-frame-pointer
)

# Core libraries
add_library(alaris_core STATIC
    core/memory_pool.cpp
    core/time_trigger.cpp
    core/event_log.cpp
    ipc/shared_memory.cpp
)

target_include_directories(alaris_core PUBLIC ${COMMON_INCLUDE_DIRS})
target_compile_options(alaris_core PRIVATE ${COMMON_COMPILE_OPTIONS})

# Core library linking
target_link_libraries(alaris_core PUBLIC
    ${QUANTLIB_TARGET}
    ${SYSTEM_LIBS}
    Threads::Threads
)

# Add explicit dependency if using external QuantLib
if(NOT QuantLib_FOUND AND TARGET ${QUANTLIB_TARGET})
    add_dependencies(alaris_core ${QUANTLIB_TARGET})
endif()

# Pricing engine library
add_library(alaris_pricing STATIC
    pricing/alo_engine.cpp
    pricing/pricing_service.cpp
)

target_include_directories(alaris_pricing PUBLIC ${COMMON_INCLUDE_DIRS})
target_compile_options(alaris_pricing PRIVATE ${COMMON_COMPILE_OPTIONS})

target_link_libraries(alaris_pricing PUBLIC
    alaris_core
    ${QUANTLIB_TARGET}
)

# Add explicit dependency if using external QuantLib
if(NOT QuantLib_FOUND AND TARGET ${QUANTLIB_TARGET})
    add_dependencies(alaris_pricing ${QUANTLIB_TARGET})
endif()

# Volatility models library
add_library(alaris_volatility STATIC
    volatility/gjrgarch_wrapper.cpp
    volatility/garch_wrapper.cpp
    volatility/vol_forecast.cpp
)

target_include_directories(alaris_volatility PUBLIC ${COMMON_INCLUDE_DIRS})
target_compile_options(alaris_volatility PRIVATE ${COMMON_COMPILE_OPTIONS})

target_link_libraries(alaris_volatility PUBLIC
    alaris_core
    ${QUANTLIB_TARGET}
)

# Add explicit dependency if using external QuantLib
if(NOT QuantLib_FOUND AND TARGET ${QUANTLIB_TARGET})
    add_dependencies(alaris_volatility ${QUANTLIB_TARGET})
endif()

# Trading strategy library
add_library(alaris_strategy STATIC
    strategy/vol_arb.cpp
    strategy/signal_gen.cpp
)

target_include_directories(alaris_strategy PUBLIC ${COMMON_INCLUDE_DIRS})
target_compile_options(alaris_strategy PRIVATE ${COMMON_COMPILE_OPTIONS})

target_link_libraries(alaris_strategy PUBLIC
    alaris_core
    alaris_pricing
    alaris_volatility
    ${QUANTLIB_TARGET}
)

# Add explicit dependency if using external QuantLib
if(NOT QuantLib_FOUND AND TARGET ${QUANTLIB_TARGET})
    add_dependencies(alaris_strategy ${QUANTLIB_TARGET})
endif()

# Main QuantLib process executable
add_executable(quantlib_process
    main.cpp
)

target_include_directories(quantlib_process PRIVATE ${COMMON_INCLUDE_DIRS})

# Executable-specific compile options for performance
target_compile_options(quantlib_process PRIVATE
    ${COMMON_COMPILE_OPTIONS}
    -O3
    -march=native
    -mtune=native
    -flto
)

# Link all libraries to the main executable
target_link_libraries(quantlib_process PRIVATE
    alaris_core
    alaris_pricing
    alaris_volatility
    alaris_strategy
    ${QUANTLIB_TARGET}
    ${SYSTEM_LIBS}
    Threads::Threads
    ${YAML_CPP_TARGET}
)

# Add explicit dependencies for external libraries
if(NOT QuantLib_FOUND AND TARGET ${QUANTLIB_TARGET})
    add_dependencies(quantlib_process ${QUANTLIB_TARGET})
endif()
if(NOT yaml-cpp_FOUND AND TARGET ${YAML_CPP_TARGET})
    add_dependencies(quantlib_process ${YAML_CPP_TARGET})
endif()

# Real-time and performance linker flags
target_link_options(quantlib_process PRIVATE
    -Wl,--no-as-needed
    -flto
)

# Set executable properties
set_target_properties(quantlib_process PROPERTIES
    OUTPUT_NAME "quantlib_process"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

# Platform-specific optimizations
if(CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64|AMD64")
    target_compile_options(quantlib_process PRIVATE -msse4.2 -mavx -mavx2)
endif()

# Debug vs Release specific settings
if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    target_compile_definitions(quantlib_process PRIVATE DEBUG_BUILD)
    target_compile_options(quantlib_process PRIVATE -O0 -g -fno-inline)
elseif(CMAKE_BUILD_TYPE STREQUAL "Release")
    target_compile_definitions(quantlib_process PRIVATE NDEBUG RELEASE_BUILD)
    target_compile_options(quantlib_process PRIVATE -DNDEBUG -fomit-frame-pointer)
endif()

# Create convenience targets for individual libraries
add_custom_target(core DEPENDS alaris_core)
add_custom_target(pricing DEPENDS alaris_pricing)
add_custom_target(volatility DEPENDS alaris_volatility)
add_custom_target(strategy DEPENDS alaris_strategy)
add_custom_target(main DEPENDS quantlib_process)

# Print library information
get_target_property(ALARIS_CORE_SOURCES alaris_core SOURCES)
get_target_property(ALARIS_PRICING_SOURCES alaris_pricing SOURCES)
get_target_property(ALARIS_VOLATILITY_SOURCES alaris_volatility SOURCES)
get_target_property(ALARIS_STRATEGY_SOURCES alaris_strategy SOURCES)

message(STATUS "Alaris libraries configured:")
message(STATUS "  - alaris_core: ${ALARIS_CORE_SOURCES}")
message(STATUS "  - alaris_pricing: ${ALARIS_PRICING_SOURCES}")
message(STATUS "  - alaris_volatility: ${ALARIS_VOLATILITY_SOURCES}")
message(STATUS "  - alaris_strategy: ${ALARIS_STRATEGY_SOURCES}")
message(STATUS "  - quantlib_process: main.cpp")
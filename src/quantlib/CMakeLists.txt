# Enhanced src/quantlib/CMakeLists.txt
# Alaris QuantLib Process Components with robust dependency management

message(STATUS "Configuring Alaris QuantLib components...")

# Verify required targets are available
if(NOT TARGET ${QUANTLIB_TARGET})
    message(FATAL_ERROR "QuantLib target '${QUANTLIB_TARGET}' not found")
endif()

if(NOT TARGET ${YAML_CPP_TARGET})
    message(FATAL_ERROR "yaml-cpp target '${YAML_CPP_TARGET}' not found")
endif()

# Common configuration for all Alaris libraries
set(ALARIS_COMMON_INCLUDE_DIRS
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_SOURCE_DIR}/src
    ${QuantLib_INCLUDE_DIRS}
    ${yaml-cpp_INCLUDE_DIRS}
)

set(ALARIS_COMMON_COMPILE_OPTIONS
    -Wall
    -Wextra
    -Wpedantic
    -Wno-unused-parameter
    -fno-omit-frame-pointer
    $<$<CONFIG:Release>:-O3>
    $<$<CONFIG:Release>:-march=native>
    $<$<CONFIG:Release>:-mtune=native>
    $<$<CONFIG:Release>:-flto>
    $<$<CONFIG:Debug>:-O0>
    $<$<CONFIG:Debug>:-g>
    $<$<CONFIG:Debug>:-fno-inline>
)

set(ALARIS_COMMON_LINK_LIBRARIES
    ${QUANTLIB_TARGET}
    ${SYSTEM_LIBS}
    Threads::Threads
)

# Platform-specific libraries
if(TARGET Platform::Libraries)
    list(APPEND ALARIS_COMMON_LINK_LIBRARIES Platform::Libraries)
endif()

# Function to create Alaris library with common settings
function(add_alaris_library target_name)
    cmake_parse_arguments(
        ARG
        "HEADER_ONLY"
        ""
        "SOURCES;HEADERS;DEPENDENCIES;PRIVATE_DEPENDENCIES"
        ${ARGN}
    )
    
    if(ARG_HEADER_ONLY)
        add_library(${target_name} INTERFACE)
        target_include_directories(${target_name} INTERFACE ${ALARIS_COMMON_INCLUDE_DIRS})
        if(ARG_DEPENDENCIES)
            target_link_libraries(${target_name} INTERFACE ${ARG_DEPENDENCIES})
        endif()
    else()
        add_library(${target_name} STATIC ${ARG_SOURCES} ${ARG_HEADERS})
        
        target_include_directories(${target_name} 
            PUBLIC ${ALARIS_COMMON_INCLUDE_DIRS}
        )
        
        target_compile_options(${target_name} 
            PRIVATE ${ALARIS_COMMON_COMPILE_OPTIONS}
        )
        
        target_link_libraries(${target_name} 
            PUBLIC ${ALARIS_COMMON_LINK_LIBRARIES}
        )
        
        if(ARG_DEPENDENCIES)
            target_link_libraries(${target_name} PUBLIC ${ARG_DEPENDENCIES})
        endif()
        
        if(ARG_PRIVATE_DEPENDENCIES)
            target_link_libraries(${target_name} PRIVATE ${ARG_PRIVATE_DEPENDENCIES})
        endif()
        
        # Set folder for IDE organization
        set_target_properties(${target_name} PROPERTIES FOLDER "Alaris/Libraries")
        
        # Add deterministic execution flags
        target_compile_definitions(${target_name} PRIVATE
            $<$<CONFIG:Release>:NDEBUG>
            $<$<CONFIG:Release>:ALARIS_OPTIMIZED>
            $<$<CONFIG:Debug>:ALARIS_DEBUG>
        )
    endif()
    
    message(STATUS "Configured library: ${target_name}")
endfunction()

# Core utilities library
add_alaris_library(alaris_core
    SOURCES
        core/memory_pool.cpp
        core/time_trigger.cpp
        core/event_log.cpp
        ipc/shared_memory.cpp
        ipc/process_manager.cpp
    HEADERS
        core/memory_pool.h
        core/time_trigger.h
        core/event_log.h
        ipc/shared_memory.h
        ipc/shared_ring_buffer.h
        ipc/message_types.h
)

# Additional dependencies for core library
target_link_libraries(alaris_core PUBLIC ${YAML_CPP_TARGET})

# Add explicit dependencies to ensure build order
add_dependencies(alaris_core ${QUANTLIB_TARGET} ${YAML_CPP_TARGET})

# Pricing engine library
add_alaris_library(alaris_pricing
    SOURCES
        pricing/alo_engine.cpp
        pricing/pricing_service.cpp
    HEADERS
        pricing/alo_engine.h
        pricing/pricing_service.h
    DEPENDENCIES
        alaris_core
)

add_dependencies(alaris_pricing alaris_core)

# Volatility models library
add_alaris_library(alaris_volatility
    SOURCES
        volatility/gjrgarch_wrapper.cpp
        volatility/garch_wrapper.cpp
        volatility/vol_forecast.cpp
    HEADERS
        volatility/gjrgarch_wrapper.h
        volatility/garch_wrapper.h
        volatility/vol_forecast.h
    DEPENDENCIES
        alaris_core
)

add_dependencies(alaris_volatility alaris_core)

# Trading strategy library
add_alaris_library(alaris_strategy
    SOURCES
        strategy/vol_arb.cpp
        strategy/signal_gen.cpp
    HEADERS
        strategy/vol_arb.h
        strategy/signal_gen.h
    DEPENDENCIES
        alaris_core
        alaris_pricing
        alaris_volatility
)

add_dependencies(alaris_strategy alaris_core alaris_pricing alaris_volatility)

# Main QuantLib process executable
add_executable(quantlib_process main.cpp)

target_include_directories(quantlib_process PRIVATE ${ALARIS_COMMON_INCLUDE_DIRS})

# Executable-specific compile options for maximum performance
target_compile_options(quantlib_process PRIVATE
    ${ALARIS_COMMON_COMPILE_OPTIONS}
    $<$<CONFIG:Release>:-ffast-math>
    $<$<CONFIG:Release>:-funroll-loops>
)

# Link all libraries to the main executable
target_link_libraries(quantlib_process PRIVATE
    alaris_core
    alaris_pricing
    alaris_volatility
    alaris_strategy
    ${YAML_CPP_TARGET}
    ${ALARIS_COMMON_LINK_LIBRARIES}
)

# Add dependencies to ensure proper build order
add_dependencies(quantlib_process 
    alaris_core 
    alaris_pricing 
    alaris_volatility 
    alaris_strategy
)

# Set executable properties
set_target_properties(quantlib_process PROPERTIES
    OUTPUT_NAME "quantlib_process"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    FOLDER "Alaris/Executables"
)

# Platform-specific optimizations and linker flags
if(CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64|AMD64")
    target_compile_options(quantlib_process PRIVATE
        $<$<CONFIG:Release>:-msse4.2>
        $<$<CONFIG:Release>:-mavx>
        $<$<CONFIG:Release>:-mavx2>
    )
endif()

# Real-time and performance linker flags
if(CMAKE_BUILD_TYPE STREQUAL "Release")
    if(NOT WIN32)
        target_link_options(quantlib_process PRIVATE
            -Wl,--no-as-needed
            -flto
        )
    endif()
endif()

# Debug-specific configurations
if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    target_compile_definitions(quantlib_process PRIVATE
        ALARIS_DEBUG_BUILD
        ALARIS_ENABLE_PROFILING
    )
    
    # Enable sanitizers if requested
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

# Create convenience targets for individual components
add_custom_target(core DEPENDS alaris_core)
add_custom_target(pricing DEPENDS alaris_pricing)
add_custom_target(volatility DEPENDS alaris_volatility)
add_custom_target(strategy DEPENDS alaris_strategy)
add_custom_target(main DEPENDS quantlib_process)

# Performance testing executable (optional)
if(BENCHMARK_AVAILABLE)
    add_executable(alaris_benchmark
        benchmark/pricing_benchmark.cpp
        benchmark/volatility_benchmark.cpp
        benchmark/memory_benchmark.cpp
    )
    
    target_include_directories(alaris_benchmark PRIVATE ${ALARIS_COMMON_INCLUDE_DIRS})
    target_compile_options(alaris_benchmark PRIVATE ${ALARIS_COMMON_COMPILE_OPTIONS})
    
    target_link_libraries(alaris_benchmark PRIVATE
        alaris_core
        alaris_pricing
        alaris_volatility
        alaris_strategy
        benchmark::benchmark
        benchmark::benchmark_main
        ${ALARIS_COMMON_LINK_LIBRARIES}
    )
    
    set_target_properties(alaris_benchmark PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
        FOLDER "Alaris/Benchmarks"
    )
    
    message(STATUS "Benchmark executable configured: alaris_benchmark")
endif()

# Configuration validation executable for deployment
add_executable(alaris_config_validator
    tools/config_validator.cpp
)

target_include_directories(alaris_config_validator PRIVATE ${ALARIS_COMMON_INCLUDE_DIRS})
target_compile_options(alaris_config_validator PRIVATE ${ALARIS_COMMON_COMPILE_OPTIONS})

target_link_libraries(alaris_config_validator PRIVATE
    alaris_core
    ${YAML_CPP_TARGET}
    ${ALARIS_COMMON_LINK_LIBRARIES}
)

set_target_properties(alaris_config_validator PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    FOLDER "Alaris/Tools"
)

# System information utility
add_executable(alaris_sysinfo
    tools/system_info.cpp
)

target_include_directories(alaris_sysinfo PRIVATE ${ALARIS_COMMON_INCLUDE_DIRS})
target_compile_options(alaris_sysinfo PRIVATE ${ALARIS_COMMON_COMPILE_OPTIONS})

target_link_libraries(alaris_sysinfo PRIVATE
    alaris_core
    ${ALARIS_COMMON_LINK_LIBRARIES}
)

set_target_properties(alaris_sysinfo PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    FOLDER "Alaris/Tools"
)

# Create build information header
configure_file(
    "${CMAKE_CURRENT_SOURCE_DIR}/alaris_build_info.h.in"
    "${CMAKE_CURRENT_BINARY_DIR}/alaris_build_info.h"
    @ONLY
)

target_include_directories(quantlib_process PRIVATE ${CMAKE_CURRENT_BINARY_DIR})

# Installation targets
install(TARGETS quantlib_process alaris_config_validator alaris_sysinfo
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
    COMPONENT Runtime
)

if(TARGET alaris_benchmark)
    install(TARGETS alaris_benchmark
        RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
        COMPONENT Benchmarks
        OPTIONAL
    )
endif()

# Development installation (headers and static libraries)
if(CMAKE_BUILD_TYPE STREQUAL "Debug" OR ALARIS_INSTALL_DEVELOPMENT)
    install(TARGETS 
            alaris_core 
            alaris_pricing 
            alaris_volatility 
            alaris_strategy
        ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
        COMPONENT Development
    )
endif()

# Create a combined target for all components
add_custom_target(alaris_all DEPENDS
    alaris_core
    alaris_pricing
    alaris_volatility
    alaris_strategy
    quantlib_process
    alaris_config_validator
    alaris_sysinfo
)

if(TARGET alaris_benchmark)
    add_dependencies(alaris_all alaris_benchmark)
endif()

# Print build summary for this module
message(STATUS "Alaris QuantLib components configured:")
message(STATUS "  ✓ alaris_core: Core utilities and memory management")
message(STATUS "  ✓ alaris_pricing: QuantLib ALO option pricing engine")
message(STATUS "  ✓ alaris_volatility: GJR-GARCH and GARCH models")
message(STATUS "  ✓ alaris_strategy: Volatility arbitrage strategy")
message(STATUS "  ✓ quantlib_process: Main trading process executable")
message(STATUS "  ✓ alaris_config_validator: Configuration validation tool")
message(STATUS "  ✓ alaris_sysinfo: System information utility")

if(TARGET alaris_benchmark)
    message(STATUS "  ✓ alaris_benchmark: Performance benchmarking suite")
endif()

# Development convenience aliases
if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    # Create symlinks for easier development (Unix only)
    if(UNIX AND NOT APPLE)
        add_custom_command(TARGET quantlib_process POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E create_symlink
                ${CMAKE_BINARY_DIR}/bin/quantlib_process
                ${CMAKE_SOURCE_DIR}/quantlib_process
            COMMENT "Creating development symlink"
        )
    endif()
endif()
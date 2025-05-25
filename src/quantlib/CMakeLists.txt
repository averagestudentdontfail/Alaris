# src/quantlib/CMakeLists.txt
message(STATUS "Configuring QuantLib process components...")

# Create the component libraries

# Core library
add_library(alaris_core STATIC
    core/memory_pool.cpp
    core/time_trigger.cpp
    core/event_log.cpp
    ipc/shared_memory.cpp
    ipc/shared_memory_manager.cpp
)
target_include_directories(alaris_core PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>
    $<INSTALL_INTERFACE:include/alaris>
)
target_link_libraries(alaris_core 
    PUBLIC 
        ${YAML_CPP_TARGET}
        Threads::Threads
)
setup_compile_options(alaris_core)

# Pricing library
add_library(alaris_pricing STATIC
    pricing/alo_engine.cpp
)
target_include_directories(alaris_pricing PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>
    $<INSTALL_INTERFACE:include/alaris>
)
target_link_libraries(alaris_pricing 
    PUBLIC 
        alaris_core
        ${QUANTLIB_TARGET}
)
setup_compile_options(alaris_pricing)

# Volatility library
add_library(alaris_volatility STATIC
    volatility/gjrgarch_wrapper.cpp
    volatility/vol_forecast.cpp
)
target_include_directories(alaris_volatility PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>
    $<INSTALL_INTERFACE:include/alaris>
)
target_link_libraries(alaris_volatility 
    PUBLIC 
        alaris_core
        ${QUANTLIB_TARGET}
)
setup_compile_options(alaris_volatility)

# Strategy library
add_library(alaris_strategy STATIC
    strategy/vol_arb.cpp
)
target_include_directories(alaris_strategy PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>
    $<INSTALL_INTERFACE:include/alaris>
)
target_link_libraries(alaris_strategy 
    PUBLIC 
        alaris_core
        alaris_pricing
        alaris_volatility
)
setup_compile_options(alaris_strategy)

# Main executable
add_executable(quantlib_process main.cpp)
target_link_libraries(quantlib_process PRIVATE
    alaris_core
    alaris_pricing
    alaris_volatility
    alaris_strategy
    ${YAML_CPP_TARGET}
    ${QUANTLIB_TARGET}
    Threads::Threads
    rt
)
set_target_properties(quantlib_process PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
)
setup_compile_options(quantlib_process)

# Platform-specific optimizations
if(CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64|AMD64")
    target_compile_options(quantlib_process PRIVATE
        $<$<CONFIG:Release>:-march=native>
    )
endif()

# Tools
add_executable(alaris_config_validator tools/config_validator.cpp)
target_link_libraries(alaris_config_validator PRIVATE
    alaris_core
    ${YAML_CPP_TARGET}
)
set_target_properties(alaris_config_validator PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
)

add_executable(alaris_sysinfo tools/system_info.cpp)
target_link_libraries(alaris_sysinfo PRIVATE
    alaris_core
)
set_target_properties(alaris_sysinfo PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
)

# Installation
install(TARGETS 
    alaris_core alaris_pricing alaris_volatility alaris_strategy
    quantlib_process alaris_config_validator alaris_sysinfo
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
    ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
)

# Install headers
install(DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/
    DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}/alaris
    FILES_MATCHING PATTERN "*.h" PATTERN "*.hpp"
)
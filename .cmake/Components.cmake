# .cmake/Text/Components.txt
# Component definitions and organization

# Define QuantLib components
set(QUANTLIB_COMPONENTS
    pricing
    volatility
    strategy
    ipc
    core
    tools
)

# Define QuantLib source files with correct paths
set(QUANTLIB_CORE_SOURCES # Renamed for clarity, as this will build 'quantlib_core'
    ${CMAKE_SOURCE_DIR}/src/quantlib/pricing/alo_engine.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/garch_wrapper.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/vol_forecast.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/strategy/vol_arb.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_memory.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_memory_manager.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/memory_pool.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/time_trigger.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/event_log.cpp
    # main.cpp for the 'alaris' executable is handled separately
)

# Define QuantLib header files (for reference, not for add_library sources)
# These are made available via target_include_directories
set(QUANTLIB_HEADERS_LIST
    ${CMAKE_SOURCE_DIR}/src/quantlib/pricing/alo_engine.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/garch_wrapper.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/vol_forecast.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/strategy/vol_arb.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_ring_buffer.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_memory.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/message_types.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/memory_pool.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/time_trigger.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/event_log.h
)

# Function to create the quantlib_core library
function(create_component_library NAME)
    add_library(${NAME} STATIC
        ${QUANTLIB_CORE_SOURCES} # Only .cpp files
    )

    target_include_directories(${NAME} PUBLIC
        ${CMAKE_SOURCE_DIR}/src # This allows includes like "quantlib/pricing/alo_engine.h" from within the library
        ${CMAKE_SOURCE_DIR}/external/quant # For QuantLib headers from submodule
    )

    target_link_libraries(${NAME} PUBLIC
        ${QUANTLIB_TARGET} # Use the variable holding the correct QuantLib target name
        yaml-cpp           # From external/yaml-cpp
        Threads::Threads
    )
    message(STATUS "${NAME} library configured with sources: ${QUANTLIB_CORE_SOURCES}")
endfunction()

# Function to configure all C++ components (libraries and executables)
function(configure_all_components)
    # Create the main quantlib_core library
    create_component_library(quantlib_core)

    # Configure the main 'alaris' executable
    add_executable(alaris ${CMAKE_SOURCE_DIR}/src/quantlib/main.cpp)
    # Includes for 'alaris' executable
    target_include_directories(alaris PRIVATE
        ${CMAKE_SOURCE_DIR}/src                # Allows #include "quantlib/..." for its own sources
        ${CMAKE_SOURCE_DIR}/external/quant     # For QuantLib headers
    )
    target_link_libraries(alaris PRIVATE quantlib_core) 
    message(STATUS "alaris executable configured.")

    # Configure tool executables
    add_executable(alaris-config-validator ${CMAKE_SOURCE_DIR}/src/quantlib/tools/config_validator.cpp)
    target_include_directories(alaris-config-validator PRIVATE
        ${CMAKE_SOURCE_DIR}/src
        ${CMAKE_SOURCE_DIR}/external/yaml-cpp/include # If yaml-cpp headers are needed directly
    )
    target_link_libraries(alaris-config-validator PRIVATE quantlib_core yaml-cpp)
    message(STATUS "alaris-config-validator executable configured.")

    add_executable(alaris-system-info ${CMAKE_SOURCE_DIR}/src/quantlib/tools/system_info.cpp)
    target_include_directories(alaris-system-info PRIVATE
        ${CMAKE_SOURCE_DIR}/src
    )
    target_link_libraries(alaris-system-info PRIVATE quantlib_core) 
    message(STATUS "alaris-system-info executable configured.")
endfunction()
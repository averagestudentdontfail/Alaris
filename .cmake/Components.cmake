# .cmake/Components.cmake
# Defines the C++ libraries and executables for the Alaris system.

# Define the source files for the core 'quantlib' static library.
set(QUANTLIB_CORE_SOURCES
    ${CMAKE_SOURCE_DIR}/src/quantlib/pricing/alo_engine.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/garch_wrapper.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/vol_forecast.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/strategy/vol_arb.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_memory.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_memory_manager.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/memory_pool.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/task_scheduler.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/event_log.cpp
)

# This function configures all the C++ build targets.
function(configure_all_components)
    message(STATUS "Components: Configuring C++ build targets...")

    # 1. Create the core 'quantlib' static library from the sources.
    #    All executables will link against this library.
    add_library(quantlib STATIC ${QUANTLIB_CORE_SOURCES})

    # Define include directories for the 'quantlib' library.
    target_include_directories(quantlib PUBLIC
        # Allow includes like "quantlib/pricing/alo_engine.h" from source files.
        $<BUILD_INTERFACE:${CMAKE_SOURCE_DIR}/src>
    )

    # Link the 'quantlib' library against its external dependencies.
    target_link_libraries(quantlib PUBLIC
        ${QUANTLIB_TARGET}      # From External.cmake (QuantLib)
        ${YAML_CPP_TARGET}      # From External.cmake (yaml-cpp)
        Threads::Threads
    )

    # 2. Create the main C++ executables.
    
    # The 'quantlib-process' executable, which is the main C++ trading engine process.
    add_executable(quantlib-process ${CMAKE_SOURCE_DIR}/src/quantlib/main.cpp)
    target_link_libraries(quantlib-process PRIVATE quantlib)

    # The 'alaris' executable (can be a duplicate or for other purposes).
    add_executable(alaris ${CMAKE_SOURCE_DIR}/src/quantlib/main.cpp)
    target_link_libraries(alaris PRIVATE quantlib)
    
    # 3. Create the tool executables.

    # The 'alaris-config' tool for configuration management.
    add_executable(alaris-config ${CMAKE_SOURCE_DIR}/src/quantlib/tools/config.cpp)
    target_link_libraries(alaris-config PRIVATE quantlib)

    # The 'alaris-system' tool for system diagnostics.
    add_executable(alaris-system ${CMAKE_SOURCE_DIR}/src/quantlib/tools/system.cpp)
    target_link_libraries(alaris-system PRIVATE quantlib)
    
    # 4. Create a manual target for setting Linux real-time capabilities.
    if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
        add_custom_target(set-capabilities
            COMMAND bash "${ALARIS_CAPABILITY_SCRIPT}"
            DEPENDS alaris quantlib-process
            COMMENT "Setting Linux capabilities for real-time performance (requires sudo)"
            VERBATIM
        )
        message(STATUS "Components: Created 'set-capabilities' target for manual setup.")
    endif()

    message(STATUS "Components: C++ build targets configured successfully.")
endfunction()
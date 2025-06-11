# Function to add capability and data management targets
function(add_capability_and_data_targets)
    # Capability targets (if enabled)
    if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
        # Create a target to set capabilities manually
        add_custom_target(set-capabilities
            COMMAND bash "${ALARIS_CAPABILITY_SCRIPT}"
            DEPENDS alaris quantlib-process
            COMMENT "Setting Linux capabilities for real-time performance"
            VERBATIM
        )
        
        # Create a target to check current capabilities
        add_custom_target(check-capabilities
            COMMAND echo "Checking capabilities for executables..."
            COMMAND bash -c "echo 'quantlib-process:' && getcap ${CMAKE_BINARY_DIR}/bin/quantlib-process || echo '  No capabilities set'"
            COMMAND bash -c "echo 'alaris:' && getcap ${CMAKE_BINARY_DIR}/bin/alaris || echo '  No capabilities set'"
            COMMENT "Checking current Linux capabilities"
            VERBATIM
        )
        
        # Create a target to remove capabilities
        add_custom_target(remove-capabilities
            COMMAND echo "Removing capabilities..."
            COMMAND bash -c "if [[ -f '${CMAKE_BINARY_DIR}/bin/quantlib-process' ]]; then sudo setcap -r '${CMAKE_BINARY_DIR}/bin/quantlib-process' && echo 'Removed from quantlib-process'; fi"
            COMMAND bash -c "if [[ -f '${CMAKE_BINARY_DIR}/bin/alaris' ]]; then sudo setcap -r '${CMAKE_BINARY_DIR}/bin/alaris' && echo 'Removed from alaris'; fi"
            COMMENT "Removing Linux capabilities"
            VERBATIM
        )
        
        message(STATUS "Components: Capability targets created (set-capabilities, check-capabilities, remove-capabilities)")
    endif()
    
    # Environment verification target (both data and capabilities)
    add_custom_target(verify-environment
        COMMAND echo "=== Alaris Environment Verification ==="
        COMMAND echo "Checking data environment..."
        COMMAND ${CMAKE_COMMAND} -P "${CMAKE_CURRENT_SOURCE_DIR}/.cmake/DataValidateTarget.cmake"
        COMMENT "Verifying complete Alaris environment"
        VERBATIM
    )
    
    if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
        add_custom_command(TARGET verify-environment POST_BUILD
            COMMAND echo "Checking capabilities..."
            COMMAND bash -c "getcap ${CMAKE_BINARY_DIR}/bin/quantlib-process || echo 'No capabilities set for quantlib-process'"
            COMMAND bash -c "getcap ${CMAKE_BINARY_DIR}/bin/alaris || echo 'No capabilities set for alaris'"
            COMMAND echo "=== Environment Verification Complete ==="
            VERBATIM
        )
    endif()
    
    message(STATUS "Components: Environment verification targets created")
endfunction()# .cmake/Components.cmake
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
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/task_scheduler.cpp
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
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/task_scheduler.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/time_type.h
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
    message(STATUS "Components: ${NAME} library configured with sources: ${QUANTLIB_CORE_SOURCES}")
endfunction()

# Function to configure Lean integration
function(configure_lean_integration)
    message(STATUS "Components: Configuring Lean integration...")
    
    # Copy config directory to build directory if it exists
    if(EXISTS "${CMAKE_SOURCE_DIR}/config")
        file(COPY "${CMAKE_SOURCE_DIR}/config"
             DESTINATION "${CMAKE_BINARY_DIR}")
        message(STATUS "Components: Config directory copied to build directory")
    endif()
    
    # Data setup is completely handled by Data.cmake module
    # Results and cache directories are created by Data.cmake
    # lean.json is generated by Data.cmake
    
    message(STATUS "Components: Lean integration configured (data managed by Data.cmake)")
endfunction()

# Function to add post-build data verification and capability setting
function(add_post_build_targets)
    # Add data verification for main executables
    if(ALARIS_AUTO_SETUP_DATA)
        add_custom_command(TARGET alaris POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E echo "Verifying data environment for Lean integration..."
            COMMAND ${CMAKE_COMMAND} -P "${CMAKE_CURRENT_SOURCE_DIR}/.cmake/DataValidateTarget.cmake"
            COMMENT "Verifying data environment"
            VERBATIM
        )
        
        add_custom_command(TARGET quantlib-process POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E echo "Data verification completed."
            COMMENT "Data verification for QuantLib process"
            VERBATIM
        )
    endif()
    
    # Add capability setting if enabled
    if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE AND ALARIS_AUTO_SET_CAPABILITIES)
        add_custom_command(TARGET quantlib-process POST_BUILD
            COMMAND echo "Auto-setting capabilities for quantlib-process..."
            COMMAND bash "${ALARIS_CAPABILITY_SCRIPT}"
            COMMENT "Automatically setting capabilities"
            VERBATIM
        )
        
        add_custom_command(TARGET alaris POST_BUILD
            COMMAND echo "Auto-setting capabilities for alaris..."
            COMMAND bash "${ALARIS_CAPABILITY_SCRIPT}"
            COMMENT "Automatically setting capabilities"
            VERBATIM
        )
        
        message(STATUS "Components: Auto-capability setting enabled (requires sudo during build)")
    endif()
    
    message(STATUS "Components: Post-build verification targets configured")
endfunction()

# Function to create development convenience targets
function(add_development_targets)
    # Quick development build target
    add_custom_target(dev
        DEPENDS alaris quantlib-process alaris-config alaris-system
        COMMENT "Building all development executables"
    )
    
    # Quick test target (builds and sets up everything for testing)
    add_custom_target(ready
        DEPENDS dev setup-data
        COMMENT "Building and setting up everything for development"
    )
    
    # Complete setup target (includes capabilities if enabled)
    if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
        add_custom_target(ready-rt
            DEPENDS ready set-capabilities
            COMMENT "Complete setup including real-time capabilities"
        )
        message(STATUS "Components: Created 'ready-rt' target for complete real-time setup")
    endif()
    
    # Environment-ready target (ensures both data and capabilities are set up)
    add_custom_target(ready-env
        DEPENDS ready verify-environment
        COMMENT "Complete environment setup with verification"
    )
    
    # Production-ready target (everything + validation)
    add_custom_target(ready-prod
        DEPENDS ready-env validate-data
        COMMENT "Production-ready setup with full validation"
    )
    
    if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
        add_dependencies(ready-prod set-capabilities)
    endif()
    
    message(STATUS "Components: Development targets created (dev, ready, ready-env, ready-prod)")
    if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
        message(STATUS "Components: Real-time target created (ready-rt)")
    endif()
endfunction()

# Function to configure all C++ components (libraries and executables)
function(configure_all_components)
    message(STATUS "Components: Configuring all components...")
    
    # Create the main quantlib library
    create_component_library(quantlib)

    # Configure the main 'alaris' executable
    add_executable(alaris ${CMAKE_SOURCE_DIR}/src/quantlib/main.cpp)
    target_include_directories(alaris PRIVATE
        ${CMAKE_SOURCE_DIR}/src                # Allows #include "quantlib/..." for its own sources
        ${CMAKE_SOURCE_DIR}/external/quant     # For QuantLib headers
    )
    target_link_libraries(alaris PRIVATE quantlib) 
    message(STATUS "Components: alaris executable configured")

    # Configure the quantlib-process executable
    add_executable(quantlib-process ${CMAKE_SOURCE_DIR}/src/quantlib/main.cpp)
    target_include_directories(quantlib-process PRIVATE
        ${CMAKE_SOURCE_DIR}/src
        ${CMAKE_SOURCE_DIR}/external/quant
    )
    target_link_libraries(quantlib-process PRIVATE quantlib)
    message(STATUS "Components: quantlib-process executable configured")

    # Configure tool executables
    add_executable(alaris-config ${CMAKE_SOURCE_DIR}/src/quantlib/tools/config.cpp)
    target_include_directories(alaris-config PRIVATE
        ${CMAKE_SOURCE_DIR}/src
        ${CMAKE_SOURCE_DIR}/external/yaml-cpp/include # If yaml-cpp headers are needed directly
    )
    target_link_libraries(alaris-config PRIVATE quantlib yaml-cpp)
    message(STATUS "Components: alaris-config executable configured")

    add_executable(alaris-system ${CMAKE_SOURCE_DIR}/src/quantlib/tools/system.cpp)
    target_include_directories(alaris-system PRIVATE
        ${CMAKE_SOURCE_DIR}/src
    )
    target_link_libraries(alaris-system PRIVATE quantlib) 
    message(STATUS "Components: alaris-system executable configured")

    # Configure Lean integration
    configure_lean_integration()
    
    # Add post-build verification and automation
    add_post_build_targets()
    
    # Add capability and data management targets
    add_capability_and_data_targets()
    
    # Add development convenience targets
    add_development_targets()
    
    message(STATUS "Components: All components configured successfully")
endfunction()

# Function to print post-build instructions
function(print_build_instructions)
    if(NOT ALARIS_SET_CAPABILITIES OR NOT ALARIS_CAPABILITIES_AVAILABLE)
        return()
    endif()
    
    message(STATUS "")
    message(STATUS "=== Post-Build Setup ===")
    if(ALARIS_AUTO_SET_CAPABILITIES)
        message(STATUS "  Capabilities will be set automatically after build")
    else()
        message(STATUS "  To enable real-time performance, run:")
        message(STATUS "    cmake --build . --target set-capabilities")
        message(STATUS "  Or manually:")
        message(STATUS "    bash ${CMAKE_BINARY_DIR}/set-capabilities.sh")
    endif()
    message(STATUS "  To check current capabilities:")
    message(STATUS "    cmake --build . --target check-capabilities")
    message(STATUS "  To remove capabilities:")
    message(STATUS "    cmake --build . --target remove-capabilities")
    message(STATUS "========================")
endfunction()

# Function to create a startup script for easy development
function(create_startup_script)
    set(STARTUP_SCRIPT_CONTENT "#!/bin/bash
# Alaris Development Startup Script
# Generated automatically by CMake

set -e  # Exit on any error

SCRIPT_DIR=\"\$(cd \"\$(dirname \"\${BASH_SOURCE[0]}\")\"; pwd)\"
BUILD_DIR=\"${CMAKE_BINARY_DIR}\"
BIN_DIR=\"\${BUILD_DIR}/bin\"
CONFIG_DIR=\"${CMAKE_SOURCE_DIR}/config\"
DATA_DIR=\"${ALARIS_DATA_DIR}\"

echo \"Starting Alaris Trading System...\"
echo \"Build: \${BUILD_DIR}\"
echo \"Config: \${CONFIG_DIR}\"
echo \"Data: \${DATA_DIR}\"
echo

# Function to cleanup on exit
cleanup() {
    echo
    echo \"Shutting down processes...\"
    jobs -p | xargs -r kill
    
    # Clean up shared memory
    echo \"Cleaning up shared memory...\"
    rm -f /dev/shm/alaris_* 2>/dev/null || true
    
    echo \"Cleanup completed.\"
}

# Set trap for cleanup
trap cleanup EXIT

# Check if executables exist
if [[ ! -f \"\${BIN_DIR}/quantlib-process\" ]]; then
    echo \"Error: quantlib-process not found. Run 'cmake --build . --target dev' first.\"
    exit 1
fi

# Verify data environment
echo \"Verifying data environment...\"
if [[ ! -d \"\${DATA_DIR}\" ]]; then
    echo \"Error: Data directory not found: \${DATA_DIR}\"
    echo \"Run: cmake --build . --target setup-data\"
    exit 1
fi

# Check for essential data files
DATA_FILES=\$(find \"\${DATA_DIR}/equity/usa/daily\" -name \"*.csv\" 2>/dev/null | wc -l)
if [[ \$DATA_FILES -eq 0 ]]; then
    echo \"Error: No historical data files found in \${DATA_DIR}/equity/usa/daily/\"
    echo \"This will cause Lean's FileSystemDataFeed to fail.\"
    echo \"Run: cmake --build . --target setup-data\"
    exit 1
fi

echo \"✓ Found \$DATA_FILES historical data files\"

# Check lean.json configuration
if [[ ! -f \"\${BUILD_DIR}/lean.json\" ]]; then
    echo \"Error: lean.json not found. Run 'cmake --build . --target setup-data'\"
    exit 1
fi

echo \"✓ lean.json configuration exists\"

# Check capabilities
echo \"Checking real-time capabilities...\"
CAPS=\$(getcap \"\${BIN_DIR}/quantlib-process\" 2>/dev/null || echo \"none\")
if [[ \"\$CAPS\" == \"none\" ]]; then
    echo \"Warning: No capabilities set. For real-time performance, run:\"
    echo \"  cmake --build . --target set-capabilities\"
    echo
else
    echo \"✓ Capabilities: \$CAPS\"
    echo
fi

# Clean any existing shared memory
echo \"Cleaning existing shared memory...\"
rm -f /dev/shm/alaris_* 2>/dev/null || true

# Start QuantLib process
echo \"Starting QuantLib process...\"
\"\${BIN_DIR}/quantlib-process\" \"\${CONFIG_DIR}/quantlib_process.yaml\" &
QUANTLIB_PID=\$!

# Wait for shared memory creation
echo \"Waiting for shared memory initialization...\"
sleep 2

# Check if QuantLib process is still running
if ! kill -0 \$QUANTLIB_PID 2>/dev/null; then
    echo \"Error: QuantLib process failed to start\"
    exit 1
fi

# Verify shared memory exists
if [[ ! -e /dev/shm/alaris_market_data ]]; then
    echo \"Error: Shared memory not created. Check QuantLib process logs.\"
    kill \$QUANTLIB_PID 2>/dev/null || true
    exit 1
fi

echo \"✓ QuantLib process started successfully (PID: \$QUANTLIB_PID)\"
echo \"✓ Shared memory initialized\"

# List shared memory objects
echo \"Shared memory objects:\"
ls -la /dev/shm/alaris_* 2>/dev/null || echo \"  None found\"
echo

# Start Lean process if .NET SDK is available
if command -v dotnet &> /dev/null; then
    LEAN_DLL=\"\${BIN_DIR}/Alaris.Lean.dll\"
    if [[ -f \"\$LEAN_DLL\" ]]; then
        echo \"Starting Lean process...\"
        echo \"Data directory for Lean: \${DATA_DIR}\"
        echo \"Available symbols with data:\"
        find \"\${DATA_DIR}/equity/usa/daily\" -mindepth 1 -maxdepth 1 -type d | while read dir; do
            symbol=\$(basename \"\$dir\")
            file_count=\$(find \"\$dir\" -name \"*.csv\" | wc -l)
            echo \"  \${symbol^^}: \$file_count data files\"
        done
        echo
        
        # Set environment for Lean
        export ALARIS_DATA_DIR=\"\${DATA_DIR}\"
        export ALARIS_CONFIG_FILE=\"\${BUILD_DIR}/lean.json\"
        
        dotnet \"\$LEAN_DLL\" --symbol SPY --mode backtest --start-date 2024-01-01 --end-date 2024-12-31 &
        LEAN_PID=\$!
        echo \"✓ Lean process started (PID: \$LEAN_PID)\"
    else
        echo \"Warning: Lean DLL not found at \$LEAN_DLL\"
        echo \"Build with: cmake --build . --target lean-process\"
    fi
else
    echo \"Warning: .NET SDK not found. Install with: sudo apt install dotnet-sdk-9.0\"
fi

echo
echo \"=== Alaris Trading System Running ===\"
echo \"QuantLib Process PID: \$QUANTLIB_PID\"
if [[ -n \"\${LEAN_PID:-}\" ]]; then
    echo \"Lean Process PID: \$LEAN_PID\"
fi
echo \"Data Directory: \${DATA_DIR}\"
echo \"Historical Data Files: \$DATA_FILES\"
echo \"Press Ctrl+C to stop all processes\"
echo \"=====================================\"

# Wait for any process to exit
wait
")
    
    set(STARTUP_SCRIPT_PATH "${CMAKE_BINARY_DIR}/start-alaris.sh")
    file(WRITE "${STARTUP_SCRIPT_PATH}" "${STARTUP_SCRIPT_CONTENT}")
    
    # Make script executable
    execute_process(
        COMMAND chmod +x "${STARTUP_SCRIPT_PATH}"
        ERROR_QUIET
    )
    
    message(STATUS "Components: Created enhanced startup script: ${STARTUP_SCRIPT_PATH}")
endfunction()

# Call the function to create startup script
if(UNIX)
    create_startup_script()
endif()
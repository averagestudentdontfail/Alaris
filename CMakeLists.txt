cmake_minimum_required(VERSION 3.20)
project(Alaris VERSION 1.0.0 LANGUAGES CXX)

# Include all build system modules
include(.cmake/BuildSystem.cmake)
include(.cmake/External.cmake)
include(.cmake/Config.cmake)
include(.cmake/Data.cmake)
include(.cmake/Components.cmake)
include(.cmake/GitInfo.cmake)
include(.cmake/Deployment.cmake)

# Check git submodules
check_submodules()

# Add build info header generation
create_build_info_file("${CMAKE_BINARY_DIR}/alaris_build_info.txt")

# Configure git hooks if in development
if(EXISTS "${CMAKE_SOURCE_DIR}/.git")
    setup_git_hooks()
endif()

# Print configuration summary
message(STATUS "")
message(STATUS "=== Alaris Build Configuration ===")
message(STATUS "  Version: ${PROJECT_VERSION}")
if(HAS_GIT)
    message(STATUS "  Git: ${GIT_BRANCH} (${GIT_COMMIT_HASH_SHORT})")
endif()
message(STATUS "  Build Type: ${CMAKE_BUILD_TYPE}")
message(STATUS "  Platform: ${ALARIS_PLATFORM}")
message(STATUS "  C++ Standard: ${CMAKE_CXX_STANDARD}")
message(STATUS "  QuantLib: ${QUANTLIB_TARGET}")
message(STATUS "  yaml-cpp: ${YAML_CPP_TARGET}")
if(DOTNET_EXECUTABLE)
    message(STATUS "  .NET SDK: ${CMAKE_DOTNET_VERSION}")
endif()
if(ALARIS_MINIMAL_DATA)
    message(STATUS "  Data Mode: Minimal (5 symbols)")
else()
    message(STATUS "  Data Mode: Full (23 symbols)")
endif()
message(STATUS "===================================")
message(STATUS "")

# Configure all components
configure_all_components()

# Add C# project if .NET SDK is available
if(DOTNET_EXECUTABLE AND EXISTS "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.Lean.csproj")
    add_dotnet_project(lean-process "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.Lean.csproj")
elseif(DOTNET_EXECUTABLE AND EXISTS "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.sln")
    add_dotnet_project(lean-process "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.sln")
endif()

# Print build targets summary
message(STATUS "")
message(STATUS "=== Build Targets ===")
message(STATUS "  Core Targets:")
message(STATUS "    alaris           - Main trading process")
message(STATUS "    quantlib-process - QuantLib pricing process")
message(STATUS "    alaris-config    - Configuration tool")
message(STATUS "    alaris-system    - System information tool")
message(STATUS "    quantlib         - Core QuantLib library")
if(DOTNET_EXECUTABLE)
    message(STATUS "    lean-process     - C# Lean components")
endif()
message(STATUS "")
message(STATUS "  Development Targets:")
message(STATUS "    dev              - Build all development executables")
message(STATUS "    ready            - Build + setup data (ready for development)")
message(STATUS "    ready-env        - Build + data + environment verification")
message(STATUS "    ready-prod       - Production-ready setup with full validation")
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    message(STATUS "    ready-rt         - Complete setup including real-time capabilities")
endif()
message(STATUS "")
message(STATUS "  Data Management:")
message(STATUS "    setup-data       - Set up essential market data and sample files")
message(STATUS "    clean-data       - Clean all data directories")
message(STATUS "    validate-data    - Validate data setup")
message(STATUS "    refresh-data     - Clean and rebuild all data")
message(STATUS "    verify-lean-data - Verify data for Lean FileSystemDataFeed")
message(STATUS "")
message(STATUS "  Environment:")
message(STATUS "    verify-environment - Check both data and capabilities")
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    message(STATUS "    set-capabilities - Set Linux capabilities for real-time performance")
    message(STATUS "    check-capabilities - Check current capabilities")
    message(STATUS "    remove-capabilities - Remove all capabilities")
endif()
message(STATUS "======================")
message(STATUS "")

# Print useful build commands
message(STATUS "=== Quick Start Commands ===")
message(STATUS "  Fresh setup:")
message(STATUS "    mkdir build && cd build")
message(STATUS "    cmake ..")
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE AND NOT ALARIS_AUTO_SET_CAPABILITIES)
    message(STATUS "    cmake --build . --target ready-rt  # Build + data + capabilities")
else()
    message(STATUS "    cmake --build . --target ready     # Build + setup data")
endif()
message(STATUS "    ./start-alaris.sh                   # Start the system")
message(STATUS "")
message(STATUS "  Individual builds:")
message(STATUS "    cmake --build . --target alaris")
message(STATUS "    cmake --build . --target quantlib-process")
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    message(STATUS "    cmake --build . --target set-capabilities")
endif()
message(STATUS "")
message(STATUS "  Development workflow:")
message(STATUS "    cmake --build . --target dev        # Build all executables")
message(STATUS "    cmake --build . --target setup-data # Set up market data")
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    message(STATUS "    cmake --build . --target set-capabilities # Enable real-time")
endif()
message(STATUS "    ./start-alaris.sh                   # Run the system")
message(STATUS "=============================")
message(STATUS "")

# Print real-time performance instructions
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    message(STATUS "=== Real-time Performance Setup ===")
    if(ALARIS_AUTO_SET_CAPABILITIES)
        message(STATUS "  Auto-capabilities: ENABLED")
        message(STATUS "  Capabilities will be set automatically after each build.")
    else()
        message(STATUS "  Auto-capabilities: DISABLED")
        message(STATUS "  To enable real-time performance:")
        message(STATUS "    cmake --build . --target set-capabilities")
        message(STATUS "  Or manually:")
        message(STATUS "    bash ./set-capabilities.sh")
    endif()
    message(STATUS "")
    message(STATUS "  To enable auto-capabilities in future builds:")
    message(STATUS "    cmake -DALARIS_AUTO_SET_CAPABILITIES=ON ..")
    message(STATUS "")
    message(STATUS "  Required capabilities: ${ALARIS_QUANTLIB_CAPABILITIES}")
    message(STATUS "====================================")
    message(STATUS "")
endif()

# Print installation instructions
message(STATUS "=== Installation ===")
message(STATUS "  Install to system:")
message(STATUS "    cmake --install .                    # Install all components")
message(STATUS "    cmake --install . --component Runtime # Runtime only")
message(STATUS "")
message(STATUS "  After installation:")
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    message(STATUS "    sudo ${CMAKE_INSTALL_PREFIX}/${CMAKE_INSTALL_BINDIR}/alaris-production-setup.sh")
endif()
message(STATUS "    ${CMAKE_INSTALL_PREFIX}/${CMAKE_INSTALL_BINDIR}/alaris-post-install.sh")
message(STATUS "====================")
message(STATUS "")

# Print troubleshooting information
message(STATUS "=== Troubleshooting ===")
message(STATUS "  Common issues:")
message(STATUS "")
message(STATUS "  1. 'Operation not permitted' for real-time priority:")
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    message(STATUS "     Solution: cmake --build . --target set-capabilities")
else()
    message(STATUS "     Solution: Install libcap2-bin, then reconfigure")
    message(STATUS "               sudo apt install libcap2-bin")
    message(STATUS "               cmake -DALARIS_SET_CAPABILITIES=ON ..")
endif()
message(STATUS "")
message(STATUS "  2. Shared memory permission denied:")
message(STATUS "     Solution: Don't run with sudo, use same user for both processes")
message(STATUS "")
message(STATUS "  3. Lean FileSystemDataFeed fails / algorithm exits immediately:")
message(STATUS "     Problem: No historical data found in ${ALARIS_DATA_DIR}")
message(STATUS "     Solution: cmake --build . --target setup-data")
message(STATUS "     Verify: cmake --build . --target verify-lean-data")
message(STATUS "")
message(STATUS "  4. QuantLib not found:")
message(STATUS "     Solution: sudo apt install libquantlib0-dev")
message(STATUS "     Or: git submodule add https://github.com/lballabio/QuantLib.git external/QuantLib")
message(STATUS "")
message(STATUS "  5. yaml-cpp not found:")
message(STATUS "     Solution: sudo apt install libyaml-cpp-dev")
message(STATUS "     Or: git submodule add https://github.com/jbeder/yaml-cpp.git external/yaml-cpp")
message(STATUS "")
message(STATUS "  6. Data validation errors:")
message(STATUS "     Solution: cmake --build . --target refresh-data")
message(STATUS "     Check: ls -la ${ALARIS_DATA_DIR}/equity/usa/daily/")
message(STATUS "")
message(STATUS "  Build logs: Check for warnings and errors above")
message(STATUS "  Runtime logs: journalctl -f (if using systemd services)")
message(STATUS "  Environment check: cmake --build . --target verify-environment")
message(STATUS "========================")
message(STATUS "")

# Print final summary
message(STATUS "=== Build Summary ===")
message(STATUS "  Configuration: Complete ✓")
message(STATUS "  Dependencies: Resolved ✓")
if(ALARIS_AUTO_SETUP_DATA)
    message(STATUS "  Data setup: Auto-configured at ${ALARIS_DATA_DIR}")
    if(ALARIS_CREATE_SAMPLE_DATA)
        message(STATUS "  Sample data: Available for backtesting ✓")
    endif()
else()
    message(STATUS "  Data setup: Manual (run 'cmake --build . --target setup-data')")
endif()
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    if(ALARIS_AUTO_SET_CAPABILITIES)
        message(STATUS "  Capabilities: Auto-enabled ✓")
    else()
        message(STATUS "  Capabilities: Available (manual setup required)")
    endif()
endif()
message(STATUS "")
message(STATUS "  Ready to build! Run:")
if(ALARIS_AUTO_SETUP_DATA)
    if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE AND NOT ALARIS_AUTO_SET_CAPABILITIES)
        message(STATUS "    cmake --build . --target ready-rt    # Complete setup")
    else()
        message(STATUS "    cmake --build . --target ready-env   # Build + verify environment")
    endif()
else()
    message(STATUS "    cmake --build . --target ready-env   # Build + setup data + verify")
endif()
message(STATUS "    ./start-alaris.sh                     # Start the system")
message(STATUS "======================")
message(STATUS "")

# Create a helpful build script for new users
set(BUILD_SCRIPT_CONTENT "#!/bin/bash
# Alaris Quick Build Script
# Generated automatically by CMake

set -e  # Exit on any error

echo \"Alaris Trading System - Quick Build\"
echo \"===================================\"

# Check if we're in the build directory
if [[ ! -f \"CMakeCache.txt\" ]]; then
    echo \"Error: This script must be run from the build directory\"
    echo \"Usage: cd build && ./build-alaris.sh\"
    exit 1
fi

# Function to check if a command succeeded
check_status() {
    if [ \$1 -eq 0 ]; then
        echo \"✓ \$2\"
    else
        echo \"✗ \$2 failed\"
        exit 1
    fi
}

echo \"Building Alaris components...\"
cmake --build . --target dev
check_status \$? \"Component build\"

echo \"Setting up market data environment...\"
cmake --build . --target setup-data
check_status \$? \"Data setup\"

echo \"Verifying data for Lean FileSystemDataFeed...\"
cmake --build . --target verify-lean-data
check_status \$? \"Lean data verification\"

$(if [[ "${ALARIS_SET_CAPABILITIES}" == "ON" && "${ALARIS_CAPABILITIES_AVAILABLE}" == "TRUE" && "${ALARIS_AUTO_SET_CAPABILITIES}" != "ON" ]]; then
echo \"echo \"Setting up real-time capabilities...\"
cmake --build . --target set-capabilities
check_status \$? \"Capabilities setup\""
fi)

echo \"Running final environment verification...\"
cmake --build . --target verify-environment
check_status \$? \"Environment verification\"

echo
echo \"Build completed successfully!\"
echo \"============================\"
echo
echo \"Environment Summary:\"
echo \"  Data directory: ${ALARIS_DATA_DIR}\"
echo \"  Historical data available for backtesting\"
echo \"  lean.json configuration ready\"
$(if [[ "${ALARIS_SET_CAPABILITIES}" == "ON" && "${ALARIS_CAPABILITIES_AVAILABLE}" == "TRUE" ]]; then
echo \"echo \"  Real-time capabilities configured\""
fi)
echo
echo \"To start the system:\"
echo \"  ./start-alaris.sh\"
echo
echo \"To check environment:\"
echo \"  cmake --build . --target verify-environment\"
echo
echo \"To verify data specifically:\"
echo \"  cmake --build . --target verify-lean-data\"
echo
")

file(WRITE "${CMAKE_BINARY_DIR}/build-alaris.sh" "${BUILD_SCRIPT_CONTENT}")
execute_process(
    COMMAND chmod +x "${CMAKE_BINARY_DIR}/build-alaris.sh"
    ERROR_QUIET
)

message(STATUS "Created enhanced quick build script: ${CMAKE_BINARY_DIR}/build-alaris.sh")
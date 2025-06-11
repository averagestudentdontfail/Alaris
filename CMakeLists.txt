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

# NEW: Clear data mode messaging
if(ALARIS_DEVELOPMENT_MODE)
    message(STATUS "  Mode: DEVELOPMENT (with synthetic data)")
    if(ALARIS_MINIMAL_DATA)
        list(LENGTH ESSENTIAL_SYMBOLS symbol_count)
        message(STATUS "  Data: Minimal synthetic (${symbol_count} symbols)")
    else()
        list(LENGTH PRODUCTION_SYMBOLS symbol_count)
        message(STATUS "  Data: Full synthetic (${symbol_count} symbols)")
    endif()
    message(STATUS "  ⚠️  WARNING: Development mode uses synthetic data!")
else()
    message(STATUS "  Mode: PRODUCTION (real data sources)")
    if(ALARIS_MINIMAL_DATA)
        list(LENGTH ESSENTIAL_SYMBOLS symbol_count)
        message(STATUS "  Data: Minimal production (${symbol_count} symbols)")
    else()
        list(LENGTH PRODUCTION_SYMBOLS symbol_count)
        message(STATUS "  Data: Full production (${symbol_count} symbols)")
    endif()
    message(STATUS "  ✓ Production-ready configuration")
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
message(STATUS "    ready            - Build + setup data")
message(STATUS "    ready-env        - Build + data + environment verification")
message(STATUS "    ready-prod       - Production-ready setup with full validation")
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    message(STATUS "    ready-rt         - Complete setup including real-time capabilities")
endif()
message(STATUS "")
message(STATUS "  Data Management:")
message(STATUS "    setup-data       - Set up data environment")
message(STATUS "    clean-data       - Clean all data directories")
message(STATUS "    validate-data    - Validate data setup")
message(STATUS "    refresh-data     - Clean and rebuild all data")
message(STATUS "    verify-lean-data - Verify data for Lean FileSystemDataFeed")
message(STATUS "")
message(STATUS "  Mode Management:")
message(STATUS "    enable-dev-mode  - Switch to development mode (synthetic data)")
message(STATUS "    disable-dev-mode - Switch to production mode (real data)")
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

# Print configuration-specific quick start commands
message(STATUS "=== Quick Start Commands ===")
if(ALARIS_DEVELOPMENT_MODE)
    message(STATUS "  Development Mode Setup:")
    message(STATUS "    mkdir build && cd build")
    message(STATUS "    cmake -DALARIS_DEVELOPMENT_MODE=ON ..")
    if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE AND NOT ALARIS_AUTO_SET_CAPABILITIES)
        message(STATUS "    cmake --build . --target ready-rt  # Build + synthetic data + capabilities")
    else()
        message(STATUS "    cmake --build . --target ready     # Build + synthetic data")
    endif()
    message(STATUS "    ./start-alaris.sh                   # Start with synthetic data")
    message(STATUS "")
    message(STATUS "  ⚠️  Development mode uses SYNTHETIC data for testing only!")
else()
    message(STATUS "  Production Mode Setup:")
    message(STATUS "    mkdir build && cd build")
    message(STATUS "    cmake ..                            # Production defaults")
    message(STATUS "    cmake --build . --target ready-env # Build + verify environment")
    message(STATUS "    # Configure real data sources (IB, etc.)")
    message(STATUS "    ./start-alaris.sh                   # Start with real data")
    message(STATUS "")
    message(STATUS "  To switch to development mode for testing:")
    message(STATUS "    cmake -DALARIS_DEVELOPMENT_MODE=ON ..")
    message(STATUS "    cmake --build . --target setup-data")
endif()

message(STATUS "")
message(STATUS "  Individual builds:")
message(STATUS "    cmake --build . --target alaris")
message(STATUS "    cmake --build . --target quantlib-process")
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    message(STATUS "    cmake --build . --target set-capabilities")
endif()

message(STATUS "=============================")
message(STATUS "")

# Print data configuration warnings and instructions
if(ALARIS_DEVELOPMENT_MODE OR ALARIS_CREATE_SAMPLE_DATA)
    message(STATUS "=== DEVELOPMENT MODE ACTIVE ===")
    message(STATUS "  ⚠️  WARNING: Using synthetic data for development!")
    message(STATUS "  ⚠️  Do NOT use for production trading!")
    message(STATUS "")
    message(STATUS "  This mode creates fake historical data for:")
    message(STATUS "  • Algorithm development and testing")
    message(STATUS "  • Backtesting strategies safely")
    message(STATUS "  • System integration testing")
    message(STATUS "")
    message(STATUS "  To switch to production mode:")
    message(STATUS "    cmake --build . --target disable-dev-mode")
    message(STATUS "    cmake ..")
    message(STATUS "================================")
    message(STATUS "")
else()
    message(STATUS "=== PRODUCTION MODE ACTIVE ===")
    message(STATUS "  ✓ Production-ready configuration")
    message(STATUS "  ✓ No synthetic data generation")
    message(STATUS "  ✓ Real data source configuration required")
    message(STATUS "")
    message(STATUS "  Next steps for production:")
    message(STATUS "  1. Configure your data provider (Interactive Brokers, etc.)")
    message(STATUS "  2. Set up real market data feeds")
    message(STATUS "  3. Configure live trading credentials")
    message(STATUS "  4. Test with paper trading first")
    message(STATUS "")
    message(STATUS "  For development/testing, enable development mode:")
    message(STATUS "    cmake --build . --target enable-dev-mode")
    message(STATUS "    cmake ..")
    message(STATUS "===============================")
    message(STATUS "")
endif()

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

if(ALARIS_DEVELOPMENT_MODE OR ALARIS_CREATE_SAMPLE_DATA)
    message(STATUS "  3. Development Mode - Synthetic Data Issues:")
    message(STATUS "     Problem: Synthetic data generation failed")
    message(STATUS "     Solution: cmake --build . --target refresh-data")
    message(STATUS "     Verify: cmake --build . --target verify-lean-data")
else()
    message(STATUS "  3. Production Mode - Real Data Issues:")
    message(STATUS "     Problem: No real market data available")
    message(STATUS "     Solution: Configure data provider (IB Gateway, etc.)")
    message(STATUS "     Or test with: cmake --build . --target enable-dev-mode")
endif()

message(STATUS "")
message(STATUS "  4. QuantLib not found:")
message(STATUS "     Solution: sudo apt install libquantlib0-dev")
message(STATUS "     Or: git submodule add https://github.com/lballabio/QuantLib.git external/QuantLib")
message(STATUS "")
message(STATUS "  5. yaml-cpp not found:")
message(STATUS "     Solution: sudo apt install libyaml-cpp-dev")
message(STATUS "     Or: git submodule add https://github.com/jbeder/yaml-cpp.git external/yaml-cpp")
message(STATUS "")

if(ALARIS_DEVELOPMENT_MODE OR ALARIS_CREATE_SAMPLE_DATA)
    message(STATUS "  6. Lean algorithm exits immediately (Development Mode):")
    message(STATUS "     Problem: Synthetic data generation failed")
    message(STATUS "     Solution: cmake --build . --target refresh-data")
    message(STATUS "     Check: ls -la ${ALARIS_DATA_DIR}/equity/usa/daily/")
else()
    message(STATUS "  6. Lean algorithm exits immediately (Production Mode):")
    message(STATUS "     Problem: Real data provider not configured")
    message(STATUS "     Solution: Configure IB Gateway or other data source")
    message(STATUS "     Test: cmake --build . --target enable-dev-mode")
endif()

message(STATUS "")
message(STATUS "  Build logs: Check for warnings and errors above")
message(STATUS "  Runtime logs: journalctl -f (if using systemd services)")
message(STATUS "  Environment check: cmake --build . --target verify-environment")
message(STATUS "========================")
message(STATUS "")

# Print final summary with mode-specific messaging
message(STATUS "=== Build Summary ===")
message(STATUS "  Configuration: Complete ✓")
message(STATUS "  Dependencies: Resolved ✓")

if(ALARIS_DEVELOPMENT_MODE)
    message(STATUS "  Mode: DEVELOPMENT with synthetic data")
    message(STATUS "  ⚠️  Using synthetic data for development/testing only!")
    if(ALARIS_AUTO_SETUP_DATA)
        message(STATUS "  Data setup: Auto-configured synthetic data at ${ALARIS_DATA_DIR}")
    else()
        message(STATUS "  Data setup: Manual (run 'cmake --build . --target setup-data')")
    endif()
else()
    message(STATUS "  Mode: PRODUCTION ready for real data")
    message(STATUS "  ✓ No synthetic data - production configuration")
    if(ALARIS_AUTO_SETUP_DATA)
        message(STATUS "  Data setup: Auto-configured production environment")
    else()
        message(STATUS "  Data setup: Manual production setup required")
    endif()
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

if(ALARIS_DEVELOPMENT_MODE)
    if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE AND NOT ALARIS_AUTO_SET_CAPABILITIES)
        message(STATUS "    cmake --build . --target ready-rt    # Complete development setup")
    else()
        message(STATUS "    cmake --build . --target ready-env   # Build + synthetic data + verify")
    endif()
    message(STATUS "    ./start-alaris.sh                     # Start with synthetic data")
    message(STATUS "")
    message(STATUS "  Remember: Development mode uses synthetic data for testing only!")
else()
    message(STATUS "    cmake --build . --target ready-env   # Build + production setup + verify")
    message(STATUS "    # Configure real data sources")
    message(STATUS "    ./start-alaris.sh                     # Start with real data")
    message(STATUS "")
    message(STATUS "  For testing: cmake --build . --target enable-dev-mode")
endif()

message(STATUS "======================")
message(STATUS "")

# Create a mode-aware build script for new users
if(ALARIS_DEVELOPMENT_MODE)
    set(MODE_DESCRIPTION "Development Mode (Synthetic Data)")
    set(MODE_WARNING "echo \"⚠️  WARNING: Using synthetic data for development/testing only!\"
echo \"⚠️  Do NOT use this configuration for real trading!\"
echo")
    set(MODE_SETUP "echo \"Setting up synthetic data for development...\"
cmake --build . --target setup-data
check_status \$? \"Synthetic data setup\"")
else()
    set(MODE_DESCRIPTION "Production Mode (Real Data)")
    set(MODE_WARNING "echo \"✓ Production configuration - ready for real data sources\"
echo")
    set(MODE_SETUP "echo \"Verifying production environment...\"
cmake --build . --target verify-environment
check_status \$? \"Production environment verification\"")
endif()

# Build the script content
set(BUILD_SCRIPT_CONTENT "#!/bin/bash
# Alaris Quick Build Script - ${MODE_DESCRIPTION}
# Generated automatically by CMake

set -e  # Exit on any error

echo \"Alaris Trading System - Quick Build\"
echo \"Mode: ${MODE_DESCRIPTION}\"
echo \"===================================\"

${MODE_WARNING}

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

${MODE_SETUP}

echo \"Verifying data for Lean FileSystemDataFeed...\"
cmake --build . --target verify-lean-data
check_status \$? \"Lean data verification\"
")

# Add capabilities setup if available and not auto-enabled
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE AND NOT ALARIS_AUTO_SET_CAPABILITIES)
    string(APPEND BUILD_SCRIPT_CONTENT "
echo \"Setting up real-time capabilities...\"
cmake --build . --target set-capabilities
check_status \$? \"Capabilities setup\"
")
endif()

# Continue building the script content
string(APPEND BUILD_SCRIPT_CONTENT "
echo \"Running final environment verification...\"
cmake --build . --target verify-environment
check_status \$? \"Environment verification\"

echo
echo \"Build completed successfully!\"
echo \"============================\"
echo
echo \"Environment Summary:\"
echo \"  Mode: ${MODE_DESCRIPTION}\"
echo \"  Data directory: ${ALARIS_DATA_DIR}\"
")

if(ALARIS_DEVELOPMENT_MODE OR ALARIS_CREATE_SAMPLE_DATA)
    string(APPEND BUILD_SCRIPT_CONTENT "echo \"  Synthetic data available for development/testing\"
echo \"  ⚠️  WARNING: Do not use for real trading!\"
")
else()
    string(APPEND BUILD_SCRIPT_CONTENT "echo \"  Production configuration ready\"
echo \"  Real data sources required for trading\"
")
endif()

string(APPEND BUILD_SCRIPT_CONTENT "echo \"  lean.json configuration ready\"
")

# Add capabilities info if available
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    string(APPEND BUILD_SCRIPT_CONTENT "echo \"  Real-time capabilities configured\"
")
endif()

# Finish the script content
string(APPEND BUILD_SCRIPT_CONTENT "echo
echo \"To start the system:\"
echo \"  ./start-alaris.sh\"
echo
")

if(ALARIS_DEVELOPMENT_MODE)
    string(APPEND BUILD_SCRIPT_CONTENT "echo \"To switch to production mode:\"
echo \"  cmake --build . --target disable-dev-mode\"
echo \"  cmake ..\"
echo
")
else()
    string(APPEND BUILD_SCRIPT_CONTENT "echo \"To enable development mode for testing:\"
echo \"  cmake --build . --target enable-dev-mode\"
echo \"  cmake ..\"
echo
")
endif()

string(APPEND BUILD_SCRIPT_CONTENT "echo \"To check environment:\"
echo \"  cmake --build . --target verify-environment\"
echo
")

# Write the build script
file(WRITE "${CMAKE_BINARY_DIR}/build-alaris.sh" "${BUILD_SCRIPT_CONTENT}")
execute_process(
    COMMAND chmod +x "${CMAKE_BINARY_DIR}/build-alaris.sh"
    ERROR_QUIET
)

message(STATUS "Created mode-aware build script: ${CMAKE_BINARY_DIR}/build-alaris.sh")
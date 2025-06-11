# Alaris CMake Configuration
include(CMakeDependentOption)

# Platform-specific configuration
if(WIN32)
    set(ALARIS_PLATFORM "Windows")
    set(ALARIS_SHARED_LIB_EXT ".dll")
    set(ALARIS_STATIC_LIB_EXT ".lib")
    set(ALARIS_EXECUTABLE_EXT ".exe")
elseif(APPLE)
    set(ALARIS_PLATFORM "Darwin")
    set(ALARIS_SHARED_LIB_EXT ".dylib")
    set(ALARIS_STATIC_LIB_EXT ".a")
    set(ALARIS_EXECUTABLE_EXT "")
elseif(UNIX)
    set(ALARIS_PLATFORM "Linux")
    set(ALARIS_SHARED_LIB_EXT ".so")
    set(ALARIS_STATIC_LIB_EXT ".a")
    set(ALARIS_EXECUTABLE_EXT "")
else()
    set(ALARIS_PLATFORM "Unknown")
endif()

# Dependency configuration - only check if variables are set, not if targets exist yet
# (targets will be created later during build process)

# Configure QuantLib (expected to be provided by External.cmake)
if(DEFINED QUANTLIB_TARGET AND QUANTLIB_TARGET)
    set(ALARIS_QUANTLIB_LIBRARIES ${QUANTLIB_TARGET}) # Use the target name for linking
    message(STATUS "Configured QuantLib target: ${QUANTLIB_TARGET}")
else()
    message(FATAL_ERROR "QuantLib was NOT configured. "
                        "Expected QUANTLIB_TARGET to be set by External.cmake processing. "
                        "Either install libquantlib0-dev or initialize git submodules.")
endif()

# Configure yaml-cpp (expected to be provided by External.cmake)
if(DEFINED YAML_CPP_TARGET AND YAML_CPP_TARGET)
    set(ALARIS_YAMLCPP_LIBRARIES ${YAML_CPP_TARGET}) # Use the target name for linking
    message(STATUS "Configured yaml-cpp target: ${YAML_CPP_TARGET}")
else()
    message(FATAL_ERROR "yaml-cpp was NOT configured. "
                        "Expected YAML_CPP_TARGET to be set by External.cmake processing. "
                        "Either install libyaml-cpp-dev or initialize git submodules.")
endif()

# Configure Boost (from find_package)
find_package(Boost QUIET)
if(Boost_FOUND)
    set(ALARIS_BOOST_INCLUDE_DIRS ${Boost_INCLUDE_DIRS})
    set(ALARIS_BOOST_LIBRARIES ${Boost_LIBRARIES})
    set(ALARIS_BOOST_DEFINITIONS ${Boost_DEFINITIONS})
    message(STATUS "Configured Boost from find_package: ${Boost_VERSION}")
endif()

# PRODUCTION-FIRST BUILD CONFIGURATION
# Alaris defaults to production-ready configuration for real trading

# Core build options
set(ALARIS_BUILD_OPTIONS
    BUILD_DOCS
    ENABLE_SANITIZERS
    ENABLE_COVERAGE
    ALARIS_INSTALL_DEVELOPMENT
    ALARIS_SET_CAPABILITIES
    ALARIS_AUTO_SET_CAPABILITIES
    ALARIS_DEVELOPMENT_MODE
)

# Set default values for build options
option(BUILD_DOCS "Build documentation" OFF)
option(ENABLE_SANITIZERS "Enable sanitizers (for Debug builds)" OFF)
option(ENABLE_COVERAGE "Enable code coverage (for Debug builds)" OFF)
option(ALARIS_INSTALL_DEVELOPMENT "Install development files (headers, etc.)" ON)

# NEW: Development mode option (defaults to OFF for production-first approach)
option(ALARIS_DEVELOPMENT_MODE "Enable development mode with synthetic data and testing defaults" OFF)

# Real-time capabilities configuration (Linux only)
if(UNIX AND NOT APPLE)
    option(ALARIS_SET_CAPABILITIES "Set Linux capabilities for real-time performance" ON)
    
    # Development mode influences auto-capabilities default
    if(ALARIS_DEVELOPMENT_MODE)
        option(ALARIS_AUTO_SET_CAPABILITIES "Automatically set capabilities after build (requires sudo)" ON)
    else()
        option(ALARIS_AUTO_SET_CAPABILITIES "Automatically set capabilities after build (requires sudo)" OFF)
    endif()
    
    # Find required tools
    find_program(SETCAP_EXECUTABLE setcap)
    find_program(GETCAP_EXECUTABLE getcap)
    
    if(SETCAP_EXECUTABLE AND GETCAP_EXECUTABLE)
        set(ALARIS_CAPABILITIES_AVAILABLE TRUE)
        message(STATUS "Linux capabilities tools found: setcap, getcap")
    else()
        set(ALARIS_CAPABILITIES_AVAILABLE FALSE)
        if(ALARIS_SET_CAPABILITIES)
            message(WARNING "setcap/getcap tools not found. Install with: sudo apt install libcap2-bin")
        endif()
    endif()
    
    # Define required capabilities for different executables
    set(ALARIS_REALTIME_CAPABILITIES "cap_sys_nice+ep")  # For real-time scheduling
    set(ALARIS_NETWORK_CAPABILITIES "cap_net_raw+ep")    # For raw network access (if needed)
    set(ALARIS_MEMORY_CAPABILITIES "cap_ipc_lock+ep")    # For memory locking
    
    # Combined capabilities for QuantLib process
    set(ALARIS_QUANTLIB_CAPABILITIES "cap_sys_nice,cap_ipc_lock+ep")
    
else()
    set(ALARIS_SET_CAPABILITIES OFF)
    set(ALARIS_AUTO_SET_CAPABILITIES OFF)
    set(ALARIS_CAPABILITIES_AVAILABLE FALSE)
endif()

# Compiler configuration - Development vs Production optimizations
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    # Common flags as a single string
    set(ALARIS_COMMON_FLAGS_STR 
        "-Wall -Wextra -Wpedantic -Werror=return-type -Werror=non-virtual-dtor -Werror=address -Werror=sequence-point -Werror=format-security -Werror=missing-braces -Werror=reorder -Werror=switch -Werror=uninitialized -Wno-unused-parameter -Wno-unused-variable -Wno-unused-function")

    # Development mode flags (more forgiving for rapid iteration)
    if(ALARIS_DEVELOPMENT_MODE)
        set(ALARIS_DEBUG_FLAGS_STR "-g -O0 -fno-omit-frame-pointer -fno-inline -fno-inline-functions")
        set(ALARIS_RELEASE_FLAGS_STR "-O2 -DNDEBUG")  # Less aggressive optimization for faster builds
    else()
        # Production mode flags (maximum optimization)
        set(ALARIS_DEBUG_FLAGS_STR "-g -O0 -fno-omit-frame-pointer -fno-inline -fno-inline-functions")
        set(ALARIS_RELEASE_FLAGS_STR "-O3 -DNDEBUG -flto -fno-fat-lto-objects -march=native")  # Maximum optimization
    endif()

    # Apply flags based on build type
    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${ALARIS_COMMON_FLAGS_STR} ${ALARIS_DEBUG_FLAGS_STR}")
    else() # Release, RelWithDebInfo etc.
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${ALARIS_COMMON_FLAGS_STR} ${ALARIS_RELEASE_FLAGS_STR}")
    endif()
endif()

# Sanitizer configuration (more permissive in development mode)
if(ENABLE_SANITIZERS AND CMAKE_BUILD_TYPE STREQUAL "Debug")
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        if(ALARIS_DEVELOPMENT_MODE)
            # Development mode: use basic sanitizers for faster iteration
            set(ALARIS_SANITIZER_FLAGS_STR "-fsanitize=address -fno-omit-frame-pointer")
        else()
            # Production mode: comprehensive sanitizers for thorough testing
            set(ALARIS_SANITIZER_FLAGS_STR "-fsanitize=address -fsanitize=undefined -fno-omit-frame-pointer")
        endif()
        
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${ALARIS_SANITIZER_FLAGS_STR}")
        set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} ${ALARIS_SANITIZER_FLAGS_STR}")
        set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} ${ALARIS_SANITIZER_FLAGS_STR}")
    endif()
endif()

# Coverage configuration
if(ENABLE_COVERAGE AND CMAKE_BUILD_TYPE STREQUAL "Debug")
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        # For GCC/gcov:
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fprofile-arcs -ftest-coverage")
        set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} -fprofile-arcs -ftest-coverage")
        set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} -fprofile-arcs -ftest-coverage")
    endif()
endif()

# Function to create capability setting script
function(create_capability_script)
    if(NOT ALARIS_CAPABILITIES_AVAILABLE)
        return()
    endif()
    
    set(SCRIPT_CONTENT "#!/bin/bash
# Alaris Capabilities Setup Script
# Generated automatically by CMake

set -e  # Exit on any error

SCRIPT_DIR=\"\$(cd \"\$(dirname \"\${BASH_SOURCE[0]}\")\"; pwd)\"
BUILD_DIR=\"${CMAKE_BINARY_DIR}\"
BIN_DIR=\"\${BUILD_DIR}/bin\"

echo \"Setting up Linux capabilities for Alaris...\"

# Check if running as root
if [[ \$EUID -eq 0 ]]; then
    echo \"Error: Do not run this script as root. Use sudo when prompted.\"
    exit 1
fi

# Function to set capabilities for an executable
set_caps() {
    local exe=\"\$1\"
    local caps=\"\$2\"
    local description=\"\$3\"
    
    if [[ ! -f \"\$exe\" ]]; then
        echo \"Warning: \$exe not found, skipping...\"
        return 0
    fi
    
    echo \"Setting capabilities for \$description...\"
    echo \"  Executable: \$exe\"
    echo \"  Capabilities: \$caps\"
    
    # Remove existing capabilities first
    sudo setcap -r \"\$exe\" 2>/dev/null || true
    
    # Set new capabilities
    if sudo setcap \"\$caps\" \"\$exe\"; then
        echo \"  ✓ Success\"
        
        # Verify capabilities were set
        local current_caps=\$(getcap \"\$exe\" 2>/dev/null || echo \"none\")
        echo \"  Verified: \$current_caps\"
    else
        echo \"  ✗ Failed to set capabilities\"
        return 1
    fi
    
    echo
}

# Set capabilities for QuantLib process (real-time scheduling + memory locking)
set_caps \"\${BIN_DIR}/quantlib-process\" \"${ALARIS_QUANTLIB_CAPABILITIES}\" \"QuantLib Process\"

# Set capabilities for main alaris executable if it exists
set_caps \"\${BIN_DIR}/alaris\" \"${ALARIS_QUANTLIB_CAPABILITIES}\" \"Main Alaris Process\"

echo \"Capabilities setup completed successfully!\"
echo
echo \"You can now run the processes as a regular user:\"
echo \"  \${BIN_DIR}/quantlib-process config/quantlib_process.yaml\"
echo \"  dotnet \${BIN_DIR}/Alaris.Lean.dll\"
echo
echo \"To verify capabilities: getcap \${BIN_DIR}/quantlib-process\"
echo \"To remove capabilities: sudo setcap -r \${BIN_DIR}/quantlib-process\"
")
    
    set(SCRIPT_PATH "${CMAKE_BINARY_DIR}/set-capabilities.sh")
    file(WRITE "${SCRIPT_PATH}" "${SCRIPT_CONTENT}")
    
    # Make script executable
    execute_process(
        COMMAND chmod +x "${SCRIPT_PATH}"
        ERROR_QUIET
    )
    
    message(STATUS "Created capability setup script: ${SCRIPT_PATH}")
    
    # Set script path in parent scope for use by other functions
    set(ALARIS_CAPABILITY_SCRIPT "${SCRIPT_PATH}" PARENT_SCOPE)
endfunction()

# Function to check current capabilities
function(check_executable_capabilities EXECUTABLE_PATH)
    if(NOT ALARIS_CAPABILITIES_AVAILABLE)
        return()
    endif()
    
    if(NOT EXISTS "${EXECUTABLE_PATH}")
        return()
    endif()
    
    execute_process(
        COMMAND ${GETCAP_EXECUTABLE} "${EXECUTABLE_PATH}"
        OUTPUT_VARIABLE CURRENT_CAPS
        OUTPUT_STRIP_TRAILING_WHITESPACE
        ERROR_QUIET
    )
    
    if(CURRENT_CAPS)
        message(STATUS "Current capabilities for ${EXECUTABLE_PATH}: ${CURRENT_CAPS}")
        set(HAS_CAPABILITIES TRUE PARENT_SCOPE)
    else()
        message(STATUS "No capabilities set for ${EXECUTABLE_PATH}")
        set(HAS_CAPABILITIES FALSE PARENT_SCOPE)
    endif()
endfunction()

# Export configuration
set(ALARIS_CONFIGURED TRUE CACHE INTERNAL "Alaris configuration complete" FORCE)

# Create capability script if needed
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    create_capability_script()
endif()

message(STATUS "Config.cmake: Configuration applied successfully")

# Print configuration summary with mode-specific information
message(STATUS "")
message(STATUS "=== Configuration Summary ===")
if(ALARIS_DEVELOPMENT_MODE)
    message(STATUS "  Mode: DEVELOPMENT")
    message(STATUS "  Purpose: Algorithm development and testing")
    message(STATUS "  Data: Synthetic data for safe testing")
    message(STATUS "  Optimizations: Development-friendly (faster builds)")
    message(STATUS "  ⚠️  Not for production trading!")
else()
    message(STATUS "  Mode: PRODUCTION")
    message(STATUS "  Purpose: Live trading system")
    message(STATUS "  Data: Real market data sources required")
    message(STATUS "  Optimizations: Maximum performance")
    message(STATUS "  ✓ Production-ready configuration")
endif()
message(STATUS "===============================")

# Print capabilities configuration summary
if(UNIX AND NOT APPLE)
    message(STATUS "")
    message(STATUS "=== Real-time Capabilities Configuration ===")
    if(ALARIS_CAPABILITIES_AVAILABLE)
        message(STATUS "  Status: Available")
        message(STATUS "  setcap: ${SETCAP_EXECUTABLE}")
        message(STATUS "  getcap: ${GETCAP_EXECUTABLE}")
        if(ALARIS_SET_CAPABILITIES)
            message(STATUS "  Setup: Enabled")
            message(STATUS "  Auto-set: ${ALARIS_AUTO_SET_CAPABILITIES}")
            message(STATUS "  Required capabilities: ${ALARIS_QUANTLIB_CAPABILITIES}")
        else()
            message(STATUS "  Setup: Disabled (enable with -DALARIS_SET_CAPABILITIES=ON)")
        endif()
    else()
        message(STATUS "  Status: Tools not found")
        message(STATUS "  Install: sudo apt install libcap2-bin")
    endif()
    message(STATUS "============================================")
endif()

# Print development mode configuration help
if(ALARIS_DEVELOPMENT_MODE)
    message(STATUS "")
    message(STATUS "=== Development Mode Active ===")
    message(STATUS "  Benefits:")
    message(STATUS "  • Synthetic data for safe testing")
    message(STATUS "  • Faster builds for rapid iteration")
    message(STATUS "  • Development-friendly defaults")
    message(STATUS "")
    message(STATUS "  To switch to production mode:")
    message(STATUS "    cmake -DALARIS_DEVELOPMENT_MODE=OFF ..")
    message(STATUS "  Or use target:")
    message(STATUS "    cmake --build . --target disable-dev-mode")
    message(STATUS "===============================")
else()
    message(STATUS "")
    message(STATUS "=== Production Mode Active ===")
    message(STATUS "  Features:")
    message(STATUS "  • Maximum performance optimizations")
    message(STATUS "  • Real data source configuration")
    message(STATUS "  • Production-ready defaults")
    message(STATUS "")
    message(STATUS "  To enable development mode for testing:")
    message(STATUS "    cmake -DALARIS_DEVELOPMENT_MODE=ON ..")
    message(STATUS "  Or use target:")
    message(STATUS "    cmake --build . --target enable-dev-mode")
    message(STATUS "===============================")
endif()
# .cmake/Config.cmake
# Alaris CMake Configuration Module
include(CMakeDependentOption)

# Platform-specific configuration
if(WIN32)
    set(ALARIS_PLATFORM "Windows")
elseif(APPLE)
    set(ALARIS_PLATFORM "Darwin")
elseif(UNIX)
    set(ALARIS_PLATFORM "Linux")
else()
    set(ALARIS_PLATFORM "Unknown")
endif()

# Set build options
option(BUILD_DOCS "Build documentation" OFF)
option(ENABLE_SANITIZERS "Enable sanitizers (for Debug builds)" OFF)
option(ENABLE_COVERAGE "Enable code coverage (for Debug builds)" OFF)

# --- Real-time capabilities configuration (Linux only) ---
if(UNIX AND NOT APPLE)
    option(ALARIS_SET_CAPABILITIES "Set Linux capabilities for real-time performance" ON)
    option(ALARIS_AUTO_SET_CAPABILITIES "Automatically set capabilities after build (requires sudo)" OFF)
    
    find_program(SETCAP_EXECUTABLE setcap)
    find_program(GETCAP_EXECUTABLE getcap)
    
    if(SETCAP_EXECUTABLE AND GETCAP_EXECUTABLE)
        set(ALARIS_CAPABILITIES_AVAILABLE TRUE)
        message(STATUS "Config: Linux capabilities tools found: setcap, getcap")
    else()
        set(ALARIS_CAPABILITIES_AVAILABLE FALSE)
        if(ALARIS_SET_CAPABILITIES)
            message(WARNING "setcap/getcap tools not found. Install with: sudo apt install libcap2-bin")
        endif()
    endif()
    
    # Define required capabilities for the executables
    set(ALARIS_QUANTLIB_CAPABILITIES "cap_sys_nice,cap_ipc_lock+ep")
else()
    set(ALARIS_SET_CAPABILITIES OFF)
    set(ALARIS_AUTO_SET_CAPABILITIES OFF)
    set(ALARIS_CAPABILITIES_AVAILABLE FALSE)
endif()

# --- Compiler configuration for maximum performance ---
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    set(ALARIS_COMMON_FLAGS_STR "-Wall -Wextra -Wpedantic -Werror=return-type -Wno-unused-parameter")
    set(ALARIS_RELEASE_FLAGS_STR "-O3 -DNDEBUG -flto")
    set(ALARIS_DEBUG_FLAGS_STR "-g -O0")

    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${ALARIS_COMMON_FLAGS_STR} ${ALARIS_DEBUG_FLAGS_STR}")
    else() # Release, RelWithDebInfo, etc.
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${ALARIS_COMMON_FLAGS_STR} ${ALARIS_RELEASE_FLAGS_STR}")
    endif()
endif()

# Function to create capability setting script (to be run post-build if needed)
function(create_capability_script)
    if(NOT ALARIS_CAPABILITIES_AVAILABLE)
        return()
    endif()
    
    set(SCRIPT_CONTENT "#!/bin/bash
# Alaris Capabilities Setup Script
set -e
BUILD_DIR=\"${CMAKE_BINARY_DIR}\"
BIN_DIR=\"\${BUILD_DIR}/bin\"
echo 'Setting up Linux capabilities for Alaris...'
if [[ \$EUID -eq 0 ]]; then
    echo 'Error: Do not run this script as root. Use sudo when prompted.'
    exit 1
fi
set_caps() {
    local exe=\\\"\$1\\\"
    local caps=\\\"\$2\\\"
    if [[ ! -f \\\"\$exe\\\" ]]; then return 0; fi
    echo \\\"Setting capabilities for \$exe...\\\"
    sudo setcap -r \\\"\$exe\\\" 2>/dev/null || true
    if sudo setcap \\\"\$caps\\\" \\\"\$exe\\\"; then
        echo \\\"  ✓ Success: \$(getcap \\\"\$exe\\\")\\\"
    else
        echo \\\"  ✗ Failed to set capabilities for \$exe\\\"
        exit 1
    fi
}
set_caps \\\"\${BIN_DIR}/quantlib-process\\\" \\\"${ALARIS_QUANTLIB_CAPABILITIES}\\\"
set_caps \\\"\${BIN_DIR}/alaris\\\" \\\"${ALARIS_QUANTLIB_CAPABILITIES}\\\"
echo 'Capabilities setup completed successfully!'
")
    
    set(SCRIPT_PATH "${CMAKE_BINARY_DIR}/set-capabilities.sh")
    file(WRITE "${SCRIPT_PATH}" "${SCRIPT_CONTENT}")
    execute_process(COMMAND chmod +x "${SCRIPT_PATH}" ERROR_QUIET)
    
    # Make the script path available to other CMake modules
    set(ALARIS_CAPABILITY_SCRIPT "${SCRIPT_PATH}" PARENT_SCOPE)
endfunction()

# Always create the capability script if the tools are available
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    create_capability_script()
endif()

message(STATUS "Config.cmake: Production configuration applied.")

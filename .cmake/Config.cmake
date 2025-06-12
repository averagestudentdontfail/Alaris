# .cmake/Config.cmake

# Platform-specific configuration
if(UNIX AND NOT APPLE)
    option(ALARIS_SET_CAPABILITIES "Set Linux capabilities automatically" ON)
    
    find_program(SETCAP_EXECUTABLE setcap)
    find_program(GETCAP_EXECUTABLE getcap)
    find_program(SUDO_EXECUTABLE sudo)
    
    if(SETCAP_EXECUTABLE AND GETCAP_EXECUTABLE)
        set(ALARIS_CAPABILITIES_AVAILABLE TRUE)
        message(STATUS "Linux capabilities tools found: ${SETCAP_EXECUTABLE}")
    else()
        set(ALARIS_CAPABILITIES_AVAILABLE FALSE)
        message(STATUS "Linux capabilities tools not found - capability setting disabled")
    endif()
    
    set(ALARIS_QUANTLIB_CAPABILITIES "cap_sys_nice,cap_ipc_lock+ep")
else()
    set(ALARIS_SET_CAPABILITIES OFF)
    set(ALARIS_CAPABILITIES_AVAILABLE FALSE)
endif()

# Compiler optimization
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wall -g -O0")
    else()
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wall -O3 -DNDEBUG -flto")
    endif()
endif()

# Global variables for script paths
set(ALARIS_CAPABILITY_SCRIPT "" CACHE INTERNAL "")
set(ALARIS_AUTO_SETUP_SCRIPT "" CACHE INTERNAL "")
set(ALARIS_STARTUP_SCRIPT "" CACHE INTERNAL "")

# Create capability script
function(create_capability_script)
    if(NOT ALARIS_CAPABILITIES_AVAILABLE)
        return()
    endif()
    
    set(SCRIPT_CONTENT "#!/bin/bash
set -e
BUILD_DIR=\"${CMAKE_BINARY_DIR}\"
CAPS=\"${ALARIS_QUANTLIB_CAPABILITIES}\"

echo \"Setting Linux capabilities for Alaris executables...\"

set_caps() {
    local exe=\"\$1\"
    if [[ -f \"\$exe\" ]]; then
        echo \"Setting capabilities for: \$exe\"
        sudo setcap -r \"\$exe\" 2>/dev/null || true
        if sudo setcap \"\$CAPS\" \"\$exe\"; then
            echo \"✓ Capabilities set successfully for \$(basename \"\$exe\")\"
        else
            echo \"✗ Failed to set capabilities for \$(basename \"\$exe\")\"
            return 1
        fi
    else
        echo \"✗ Executable not found: \$exe\"
        return 1
    fi
}

set_caps \"\${BUILD_DIR}/bin/quantlib-process\"
set_caps \"\${BUILD_DIR}/bin/alaris\"

echo \"Capability setting complete.\"
")
    
    set(SCRIPT_PATH "${CMAKE_BINARY_DIR}/set-capabilities.sh")
    file(WRITE "${SCRIPT_PATH}" "${SCRIPT_CONTENT}")
    execute_process(COMMAND chmod +x "${SCRIPT_PATH}" ERROR_QUIET)
    set(ALARIS_CAPABILITY_SCRIPT "${SCRIPT_PATH}" CACHE INTERNAL "")
    message(STATUS "Created capability script: ${SCRIPT_PATH}")
endfunction()

# Create automated setup with better error handling
function(create_automated_setup)
    set(AUTO_SETUP_CONTENT "#!/bin/bash
set -e
BUILD_DIR=\"${CMAKE_BINARY_DIR}\"

echo \"Running Alaris automated setup...\"

# Create logs directory
echo \"Creating logs directory...\"
mkdir -p \"\$BUILD_DIR/logs\"

# Update logging configuration
if [[ -f \"\$BUILD_DIR/../config/quantlib_process.yaml\" ]]; then
    if grep -q \"/var/log/alaris\" \"\$BUILD_DIR/../config/quantlib_process.yaml\"; then
        echo \"Updating logging paths in configuration...\"
        cp \"\$BUILD_DIR/../config/quantlib_process.yaml\" \"\$BUILD_DIR/../config/quantlib_process.yaml.backup\"
        sed -i 's|/var/log/alaris/|logs/|g' \"\$BUILD_DIR/../config/quantlib_process.yaml\"
        echo \"✓ Configuration updated\"
    fi
fi

# Check if we can use sudo
CAN_SUDO=false
if command -v sudo >/dev/null 2>&1; then
    if sudo -n true 2>/dev/null || [[ \$EUID -eq 0 ]]; then
        CAN_SUDO=true
    fi
fi

# Set capabilities if possible
if [[ \"\$CAN_SUDO\" == \"true\" ]]; then
    if [[ -f \"\$BUILD_DIR/set-capabilities.sh\" ]]; then
        echo \"Setting Linux capabilities...\"
        if bash \"\$BUILD_DIR/set-capabilities.sh\"; then
            echo \"✓ Capabilities set successfully\"
        else
            echo \"⚠ Warning: Failed to set capabilities (non-fatal)\"
        fi
    fi
else
    echo \"⚠ Warning: Cannot set capabilities (no sudo access)\"
    echo \"  Run manually: sudo bash \$BUILD_DIR/set-capabilities.sh\"
fi

echo \"Automated setup complete.\"
")
    
    set(AUTO_SETUP_SCRIPT "${CMAKE_BINARY_DIR}/alaris-auto-setup.sh")
    file(WRITE "${AUTO_SETUP_SCRIPT}" "${AUTO_SETUP_CONTENT}")
    execute_process(COMMAND chmod +x "${AUTO_SETUP_SCRIPT}" ERROR_QUIET)
    set(ALARIS_AUTO_SETUP_SCRIPT "${AUTO_SETUP_SCRIPT}" CACHE INTERNAL "")
    message(STATUS "Created automated setup script: ${AUTO_SETUP_SCRIPT}")
endfunction()

# Create startup script
function(create_startup_script)
    set(STARTUP_CONTENT "#!/bin/bash
set -e
BUILD_DIR=\"${CMAKE_BINARY_DIR}\"
cd \"\$BUILD_DIR\"

MODE=\"\${1:-paper}\"
IBKR_HOST=\$(grep \"host:\" ../config/lean_process.yaml | awk '{print \$2}' | tr -d '\"' 2>/dev/null || echo \"127.0.0.1\")

echo \"Starting Alaris Trading System in \$MODE mode...\"
echo \"IBKR Host: \$IBKR_HOST\"

# Check for required executables
if [[ ! -f \"./bin/quantlib-process\" ]]; then
    echo \"Error: quantlib-process not found. Run 'cmake --build .' first.\"
    exit 1
fi

if [[ ! -f \"./bin/Alaris.Lean.dll\" ]] && [[ ! -d \"./bin/Release\" ]]; then
    echo \"Error: Lean process not found. Ensure .NET build completed successfully.\"
    exit 1
fi

# Clean shared memory
echo \"Cleaning shared memory...\"
sudo rm -f /dev/shm/alaris_* 2>/dev/null || rm -f /dev/shm/alaris_* 2>/dev/null || true

cleanup() {
    echo \"Shutting down Alaris...\"
    [[ -n \$QUANTLIB_PID ]] && kill \$QUANTLIB_PID 2>/dev/null || true
    [[ -n \$LEAN_PID ]] && kill \$LEAN_PID 2>/dev/null || true
    sudo rm -f /dev/shm/alaris_* 2>/dev/null || rm -f /dev/shm/alaris_* 2>/dev/null || true
    echo \"Cleanup complete.\"
}
trap cleanup EXIT INT TERM

# Start QuantLib process
echo \"Starting QuantLib process...\"
./bin/quantlib-process ../config/quantlib_process.yaml &
QUANTLIB_PID=\$!
echo \"QuantLib PID: \$QUANTLIB_PID\"
sleep 3

# Start Lean process
echo \"Starting Lean process...\"
if [[ -f \"./bin/Alaris.Lean.dll\" ]]; then
    dotnet ./bin/Alaris.Lean.dll --mode \"\$MODE\" &
elif [[ -d \"./bin/Release\" ]]; then
    dotnet ./bin/Release/Alaris.Lean.dll --mode \"\$MODE\" &
else
    echo \"Error: Could not find Lean executable\"
    exit 1
fi
LEAN_PID=\$!
echo \"Lean PID: \$LEAN_PID\"

echo \"Alaris Trading System started successfully!\"
echo \"Press Ctrl+C to stop...\"

wait \$LEAN_PID
")
    
    set(STARTUP_SCRIPT "${CMAKE_BINARY_DIR}/start-alaris.sh")
    file(WRITE "${STARTUP_SCRIPT}" "${STARTUP_CONTENT}")
    execute_process(COMMAND chmod +x "${STARTUP_SCRIPT}" ERROR_QUIET)
    set(ALARIS_STARTUP_SCRIPT "${STARTUP_SCRIPT}" CACHE INTERNAL "")
    message(STATUS "Created startup script: ${STARTUP_SCRIPT}")
endfunction()

# Check sudo availability for better user feedback
function(check_sudo_availability)
    if(UNIX AND NOT APPLE)
        find_program(SUDO_EXECUTABLE sudo)
        if(SUDO_EXECUTABLE)
            execute_process(
                COMMAND sudo -n true
                RESULT_VARIABLE SUDO_CHECK_RESULT
                OUTPUT_QUIET
                ERROR_QUIET
            )
            if(SUDO_CHECK_RESULT EQUAL 0)
                set(ALARIS_CAN_SUDO TRUE CACHE INTERNAL "")
                message(STATUS "Sudo access available - capabilities will be set automatically")
            else
                set(ALARIS_CAN_SUDO FALSE CACHE INTERNAL "")
                message(STATUS "Sudo access not available - capabilities must be set manually")
            endif()
        else()
            set(ALARIS_CAN_SUDO FALSE CACHE INTERNAL "")
        endif()
    endif()
endfunction()

# Execute setup functions with proper ordering
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    check_sudo_availability()
    create_capability_script()
    create_automated_setup()
else()
    message(STATUS "Capability setting disabled or not available")
endif()

create_startup_script()

# Summary message
if(ALARIS_CAPABILITIES_AVAILABLE)
    message(STATUS "")
    message(STATUS "=== Alaris Setup Scripts ===")
    if(ALARIS_CAPABILITY_SCRIPT)
        message(STATUS "  Capabilities: ${ALARIS_CAPABILITY_SCRIPT}")
    endif()
    if(ALARIS_AUTO_SETUP_SCRIPT)
        message(STATUS "  Auto Setup:   ${ALARIS_AUTO_SETUP_SCRIPT}")
    endif()
    if(ALARIS_STARTUP_SCRIPT)
        message(STATUS "  Startup:      ${ALARIS_STARTUP_SCRIPT}")
    endif()
    message(STATUS "===========================")
endif()
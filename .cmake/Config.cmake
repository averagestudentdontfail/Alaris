# .cmake/Config.cmake

# Platform-specific configuration
if(UNIX AND NOT APPLE)
    option(ALARIS_SET_CAPABILITIES "Set Linux capabilities automatically" ON)
    
    find_program(SETCAP_EXECUTABLE setcap)
    find_program(GETCAP_EXECUTABLE getcap)
    
    if(SETCAP_EXECUTABLE AND GETCAP_EXECUTABLE)
        set(ALARIS_CAPABILITIES_AVAILABLE TRUE)
    else()
        set(ALARIS_CAPABILITIES_AVAILABLE FALSE)
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

# Create capability script
function(create_capability_script)
    if(NOT ALARIS_CAPABILITIES_AVAILABLE)
        return()
    endif()
    
    set(SCRIPT_CONTENT "#!/bin/bash
set -e
BUILD_DIR=\"${CMAKE_BINARY_DIR}\"
CAPS=\"${ALARIS_QUANTLIB_CAPABILITIES}\"

set_caps() {
    local exe=\"\$1\"
    if [[ -f \"\$exe\" ]]; then
        sudo setcap -r \"\$exe\" 2>/dev/null || true
        sudo setcap \"\$CAPS\" \"\$exe\"
    fi
}

set_caps \"\${BUILD_DIR}/bin/quantlib-process\"
set_caps \"\${BUILD_DIR}/bin/alaris\"
")
    
    set(SCRIPT_PATH "${CMAKE_BINARY_DIR}/set-capabilities.sh")
    file(WRITE "${SCRIPT_PATH}" "${SCRIPT_CONTENT}")
    execute_process(COMMAND chmod +x "${SCRIPT_PATH}" ERROR_QUIET)
    set(ALARIS_CAPABILITY_SCRIPT "${SCRIPT_PATH}" PARENT_SCOPE)
endfunction()

# Create automated setup
function(create_automated_setup)
    if(NOT ALARIS_CAPABILITIES_AVAILABLE)
        return()
    endif()
    
    set(AUTO_SETUP_CONTENT "#!/bin/bash
set -e
BUILD_DIR=\"${CMAKE_BINARY_DIR}\"

# Create logs directory
mkdir -p \"\$BUILD_DIR/logs\"

# Update logging configuration
if [[ -f \"\$BUILD_DIR/../config/quantlib_process.yaml\" ]]; then
    if grep -q \"/var/log/alaris\" \"\$BUILD_DIR/../config/quantlib_process.yaml\"; then
        cp \"\$BUILD_DIR/../config/quantlib_process.yaml\" \"\$BUILD_DIR/../config/quantlib_process.yaml.backup\"
        sed -i 's|/var/log/alaris/|logs/|g' \"\$BUILD_DIR/../config/quantlib_process.yaml\"
    fi
fi

# Set capabilities if sudo available
if sudo -n true 2>/dev/null || [[ \$EUID -eq 0 ]]; then
    if [[ -f \"\$BUILD_DIR/set-capabilities.sh\" ]]; then
        bash \"\$BUILD_DIR/set-capabilities.sh\" 2>/dev/null || true
    fi
fi
")
    
    set(AUTO_SETUP_SCRIPT "${CMAKE_BINARY_DIR}/alaris-auto-setup.sh")
    file(WRITE "${AUTO_SETUP_SCRIPT}" "${AUTO_SETUP_CONTENT}")
    execute_process(COMMAND chmod +x "${AUTO_SETUP_SCRIPT}" ERROR_QUIET)
    set(ALARIS_AUTO_SETUP_SCRIPT "${AUTO_SETUP_SCRIPT}" PARENT_SCOPE)
endfunction()

# Create startup script
function(create_startup_script)
    set(STARTUP_CONTENT "#!/bin/bash
set -e
BUILD_DIR=\"${CMAKE_BINARY_DIR}\"
cd \"\$BUILD_DIR\"

MODE=\"\${1:-paper}\"
IBKR_HOST=\$(grep \"host:\" ../config/lean_process.yaml | awk '{print \$2}' | tr -d '\"' 2>/dev/null || echo \"127.0.0.1\")

# Clean shared memory
sudo rm -f /dev/shm/alaris_* 2>/dev/null || rm -f /dev/shm/alaris_* 2>/dev/null || true

cleanup() {
    [[ -n \$QUANTLIB_PID ]] && kill \$QUANTLIB_PID 2>/dev/null || true
    [[ -n \$LEAN_PID ]] && kill \$LEAN_PID 2>/dev/null || true
    sudo rm -f /dev/shm/alaris_* 2>/dev/null || rm -f /dev/shm/alaris_* 2>/dev/null || true
}
trap cleanup EXIT INT TERM

# Start QuantLib
./bin/quantlib-process ../config/quantlib_process.yaml &
QUANTLIB_PID=\$!
sleep 3

# Start Lean
dotnet bin/Release/Alaris.Lean.dll --mode \"\$MODE\" &
LEAN_PID=\$!

wait \$LEAN_PID
")
    
    set(STARTUP_SCRIPT "${CMAKE_BINARY_DIR}/start-alaris.sh")
    file(WRITE "${STARTUP_SCRIPT}" "${STARTUP_CONTENT}")
    execute_process(COMMAND chmod +x "${STARTUP_SCRIPT}" ERROR_QUIET)
endfunction()

# Execute setup functions
if(ALARIS_SET_CAPABILITIES AND ALARIS_CAPABILITIES_AVAILABLE)
    create_capability_script()
    create_automated_setup()
endif()
create_startup_script()
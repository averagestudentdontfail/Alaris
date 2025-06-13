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

# Create enhanced startup script with CORRECT mode passing
function(create_startup_script)
    set(STARTUP_CONTENT "#!/bin/bash
set -e
BUILD_DIR=\"${CMAKE_BINARY_DIR}\"
cd \"\$BUILD_DIR\"

MODE=\"\${1:-paper}\"
IBKR_HOST=\$(grep \"host:\" ../config/lean_process.yaml | awk '{print \$2}' | tr -d '\"' 2>/dev/null || echo \"127.0.0.1\")

echo \"Starting Alaris Trading System in \$MODE mode...\"
echo \"IBKR Host: \$IBKR_HOST\"

# Function to setup Lean data environment
setup_lean_data() {
    echo \"Setting up Lean data environment...\"
    
    # Create essential directories
    mkdir -p data/market-hours
    mkdir -p data/symbol-properties
    mkdir -p data/factor-files
    mkdir -p data/map-files
    mkdir -p data/equity/usa/daily
    mkdir -p data/equity/usa/hour
    mkdir -p data/equity/usa/minute
    mkdir -p data/equity/usa/second
    mkdir -p data/equity/usa/tick
    mkdir -p cache
    mkdir -p results
    mkdir -p logs
    
    # Download essential Lean data files if they don't exist
    if [[ ! -f \"data/market-hours/market-hours-database.json\" ]]; then
        echo \"Downloading market hours database...\"
        if command -v curl >/dev/null 2>&1; then
            curl -L -o \"data/market-hours/market-hours-database.json\" \\
                \"https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/market-hours/market-hours-database.json\" || \\
            echo \"Warning: Could not download market-hours-database.json\"
        elif command -v wget >/dev/null 2>&1; then
            wget -O \"data/market-hours/market-hours-database.json\" \\
                \"https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/market-hours/market-hours-database.json\" || \\
            echo \"Warning: Could not download market-hours-database.json\"
        else
            echo \"Warning: Neither curl nor wget available for downloading data files\"
            # Create minimal market hours file
            cat > \"data/market-hours/market-hours-database.json\" << 'EOF'
{
  \"entries\": {
    \"USA\": {
      \"market\": \"usa\",
      \"dataTimeZone\": \"America/New_York\",
      \"exchangeTimeZone\": \"America/New_York\",
      \"sunday\": [],
      \"monday\": [
        { \"start\": \"09:30:00\", \"end\": \"16:00:00\" }
      ],
      \"tuesday\": [
        { \"start\": \"09:30:00\", \"end\": \"16:00:00\" }
      ],
      \"wednesday\": [
        { \"start\": \"09:30:00\", \"end\": \"16:00:00\" }
      ],
      \"thursday\": [
        { \"start\": \"09:30:00\", \"end\": \"16:00:00\" }
      ],
      \"friday\": [
        { \"start\": \"09:30:00\", \"end\": \"16:00:00\" }
      ],
      \"saturday\": [],
      \"holidays\": [],
      \"earlyCloses\": []
    }
  }
}
EOF
            echo \"✓ Created minimal market hours database\"
        fi
    fi
    
    # Download symbol properties database
    if [[ ! -f \"data/symbol-properties/symbol-properties-database.csv\" ]]; then
        echo \"Downloading symbol properties database...\"
        if command -v curl >/dev/null 2>&1; then
            curl -L -o \"data/symbol-properties/symbol-properties-database.csv\" \\
                \"https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/symbol-properties/symbol-properties-database.csv\" || \\
            echo \"Warning: Could not download symbol-properties-database.csv\"
        elif command -v wget >/dev/null 2>&1; then
            wget -O \"data/symbol-properties/symbol-properties-database.csv\" \\
                \"https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/symbol-properties/symbol-properties-database.csv\" || \\
            echo \"Warning: Could not download symbol-properties-database.csv\"
        fi
    fi
    
    echo \"✓ Lean data environment setup complete\"
}

# Handle different modes
case \"\$MODE\" in
    \"download\")
        echo \"=== Data Download Mode ===\"
        setup_lean_data
        
        # Check if QuantConnect API credentials are available
        local qc_user_id=\"\${QC_USER_ID:-}\"
        local qc_api_token=\"\${QC_API_TOKEN:-}\"
        
        if [[ -z \"\$qc_user_id\" || -z \"\$qc_api_token\" ]]; then
            echo \"\"
            echo \"⚠ QuantConnect API credentials not found in environment variables.\"
            echo \"For downloading historical data:\"
            echo \"\"
            echo \"1. Sign up for a free account at https://www.quantconnect.com\"
            echo \"2. Get your API credentials from your account dashboard\"
            echo \"3. Set environment variables:\"
            echo \"   export QC_USER_ID='your-user-id'\"
            echo \"   export QC_API_TOKEN='your-api-token'\"
            echo \"4. Re-run this download command\"
            echo \"\"
            read -p \"Continue with limited data download? (y/n): \" -n 1 -r
            echo
            if [[ ! \$REPLY =~ ^[Yy]\$ ]]; then
                echo \"Data download cancelled. Set up QuantConnect API credentials and try again.\"
                exit 1
            fi
            echo \"Proceeding with limited data download...\"
        else
            echo \"✓ QuantConnect API credentials found\"
            echo \"Using QuantConnect data service for historical data download\"
        fi
        
        # Check for required executables
        if [[ ! -f \"./bin/Alaris.Lean.dll\" ]]; then
            echo \"✗ Lean process not found. Please build the project first:\"
            echo \"  cmake --build .\"
            exit 1
        fi
        
        echo \"\"
        echo \"Starting Lean engine for data download...\"
        echo \"This will download historical data for all configured symbols.\"
        echo \"Download may take 10-30 minutes depending on data amount and connection speed.\"
        echo \"\"
        
        # Set environment variables for data download
        export QC_USER_ID=\"\$qc_user_id\"
        export QC_API_TOKEN=\"\$qc_api_token\"
        
        # Start the data download process - THIS IS THE KEY FIX
        local download_log=\"logs/data_download_\$(date +%Y%m%d_%H%M%S).log\"
        
        echo \"Download progress (this may take a while):\"
        echo \"Log file: \$download_log\"
        echo \"\"
        
        # CRITICAL FIX: Use --mode download, not --mode backtest
        if timeout 1800 dotnet \"./bin/Alaris.Lean.dll\" --mode download 2>&1 | tee \"\$download_log\"; then
            echo \"\"
            echo \"✓ Data download process completed!\"
            echo \"\"
            
            # Check what was actually downloaded
            local symbols_with_data=0
            local total_files=0
            
            echo \"Downloaded data summary:\"
            for symbol_dir in data/equity/usa/*/; do
                if [[ -d \"\$symbol_dir\" ]]; then
                    local symbol=\$(basename \"\$symbol_dir\")
                    local zip_files=\$(find \"\$symbol_dir\" -name \"*.zip\" 2>/dev/null | wc -l)
                    local csv_files=\$(find \"\$symbol_dir\" -name \"*.csv\" 2>/dev/null | wc -l)
                    local total_symbol_files=\$((zip_files + csv_files))
                    
                    if [[ \$total_symbol_files -gt 0 ]]; then
                        echo \"  ✓ \$symbol: \$total_symbol_files files (\$zip_files zip, \$csv_files csv)\"
                        ((symbols_with_data++))
                        ((total_files += total_symbol_files))
                    fi
                fi
            done
            
            echo \"\"
            if [[ \$symbols_with_data -gt 0 ]]; then
                echo \"✓ Successfully downloaded data for \$symbols_with_data symbols (\$total_files total files)\"
                echo \"✓ Data ready for backtesting and analysis\"
                echo \"\"
                echo \"Data location: \$(pwd)/data/\"
                echo \"\"
                echo \"Next steps:\"
                echo \"  ./start-alaris.sh backtest  # Test strategy with downloaded data\"
                echo \"  ./start-alaris.sh paper     # Live forward testing\"
                echo \"\"
            else
                echo \"⚠ No data files found after download process\"
                echo \"\"
                echo \"Check the download log for details:\"
                echo \"  cat \$download_log\"
                echo \"\"
            fi
        else
            echo \"\"
            echo \"✗ Data download failed or timed out (30 minute limit)\"
            echo \"\"
            echo \"Check the download log for details:\"
            echo \"  cat \$download_log\"
            echo \"\"
        fi
        exit 0
        ;;
    \"backtest\")
        echo \"=== Backtest Mode ===\"
        setup_lean_data
        # Check for data availability
        local data_count=\$(find data/equity/usa -name \"*.csv\" -o -name \"*.zip\" 2>/dev/null | wc -l)
        if [[ \$data_count -lt 5 ]]; then
            echo \"\"
            echo \"Historical data required for backtesting. Options:\"
            echo \"  1. Run './start-alaris.sh download' to download real data\"
            echo \"  2. Use './start-alaris.sh paper' for live forward testing\"
            echo \"\"
            read -p \"Download real data now? (y/n): \" -n 1 -r
            echo
            if [[ \$REPLY =~ ^[Yy]\$ ]]; then
                echo \"Starting data download...\"
                exec \"\$0\" download
            else
                echo \"Skipping data download.\"
                echo \"Run './start-alaris.sh download' when ready for backtesting.\"
                exit 1
            fi
        fi
        ;;
    \"paper\"|\"live\")
        echo \"=== \$(echo \$MODE | tr '[:lower:]' '[:upper:]') Trading Mode ===\"
        setup_lean_data
        ;;
    *)
        echo \"Usage: \$0 {download|backtest|paper|live}\"
        echo \"\"
        echo \"Modes:\"
        echo \"  download  - Download historical data from QuantConnect API\"
        echo \"  backtest  - Run strategy backtest on historical data\"
        echo \"  paper     - Forward test with paper trading account\"
        echo \"  live      - Live trading with real money (use with caution)\"
        echo \"\"
        echo \"Setup:\"
        echo \"  1. Get QuantConnect API credentials: https://www.quantconnect.com\"
        echo \"  2. Export QC_USER_ID and QC_API_TOKEN environment variables\"
        echo \"  3. Run './start-alaris.sh download' to get real data\"
        echo \"\"
        exit 1
        ;;
esac

# Check for required executables (for non-download modes)
if [[ ! -f \"./bin/quantlib-process\" ]]; then
    echo \"Error: quantlib-process not found. Run 'cmake --build .' first.\"
    exit 1
fi

if [[ ! -f \"./bin/Alaris.Lean.dll\" ]]; then
    echo \"Error: Lean process not found. Ensure .NET build completed successfully.\"
    exit 1
fi

# Test IBKR connectivity for trading modes
if [[ \"\$MODE\" == \"paper\" || \"\$MODE\" == \"live\" ]]; then
    echo \"Testing IBKR connectivity...\"
    local port
    if [[ \"\$MODE\" == \"paper\" ]]; then
        port=4002
    else
        port=4001
    fi
    
    if ! timeout 5 bash -c 'cat < /dev/null > /dev/tcp/'\$IBKR_HOST'/'\$port 2>/dev/null; then
        echo \"Warning: Cannot connect to IBKR on \$IBKR_HOST:\$port\"
        echo \"Please ensure IB Gateway/TWS is running and configured properly.\"
        echo \"\"
        read -p \"Continue anyway? (y/n): \" -n 1 -r
        echo
        if [[ ! \$REPLY =~ ^[Yy]\$ ]]; then
            exit 1
        fi
    else
        echo \"✓ IBKR connection available\"
    fi
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

# Start QuantLib process (for trading modes)
if [[ \"\$MODE\" != \"download\" ]]; then
    echo \"Starting QuantLib process...\"
    ./bin/quantlib-process ../config/quantlib_process.yaml &
    QUANTLIB_PID=\$!
    echo \"QuantLib PID: \$QUANTLIB_PID\"
    sleep 3
fi

# Start Lean process - CRITICAL FIX: Pass the correct mode
echo \"Starting Lean process...\"
dotnet ./bin/Alaris.Lean.dll --mode \"\$MODE\" &
LEAN_PID=\$!
echo \"Lean PID: \$LEAN_PID\"

echo \"\"
echo \"Alaris Trading System started successfully!\"
echo \"Mode: \$MODE\"
if [[ -n \$QUANTLIB_PID ]]; then
    echo \"QuantLib PID: \$QUANTLIB_PID\"
fi
echo \"Lean PID: \$LEAN_PID\"
echo \"\"
echo \"Press Ctrl+C to stop...\"

wait \$LEAN_PID
")
    
    set(STARTUP_SCRIPT "${CMAKE_BINARY_DIR}/start-alaris.sh")
    file(WRITE "${STARTUP_SCRIPT}" "${STARTUP_CONTENT}")
    execute_process(COMMAND chmod +x "${STARTUP_SCRIPT}" ERROR_QUIET)
    set(ALARIS_STARTUP_SCRIPT "${STARTUP_SCRIPT}" CACHE INTERNAL "")
    message(STATUS "Created enhanced startup script: ${STARTUP_SCRIPT}")
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
            else()
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
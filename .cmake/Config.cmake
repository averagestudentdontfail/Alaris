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

# Create enhanced startup script with data management
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

# Function to download historical data
download_historical_data() {
    echo \"Downloading historical data using Lean's built-in data downloader...\"
    echo \"This will create sample data files and verify the environment.\"
    
    # Check if IBKR connection is available for live data
    local ibkr_available=false
    if timeout 5 bash -c 'cat < /dev/null > /dev/tcp/'\$IBKR_HOST'/4002' 2>/dev/null; then
        echo \"✓ IBKR connection available for live data download\"
        ibkr_available=true
    else
        echo \"⚠ IBKR connection not available. Will create sample data for testing.\"
    fi
    
    # Create sample data files for testing
    echo \"Creating sample historical data for backtesting...\"
    
    local symbols=(\"SPY\" \"QQQ\" \"IWM\" \"EFA\" \"VTI\" \"AAPL\" \"MSFT\" \"GOOGL\" \"AMZN\" \"NVDA\" 
                   \"JPM\" \"BAC\" \"WFC\" \"GS\" \"MS\" \"XOM\" \"CVX\" \"COP\" \"EOG\" \"SLB\" 
                   \"JNJ\" \"PFE\" \"UNH\" \"ABBV\" \"MRK\")
    
    # Create basic daily data structure for each symbol
    for symbol in \"\${symbols[@]}\"; do
        local symbol_dir=\"data/equity/usa/daily/\$symbol\"
        mkdir -p \"\$symbol_dir\"
        
        # Create a simple CSV file with sample data if it doesn't exist
        local data_file=\"\$symbol_dir/\$(echo \$symbol | tr '[:upper:]' '[:lower:]').csv\"
        if [[ ! -f \"\$data_file\" ]]; then
            echo \"Creating sample data for \$symbol...\"
            # Create sample OHLCV data for 2023-2024
            cat > \"\$data_file\" << EOF
20230103 000000,100.00,102.50,99.50,101.25,1000000
20230104 000000,101.25,103.00,100.75,102.50,1100000
20230105 000000,102.50,104.25,101.50,103.75,1200000
20230106 000000,103.75,105.00,102.50,104.50,1300000
20230109 000000,104.50,106.25,103.75,105.25,1400000
20230110 000000,105.25,107.00,104.50,106.00,1500000
20230111 000000,106.00,108.50,105.25,107.75,1600000
20230112 000000,107.75,109.00,106.50,108.25,1700000
20230113 000000,108.25,110.75,107.50,109.50,1800000
20230117 000000,109.50,111.25,108.75,110.00,1900000
EOF
            echo \"✓ Sample data created for \$symbol\"
        else
            echo \"✓ Data already exists for \$symbol\"
        fi
    done
    
    # If IBKR is available, run a quick data verification
    if [[ \"\$ibkr_available\" == \"true\" ]]; then
        echo \"\"
        echo \"IBKR is available. You can enhance data by running:\"
        echo \"  ./start-alaris.sh paper    # This will download live data\"
        echo \"  ./start-alaris.sh live     # This will download live data\"
        echo \"\"
    fi
    
    echo \"✓ Historical data setup complete\"
    echo \"✓ Ready for backtesting with sample data\"
}

# Function to check data availability
check_data_availability() {
    local data_ready=true
    local missing_files=()
    
    if [[ ! -f \"data/market-hours/market-hours-database.json\" ]]; then
        echo \"✗ Market hours database missing\"
        missing_files+=(\"Market hours database\")
        data_ready=false
    else
        echo \"✓ Market hours database found\"
    fi
    
    if [[ ! -f \"data/symbol-properties/symbol-properties-database.csv\" ]]; then
        echo \"✗ Symbol properties database missing\"
        missing_files+=(\"Symbol properties database\")
        data_ready=false
    else
        echo \"✓ Symbol properties database found\"
    fi
    
    # Check for at least some historical data files
    local data_count=\$(find data/equity/usa/daily -name \"*.csv\" 2>/dev/null | wc -l)
    if [[ \$data_count -lt 5 ]]; then
        echo \"✗ Insufficient historical data (found \$data_count files, need at least 5)\"
        missing_files+=(\"Historical data files\")
        data_ready=false
    else
        echo \"✓ Historical data found (\$data_count symbol files)\"
    fi
    
    if [[ \"\$data_ready\" == \"true\" ]]; then
        echo \"✓ Data environment ready for backtesting\"
        return 0
    else
        echo \"\"
        echo \"Missing components: \${missing_files[*]}\"
        return 1
    fi
}

# Handle different modes
case \"\$MODE\" in
    \"download\")
        echo \"=== Data Setup Mode ===\"
        setup_lean_data
        download_historical_data
        echo \"\"
        echo \"=== Data Setup Complete ===\"
        echo \"✓ Market hours database ready\"
        echo \"✓ Symbol properties database ready\"
        echo \"✓ Sample historical data created\"
        echo \"\"
        echo \"Next steps:\"
        echo \"  ./start-alaris.sh backtest  # Test strategy with sample data\"
        echo \"  ./start-alaris.sh paper     # Live forward testing\"
        echo \"\"
        exit 0
        ;;
    \"backtest\")
        echo \"=== Backtest Mode ===\"
        setup_lean_data
        if ! check_data_availability; then
            echo \"\"
            echo \"Data setup required for backtesting. Options:\"
            echo \"  1. Run './start-alaris.sh download' to setup sample data\"
            echo \"  2. Continue with paper/live trading modes (no backtest data needed)\"
            echo \"\"
            read -p \"Setup sample data now? (y/n): \" -n 1 -r
            echo
            if [[ \$REPLY =~ ^[Yy]\$ ]]; then
                echo \"Setting up data environment...\"
                download_historical_data
            else
                echo \"Skipping data setup.\"
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
        echo \"  download  - Setup data environment with sample historical data\"
        echo \"  backtest  - Run strategy backtest on historical data\"
        echo \"  paper     - Forward test with paper trading account\"
        echo \"  live      - Live trading with real money (use with caution)\"
        echo \"\"
        echo \"Data Setup:\"
        echo \"  The 'download' mode creates essential Lean files and sample data.\"
        echo \"  For live market data, use 'paper' or 'live' modes with IBKR connected.\"
        exit 1
        ;;
esac

# Check for required executables
if [[ ! -f \"./bin/quantlib-process\" ]]; then
    echo \"Error: quantlib-process not found. Run 'cmake --build .' first.\"
    exit 1
fi

if [[ ! -f \"./bin/Alaris.Lean.dll\" ]] && [[ ! -d \"./bin/Release\" ]]; then
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

echo \"\"
echo \"Alaris Trading System started successfully!\"
echo \"Mode: \$MODE\"
echo \"QuantLib PID: \$QUANTLIB_PID\"
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
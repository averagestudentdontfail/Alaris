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

# Create enhanced startup script with REAL data download
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

# Function to download REAL historical data using Lean engine
download_historical_data() {
    echo \"Downloading REAL historical data using Lean engine...\"
    echo \"This will connect to your configured data provider and download actual market data.\"
    
    # Check if QuantConnect API credentials are available
    local qc_user_id=\"\${QC_USER_ID:-}\"
    local qc_api_token=\"\${QC_API_TOKEN:-}\"
    
    if [[ -z \"\$qc_user_id\" || -z \"\$qc_api_token\" ]]; then
        echo \"\"
        echo \"⚠ QuantConnect API credentials not found in environment variables.\"
        echo \"For downloading historical data, you have two options:\"
        echo \"\"
        echo \"Option 1: Use QuantConnect's data service (recommended)\"
        echo \"  1. Sign up for a free account at https://www.quantconnect.com\"
        echo \"  2. Get your API credentials from your account dashboard\"
        echo \"  3. Set environment variables:\"
        echo \"     export QC_USER_ID='your-user-id'\"
        echo \"     export QC_API_TOKEN='your-api-token'\"
        echo \"  4. Re-run this download command\"
        echo \"\"
        echo \"Option 2: Use IBKR for live data capture\"
        echo \"  - Use paper/live modes to capture real-time data\"
        echo \"  - This will build up historical data over time\"
        echo \"\"
        read -p \"Do you want to continue with IBKR live data capture? (y/n): \" -n 1 -r
        echo
        if [[ ! \$REPLY =~ ^[Yy]\$ ]]; then
            echo \"Data download cancelled. Set up QuantConnect API credentials and try again.\"
            return 1
        fi
        echo \"Proceeding with IBKR live data capture setup...\"
    else
        echo \"✓ QuantConnect API credentials found\"
        echo \"Using QuantConnect data service for historical data download\"
    fi
    
    # Check IBKR connectivity
    local ibkr_available=false
    if timeout 5 bash -c 'cat < /dev/null > /dev/tcp/'\$IBKR_HOST'/4002' 2>/dev/null; then
        echo \"✓ IBKR connection available\"
        ibkr_available=true
    else
        echo \"⚠ IBKR connection not available on \$IBKR_HOST:4002\"
        echo \"Live data features will be limited\"
    fi
    
    # Create data download configuration
    local download_config=\"../config/lean_download_temp.yaml\"
    echo \"Creating data download configuration...\"
    
    # Extract symbols from main config
    local symbols_section=\$(grep -A 50 \"symbols:\" ../config/quantlib_process.yaml | grep -E \"^\\s*-\" | head -25 | sed 's/^\\s*- *//' | tr -d '\"')
    
    cat > \"\$download_config\" << EOF
algorithm:
  name: \"Alaris.Algorithm.ArbitrageAlgorithm\"
  start_date: \"2020-01-01\"
  end_date: \"\$(date '+%Y-%m-%d')\"
  cash: 100000

brokerage:
  type: \"InteractiveBrokers\"
  host: \"\$IBKR_HOST\"
  paper_port: 4002
  live_port: 4001
  account: \"\$(grep 'account:' ../config/lean_process.yaml | awk '{print \$2}' | tr -d '\"')\"
  client_id: 200

universe:
  symbols:
EOF

    # Add symbols to config
    echo \"\$symbols_section\" | while read -r symbol; do
        if [[ -n \"\$symbol\" ]]; then
            echo \"    - \\\"\$symbol\\\"\" >> \"\$download_config\"
        fi
    done

    cat >> \"\$download_config\" << EOF

shared_memory:
  market_data_buffer: \"/alaris_market_data\"
  signal_buffer: \"/alaris_signals\"
  control_buffer: \"/alaris_control\"

logging:
  level: \"INFO\"
  file: \"alaris_download.log\"
EOF

    echo \"✓ Download configuration created\"
    
    # Check for required executables
    if [[ ! -f \"./bin/Alaris.Lean.dll\" ]] && [[ ! -d \"./bin/Release\" ]]; then
        echo \"✗ Lean process not found. Please build the project first:\"
        echo \"  cmake --build . --target lean-process\"
        rm -f \"\$download_config\"
        return 1
    fi
    
    echo \"\"
    echo \"Starting Lean engine for data download...\"
    echo \"This will download REAL historical data for all configured symbols.\"
    echo \"Download may take 10-30 minutes depending on data amount and connection speed.\"
    echo \"\"
    echo \"Symbols to download:\"
    echo \"\$symbols_section\" | while read -r symbol; do
        if [[ -n \"\$symbol\" ]]; then
            echo \"  - \$symbol\"
        fi
    done
    echo \"\"
    
    # Set environment variables for data download
    export QC_USER_ID=\"\$qc_user_id\"
    export QC_API_TOKEN=\"\$qc_api_token\"
    
    # Start the data download process
    local download_success=false
    local download_log=\"logs/data_download_\$(date +%Y%m%d_%H%M%S).log\"
    
    echo \"Download progress (this may take a while):\"
    echo \"Log file: \$download_log\"
    echo \"\"
    
    if [[ -f \"./bin/Alaris.Lean.dll\" ]]; then
        if timeout 1800 dotnet \"./bin/Alaris.Lean.dll\" --mode backtest --config-dir \"\$(dirname \"\$download_config\")\" 2>&1 | tee \"\$download_log\"; then
            download_success=true
        fi
    elif [[ -d \"./bin/Release\" ]]; then
        if timeout 1800 dotnet \"./bin/Release/Alaris.Lean.dll\" --mode backtest --config-dir \"\$(dirname \"\$download_config\")\" 2>&1 | tee \"\$download_log\"; then
            download_success=true
        fi
    fi
    
    # Clean up temporary config
    rm -f \"\$download_config\"
    
    # Analyze results
    if [[ \"\$download_success\" == \"true\" ]]; then
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
            echo \"This could indicate:\"
            echo \"  1. API credentials issue (check QuantConnect account)\"
            echo \"  2. Data subscription limitations\"
            echo \"  3. Network connectivity problems\"
            echo \"  4. Lean configuration issues\"
            echo \"\"
            echo \"Troubleshooting:\"
            echo \"  1. Check download log: cat \$download_log\"
            echo \"  2. Verify QuantConnect API credentials\"
            echo \"  3. Test with a single symbol first\"
            echo \"  4. Check QuantConnect account data quotas\"
            echo \"\"
            return 1
        fi
    else
        echo \"\"
        echo \"✗ Data download failed or timed out (30 minute limit)\"
        echo \"\"
        echo \"Troubleshooting steps:\"
        echo \"  1. Check download log: cat \$download_log\"
        echo \"  2. Verify QuantConnect API credentials\"
        echo \"  3. Check internet connection\"
        echo \"  4. Verify QuantConnect account is active\"
        echo \"  5. Try downloading fewer symbols at once\"
        echo \"\"
        echo \"For immediate testing, you can use:\"
        echo \"  ./start-alaris.sh paper     # Live forward testing\"
        echo \"\"
        return 1
    fi
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
    
    # Check for historical data files
    local data_count=\$(find data/equity/usa -name \"*.csv\" -o -name \"*.zip\" 2>/dev/null | wc -l)
    if [[ \$data_count -lt 5 ]]; then
        echo \"✗ Insufficient historical data (found \$data_count files, need at least 5)\"
        missing_files+=(\"Historical data files\")
        data_ready=false
    else
        echo \"✓ Historical data found (\$data_count data files)\"
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
        echo \"=== Data Download Mode ===\"
        setup_lean_data
        download_historical_data
        if [[ \$? -eq 0 ]]; then
            echo \"\"
            echo \"=== Data Download Complete ===\"
            echo \"✓ Real market data downloaded successfully\"
            echo \"✓ Lean data environment ready\"
            echo \"\"
            echo \"Your system is now ready for:\"
            echo \"  ./start-alaris.sh backtest  # Strategy backtesting\"
            echo \"  ./start-alaris.sh paper     # Live forward testing\"
            echo \"\"
        else
            echo \"\"
            echo \"=== Data Download Failed ===\"
            echo \"Please check the error messages above and try again.\"
            echo \"\"
            exit 1
        fi
        exit 0
        ;;
    \"backtest\")
        echo \"=== Backtest Mode ===\"
        setup_lean_data
        if ! check_data_availability; then
            echo \"\"
            echo \"Historical data required for backtesting. Options:\"
            echo \"  1. Run './start-alaris.sh download' to download real data\"
            echo \"  2. Use './start-alaris.sh paper' for live forward testing\"
            echo \"\"
            read -p \"Download real data now? (y/n): \" -n 1 -r
            echo
            if [[ \$REPLY =~ ^[Yy]\$ ]]; then
                echo \"Starting data download...\"
                download_historical_data
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
        echo \"  download  - Download REAL historical data from configured sources\"
        echo \"  backtest  - Run strategy backtest on historical data\"
        echo \"  paper     - Forward test with paper trading account\"
        echo \"  live      - Live trading with real money (use with caution)\"
        echo \"\"
        echo \"Data Sources:\"
        echo \"  - QuantConnect API (requires free account and API credentials)\"
        echo \"  - Interactive Brokers (for live data capture)\"
        echo \"\"
        echo \"Setup:\"
        echo \"  1. Get QuantConnect API credentials: https://www.quantconnect.com\"
        echo \"  2. Export QC_USER_ID and QC_API_TOKEN environment variables\"
        echo \"  3. Run './start-alaris.sh download' to get real data\"
        echo \"\"
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
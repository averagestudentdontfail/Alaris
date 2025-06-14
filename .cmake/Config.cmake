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

# Create production-grade startup script with comprehensive QuantConnect API support
function(create_startup_script)
    set(STARTUP_CONTENT "#!/bin/bash
# Production-grade Alaris startup script with comprehensive QuantConnect API support
set -e
BUILD_DIR=\"${CMAKE_BINARY_DIR}\"
cd \"\$BUILD_DIR\"

MODE=\"\${1:-paper}\"
DEBUG_FLAG=\"\${2:-}\"
VERBOSE_FLAG=\"\${3:-}\"

# Enhanced logging functions
log_info() {
    echo \"[INFO] \$1\"
}

log_warn() {
    echo \"[WARN] \$1\" >&2
}

log_error() {
    echo \"[ERROR] \$1\" >&2
}

log_success() {
    echo \"[SUCCESS] \$1\"
}

# Function to validate QuantConnect credentials
validate_qc_credentials() {
    local user_id=\"\${QC_USER_ID:-}\"
    local api_token=\"\${QC_API_TOKEN:-}\"
    
    if [[ -z \"\$user_id\" || -z \"\$api_token\" ]]; then
        return 1
    fi
    
    # Validate user ID format (should be numeric)
    if ! [[ \"\$user_id\" =~ ^[0-9]+\$ ]]; then
        log_error \"QC_USER_ID must be numeric. Current value: '\$user_id'\"
        return 1
    fi
    
    # Validate API token format (should be hex string, typically 64 characters)
    if [[ \${#api_token} -lt 32 ]]; then
        log_error \"QC_API_TOKEN appears to be too short. Expected at least 32 characters, got \${#api_token}\"
        return 1
    fi
    
    if ! [[ \"\$api_token\" =~ ^[a-fA-F0-9]+\$ ]]; then
        log_error \"QC_API_TOKEN should be a hexadecimal string\"
        return 1
    fi
    
    return 0
}

# Function to display credential setup instructions
show_credential_setup() {
    echo \"\"
    echo \"==============================================================\"
    echo \"          QuantConnect API Credentials Required\"
    echo \"==============================================================\"
    echo \"\"
    echo \"To download real historical data, you need QuantConnect API credentials:\"
    echo \"\"
    echo \"1. Create a free account at: https://www.quantconnect.com\"
    echo \"2. Sign in and go to your account dashboard\"
    echo \"3. Navigate to the API section\"
    echo \"4. Find your credentials:\"
    echo \"   • User ID: A numeric value (e.g., 1234567)\"
    echo \"   • API Token: A long hexadecimal string\"
    echo \"5. Set them as environment variables:\"
    echo \"\"
    echo \"   export QC_USER_ID='your-numeric-user-id'\"
    echo \"   export QC_API_TOKEN='your-hex-api-token'\"
    echo \"\"
    echo \"6. Important: Ensure you have:\"
    echo \"   • Signed the data agreement in your account\"
    echo \"   • Sufficient credit balance for data downloads\"
    echo \"   • Active data subscription (if required)\"
    echo \"\"
    echo \"7. Re-run this command after setting credentials\"
    echo \"\"
    echo \"==============================================================\"
    echo \"\"
}

# Function to test network connectivity
test_network_connectivity() {
    log_info \"Testing network connectivity to QuantConnect...\"
    
    if command -v curl >/dev/null 2>&1; then
        if curl -s --connect-timeout 10 \"https://www.quantconnect.com\" >/dev/null; then
            log_success \"Network connectivity to QuantConnect: OK\"
            return 0
        else
            log_error \"Cannot reach QuantConnect servers\"
            return 1
        fi
    elif command -v wget >/dev/null 2>&1; then
        if wget -q --timeout=10 --spider \"https://www.quantconnect.com\" 2>/dev/null; then
            log_success \"Network connectivity to QuantConnect: OK\"
            return 0
        else
            log_error \"Cannot reach QuantConnect servers\"
            return 1
        fi
    else
        log_warn \"Neither curl nor wget available - cannot test connectivity\"
        return 0
    fi
}

# Enhanced data environment setup
setup_lean_data_environment() {
    log_info \"Setting up comprehensive Lean data environment...\"
    
    # Create all required directories
    local directories=(
        \"data/market-hours\"
        \"data/symbol-properties\" 
        \"data/factor-files\"
        \"data/map-files\"
        \"data/fundamental\"
        \"data/alternative\"
        \"data/equity/usa/daily\"
        \"data/equity/usa/hour\"
        \"data/equity/usa/minute\"
        \"data/equity/usa/second\"
        \"data/equity/usa/tick\"
        \"data/option/usa\"
        \"data/forex\"
        \"data/cfd\"
        \"data/crypto\"
        \"cache\"
        \"results\" 
        \"logs\"
    )
    
    for dir in \"\${directories[@]}\"; do
        mkdir -p \"\$dir\"
    done
    
    # Download essential configuration files
    download_essential_files
    
    log_success \"Lean data environment setup complete\"
}

# Download essential Lean configuration files
download_essential_files() {
    log_info \"Downloading essential Lean configuration files...\"
    
    # Market hours database
    if [[ ! -f \"data/market-hours/market-hours-database.json\" ]]; then
        log_info \"Downloading market hours database...\"
        if download_file \\
            \"https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/market-hours/market-hours-database.json\" \\
            \"data/market-hours/market-hours-database.json\"; then
            log_success \"Market hours database downloaded\"
        else
            log_warn \"Failed to download market hours database - creating minimal version\"
            create_minimal_market_hours
        fi
    fi
    
    # Symbol properties database
    if [[ ! -f \"data/symbol-properties/symbol-properties-database.csv\" ]]; then
        log_info \"Downloading symbol properties database...\"
        if ! download_file \\
            \"https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/symbol-properties/symbol-properties-database.csv\" \\
            \"data/symbol-properties/symbol-properties-database.csv\"; then
            log_warn \"Failed to download symbol properties database\"
        else
            log_success \"Symbol properties database downloaded\"
        fi
    fi
}

# Generic file download function with retry logic
download_file() {
    local url=\"\$1\"
    local destination=\"\$2\"
    local max_retries=3
    local retry_count=0
    
    while [[ \$retry_count -lt \$max_retries ]]; do
        if command -v curl >/dev/null 2>&1; then
            if curl -L -f -s --connect-timeout 30 --max-time 300 -o \"\$destination\" \"\$url\"; then
                return 0
            fi
        elif command -v wget >/dev/null 2>&1; then
            if wget -q --timeout=300 -O \"\$destination\" \"\$url\"; then
                return 0
            fi
        else
            log_error \"Neither curl nor wget available for downloading files\"
            return 1
        fi
        
        ((retry_count++))
        if [[ \$retry_count -lt \$max_retries ]]; then
            log_warn \"Download attempt \$retry_count failed, retrying in 5 seconds...\"
            sleep 5
        fi
    done
    
    return 1
}

# Create minimal market hours database if download fails
create_minimal_market_hours() {
    cat > \"data/market-hours/market-hours-database.json\" << 'EOF'
{
  \"entries\": {
    \"USA\": {
      \"market\": \"usa\",
      \"dataTimeZone\": \"America/New_York\",
      \"exchangeTimeZone\": \"America/New_York\",
      \"sunday\": [],
      \"monday\": [{ \"start\": \"09:30:00\", \"end\": \"16:00:00\" }],
      \"tuesday\": [{ \"start\": \"09:30:00\", \"end\": \"16:00:00\" }],
      \"wednesday\": [{ \"start\": \"09:30:00\", \"end\": \"16:00:00\" }],
      \"thursday\": [{ \"start\": \"09:30:00\", \"end\": \"16:00:00\" }],
      \"friday\": [{ \"start\": \"09:30:00\", \"end\": \"16:00:00\" }],
      \"saturday\": [],
      \"holidays\": [],
      \"earlyCloses\": []
    }
  }
}
EOF
    log_success \"Created minimal market hours database\"
}

# Function to check system requirements
check_system_requirements() {
    log_info \"Checking system requirements...\"
    
    # Check .NET availability
    if ! command -v dotnet >/dev/null 2>&1; then
        log_error \".NET SDK not found. Please install .NET 6.0 or later.\"
        return 1
    fi
    
    local dotnet_version
    dotnet_version=\$(dotnet --version 2>/dev/null || echo \"unknown\")
    log_success \".NET SDK found: \$dotnet_version\"
    
    # Check required files
    if [[ ! -f \"./bin/Alaris.Lean.dll\" ]]; then
        log_error \"Alaris.Lean.dll not found. Please build the project first:\"
        log_error \"  cmake --build .\"
        return 1
    fi
    
    log_success \"Required files found\"
    return 0
}

# Production download mode with comprehensive validation
run_production_download() {
    log_info \"=== Starting Production Data Download ===\"
    
    # System requirements check
    if ! check_system_requirements; then
        log_error \"System requirements not met\"
        return 1
    fi
    
    # Validate credentials
    if ! validate_qc_credentials; then
        log_error \"QuantConnect API credentials validation failed\"
        show_credential_setup
        return 1
    fi
    
    local user_id=\"\${QC_USER_ID}\"
    local token_preview=\"\${QC_API_TOKEN:0:8}***\"
    log_success \"Credentials validated - User ID: \$user_id, Token: \$token_preview\"
    
    # Test network connectivity
    if ! test_network_connectivity; then
        log_error \"Network connectivity issues detected\"
        log_error \"Please check your internet connection and firewall settings\"
        return 1
    fi
    
    # Setup data environment
    setup_lean_data_environment
    
    # Configure environment variables for the download process
    export QC_USER_ID=\"\$user_id\"
    export QC_API_TOKEN=\"\$QC_API_TOKEN\"
    
    # Set optional configuration from environment
    export ALARIS_DOWNLOAD_SYMBOLS=\"\${ALARIS_DOWNLOAD_SYMBOLS:-}\"
    export ALARIS_DOWNLOAD_RESOLUTION=\"\${ALARIS_DOWNLOAD_RESOLUTION:-Daily}\"
    export ALARIS_DOWNLOAD_OPTIONS=\"\${ALARIS_DOWNLOAD_OPTIONS:-true}\"
    export ALARIS_DATA_START_DATE=\"\${ALARIS_DATA_START_DATE:-2018-01-01}\"
    export ALARIS_DATA_END_DATE=\"\${ALARIS_DATA_END_DATE:-}\"
    
    # Create download log
    local download_log=\"logs/production_download_\$(date +%Y%m%d_%H%M%S).log\"
    
    log_info \"Starting QuantConnect API data download...\"
    log_info \"This may take 15-45 minutes depending on data volume and connection speed\"
    log_info \"Log file: \$download_log\"
    log_info \"\"
    log_info \"Download configuration:\"
    log_info \"  Symbols: \${ALARIS_DOWNLOAD_SYMBOLS:-'Production defaults (40+ symbols)'}\"
    log_info \"  Resolution: \${ALARIS_DOWNLOAD_RESOLUTION}\"
    log_info \"  Include Options: \${ALARIS_DOWNLOAD_OPTIONS}\"
    log_info \"  Date Range: \${ALARIS_DATA_START_DATE} to \${ALARIS_DATA_END_DATE:-'latest'}\"
    log_info \"\"
    
    # Build dotnet command with appropriate flags
    local dotnet_args=(
        \"./bin/Alaris.Lean.dll\"
        \"--mode\" \"download\"
    )
    
    # Add debug/verbose flags if specified
    if [[ \"\$DEBUG_FLAG\" == \"--debug\" ]]; then
        dotnet_args+=(\"--debug\")
    fi
    
    if [[ \"\$VERBOSE_FLAG\" == \"--verbose\" ]]; then
        dotnet_args+=(\"--verbose\")
    fi
    
    # Execute download with timeout
    log_info \"Executing download command...\"
    if timeout 3600 dotnet \"\${dotnet_args[@]}\" 2>&1 | tee \"\$download_log\"; then
        log_success \"Download process completed!\"
        analyze_download_results \"\$download_log\"
    else
        local exit_code=\$?
        if [[ \$exit_code -eq 124 ]]; then
            log_error \"Download timed out after 60 minutes\"
        else
            log_error \"Download process failed with exit code: \$exit_code\"
        fi
        log_error \"Check the download log for details: \$download_log\"
        return 1
    fi
}

# Analyze download results and provide feedback
analyze_download_results() {
    local log_file=\"\$1\"
    
    log_info \"Analyzing download results...\"
    
    # Count data files
    local data_files
    data_files=\$(find data/equity/usa -name \"*.zip\" -o -name \"*.csv\" 2>/dev/null | wc -l)
    
    local option_files
    option_files=\$(find data/option/usa -name \"*.zip\" -o -name \"*.csv\" 2>/dev/null | wc -l)
    
    # Check for successful completion indicators in log
    local symbols_with_data
    symbols_with_data=\$(grep -c \"Successfully added equity:\" \"\$log_file\" 2>/dev/null || echo \"0\")
    
    local symbols_with_options
    symbols_with_options=\$(grep -c \"Successfully added options for:\" \"\$log_file\" 2>/dev/null || echo \"0\")
    
    # Check for errors
    local error_count
    error_count=\$(grep -c \"ERROR\\|Failed to add symbol\" \"\$log_file\" 2>/dev/null || echo \"0\")
    
    log_info \"\"
    log_info \"=== Download Results Analysis ===\"
    log_info \"Data files found:\"
    log_info \"  Equity data files: \$data_files\"
    log_info \"  Option data files: \$option_files\"
    log_info \"  Total data files: \$((data_files + option_files))\"
    log_info \"\"
    log_info \"Symbol processing:\"
    log_info \"  Symbols with equity data: \$symbols_with_data\"
    log_info \"  Symbols with options data: \$symbols_with_options\"
    log_info \"  Errors encountered: \$error_count\"
    log_info \"=================================\"
    log_info \"\"
    
    if [[ \$data_files -gt 0 ]]; then
        log_success \"✓ Download completed successfully!\"
        log_info \"Next steps:\"
        log_info \"  1. Run backtest: './start-alaris.sh backtest'\"
        log_info \"  2. Check data directory: ls -la data/equity/usa/\"
        log_info \"  3. Start paper trading: './start-alaris.sh paper'\"
    else
        log_warn \"⚠ No data files found after download\"
        log_info \"Possible issues:\"
        log_info \"  - API authentication problems\"
        log_info \"  - Insufficient account balance\"
        log_info \"  - Data subscription required\"
        log_info \"  - Network connectivity issues\"
        log_info \"\"
        log_info \"Check the log file for detailed error information:\"
        log_info \"  tail -50 \$log_file\"
    fi
}

# Get IBKR host configuration
IBKR_HOST=\$(grep \"host:\" ../config/lean_process.yaml | awk '{print \$2}' | tr -d '\"' 2>/dev/null || echo \"127.0.0.1\")

echo \"Starting Alaris Trading System in \$MODE mode...\"
echo \"IBKR Host: \$IBKR_HOST\"

# Main mode handling
case \"\$MODE\" in
    \"download\")
        run_production_download
        exit \$?
        ;;
        
    \"backtest\")
        log_info \"=== Backtest Mode ===\"
        setup_lean_data_environment
        
        # Check for data availability
        data_count=\$(find data/equity/usa -name \"*.csv\" -o -name \"*.zip\" 2>/dev/null | wc -l)
        if [[ \$data_count -lt 1 ]]; then
            log_warn \"No historical data found for backtesting\"
            log_info \"\"
            log_info \"Options:\"
            log_info \"  1. Download data: './start-alaris.sh download'\"
            log_info \"  2. Use paper trading: './start-alaris.sh paper'\"
            log_info \"\"
            read -p \"Download data now? (y/N): \" -r
            if [[ \$REPLY =~ ^[Yy]\$ ]]; then
                exec \"\$0\" download \"\$DEBUG_FLAG\" \"\$VERBOSE_FLAG\"
            else
                log_info \"Backtest cancelled. Please download data first.\"
                exit 1
            fi
        fi
        
        log_info \"Found \$data_count data files for backtesting\"
        ;;
        
    \"paper\"|\"live\")
        log_info \"=== \$(echo \$MODE | tr '[:lower:]' '[:upper:]') Trading Mode ===\"
        setup_lean_data_environment
        ;;
        
    *)
        echo \"Usage: \$0 {download|backtest|paper|live} [--debug] [--verbose]\"
        echo \"\"
        echo \"Modes:\"
        echo \"  download   - Download historical data from QuantConnect API\"
        echo \"  backtest   - Run strategy backtest on historical data\"
        echo \"  paper      - Forward test with paper trading account\"
        echo \"  live       - Live trading with real money (use with caution)\"
        echo \"\"
        echo \"Flags:\"
        echo \"  --debug    - Enable maximum debugging output\"
        echo \"  --verbose  - Enable verbose logging\"
        echo \"\"
        echo \"Data Download Setup:\"
        echo \"  1. Get QuantConnect API credentials: https://www.quantconnect.com\"
        echo \"  2. Set environment variables:\"
        echo \"     export QC_USER_ID='your-user-id'\"
        echo \"     export QC_API_TOKEN='your-api-token'\"
        echo \"  3. Run: './start-alaris.sh download'\"
        echo \"\"
        echo \"Optional Configuration:\"
        echo \"  export ALARIS_DOWNLOAD_SYMBOLS='SPY,QQQ,AAPL'  # Custom symbol list\"
        echo \"  export ALARIS_DOWNLOAD_RESOLUTION='Minute'     # Resolution (Daily/Hour/Minute)\"
        echo \"  export ALARIS_DOWNLOAD_OPTIONS='true'          # Include options data\"
        echo \"  export ALARIS_DATA_START_DATE='2020-01-01'     # Custom start date\"
        echo \"\"
        exit 1
        ;;
esac

# Rest of the script for trading modes (non-download)
if [[ \"\$MODE\" != \"download\" ]]; then
    # Check for required executables
    if [[ ! -f \"./bin/quantlib-process\" ]]; then
        log_error \"quantlib-process not found. Run 'cmake --build .' first.\"
        exit 1
    fi

    if [[ ! -f \"./bin/Alaris.Lean.dll\" ]]; then
        log_error \"Lean process not found. Ensure .NET build completed successfully.\"
        exit 1
    fi

    # Test IBKR connectivity for trading modes
    if [[ \"\$MODE\" == \"paper\" || \"\$MODE\" == \"live\" ]]; then
        log_info \"Testing IBKR connectivity...\"
        port=\$([[ \"\$MODE\" == \"paper\" ]] && echo \"4002\" || echo \"4001\")
        
        if ! timeout 5 bash -c \"cat < /dev/null > /dev/tcp/\$IBKR_HOST/\$port\" 2>/dev/null; then
            log_warn \"Cannot connect to IBKR on \$IBKR_HOST:\$port\"
            log_warn \"Please ensure IB Gateway/TWS is running and configured properly.\"
            echo \"\"
            read -p \"Continue anyway? (y/N): \" -r
            if [[ ! \$REPLY =~ ^[Yy]\$ ]]; then
                exit 1
            fi
        else
            log_success \"IBKR connection available\"
        fi
    fi

    # Clean shared memory
    log_info \"Cleaning shared memory...\"
    sudo rm -f /dev/shm/alaris_* 2>/dev/null || rm -f /dev/shm/alaris_* 2>/dev/null || true

    # Cleanup function
    cleanup() {
        log_info \"Shutting down Alaris...\"
        [[ -n \$QUANTLIB_PID ]] && kill \$QUANTLIB_PID 2>/dev/null || true
        [[ -n \$LEAN_PID ]] && kill \$LEAN_PID 2>/dev/null || true
        sudo rm -f /dev/shm/alaris_* 2>/dev/null || rm -f /dev/shm/alaris_* 2>/dev/null || true
        log_info \"Cleanup complete.\"
    }
    trap cleanup EXIT INT TERM

    # Start QuantLib process
    log_info \"Starting QuantLib process...\"
    ./bin/quantlib-process ../config/quantlib_process.yaml &
    QUANTLIB_PID=\$!
    log_info \"QuantLib PID: \$QUANTLIB_PID\"
    sleep 3

    # Build Lean command
    lean_args=(\"./bin/Alaris.Lean.dll\" \"--mode\" \"\$MODE\")
    
    if [[ \"\$DEBUG_FLAG\" == \"--debug\" ]]; then
        lean_args+=(\"--debug\")
    fi
    
    if [[ \"\$VERBOSE_FLAG\" == \"--verbose\" ]]; then
        lean_args+=(\"--verbose\")
    fi

    # Start Lean process
    log_info \"Starting Lean process...\"
    dotnet \"\${lean_args[@]}\" &
    LEAN_PID=\$!
    log_info \"Lean PID: \$LEAN_PID\"

    log_success \"\"
    log_success \"Alaris Trading System started successfully!\"
    log_info \"Mode: \$MODE\"
    log_info \"QuantLib PID: \$QUANTLIB_PID\" 
    log_info \"Lean PID: \$LEAN_PID\"
    log_info \"\"
    log_info \"Press Ctrl+C to stop...\"

    wait \$LEAN_PID
fi
")
    
    set(STARTUP_SCRIPT "${CMAKE_BINARY_DIR}/start-alaris.sh")
    file(WRITE "${STARTUP_SCRIPT}" "${STARTUP_CONTENT}")
    execute_process(COMMAND chmod +x "${STARTUP_SCRIPT}" ERROR_QUIET)
    set(ALARIS_STARTUP_SCRIPT "${STARTUP_SCRIPT}" CACHE INTERNAL "")
    message(STATUS "Created production startup script: ${STARTUP_SCRIPT}")
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
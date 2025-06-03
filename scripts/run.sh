#!/bin/bash

# Configuration
WINDOWS_HOST=$(cat /etc/resolv.conf 2>/dev/null | grep nameserver | awk '{print $2}' || echo "192.168.1.1")
WSL_IP=$(ip addr show eth0 2>/dev/null | grep "inet\b" | awk '{print $2}' | cut -d/ -f1 || echo "127.0.0.1")
IB_GATEWAY_HOSTS=("host.docker.internal" "localhost" "127.0.0.1" "$WINDOWS_HOST")  
IB_GATEWAY_PORT="4002"
QUANTLIB_CONFIG_FILE="config/quantlib_process.yaml"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Helper functions
log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_step() { echo -e "${BLUE}[STEP]${NC} $1"; }

# Default values
SYMBOL="SPY"
MODE="backtest"
STRATEGY="deltaneutral"
START_DATE="2023-01-01"
END_DATE="2023-01-02"
FREQUENCY="minute"
DEBUG="false"
QUANTLIB_PROCESS_PID=""

# Help message
show_help() {
    echo "Usage: $0 [options]"
    echo ""
    echo "Alaris Integrated Trading System (QuantLib + Lean)"
    echo ""
    echo "Options:"
    echo "  -s, --symbol SYMBOL     Trading symbol (default: SPY)"
    echo "  -m, --mode MODE         Trading mode: live, paper, or backtest (default: backtest)"
    echo "  -t, --strategy STRAT    Strategy mode (default: deltaneutral)"
    echo "  -sd, --start-date DATE  Backtest start date (YYYY-MM-DD, default: 2023-01-01)"
    echo "  -ed, --end-date DATE    Backtest end date (YYYY-MM-DD, default: 2023-01-02)"
    echo "  -f, --frequency FREQ    Data frequency: minute, hour, or daily (default: minute)"
    echo "  -d, --debug            Enable debug logging"
    echo "  -h, --help              Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                                    # Backtest SPY with defaults"
    echo "  $0 -s AAPL -sd 2024-01-01 -ed 2024-12-31 -d"
    echo "  $0 -m live -s SPY -t gammascalping   # Live trading"
    echo ""
    echo "Architecture:"
    echo "  QuantLib Process (C++) - Pricing, volatility models, strategy logic"
    echo "  Lean Process (C#)      - Market data, order execution, risk management"
    echo "  Communication          - Shared memory ring buffers"
    exit 0
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -s|--symbol)
            SYMBOL="$2"
            shift 2
            ;;
        -m|--mode)
            MODE="$2"
            shift 2
            ;;
        -t|--strategy)
            STRATEGY="$2"
            shift 2
            ;;
        -sd|--start-date)
            START_DATE="$2"
            shift 2
            ;;
        -ed|--end-date)
            END_DATE="$2"
            shift 2
            ;;
        -f|--frequency)
            FREQUENCY="$2"
            shift 2
            ;;
        -d|--debug)
            DEBUG="true"
            shift
            ;;
        -h|--help)
            show_help
            ;;
        *)
            echo "Unknown option: $1"
            show_help
            ;;
    esac
done

# Validate inputs
validate_inputs() {
    # Validate mode
    if [[ ! "$MODE" =~ ^(live|paper|backtest)$ ]]; then
        log_error "Invalid mode: $MODE. Must be one of: live, paper, backtest"
        exit 1
    fi

    # Validate strategy
    if [[ ! "$STRATEGY" =~ ^(deltaneutral|gammascalping|volatilitytiming|relativevalue)$ ]]; then
        log_error "Invalid strategy: $STRATEGY"
        exit 1
    fi

    # Validate frequency
    if [[ ! "$FREQUENCY" =~ ^(minute|hour|daily)$ ]]; then
        log_error "Invalid frequency: $FREQUENCY"
        exit 1
    fi

    # Validate dates for backtest mode
    if [[ "$MODE" == "backtest" ]]; then
        date_regex="^[0-9]{4}-[0-9]{2}-[0-9]{2}$"
        if [[ ! "$START_DATE" =~ $date_regex ]] || [[ ! "$END_DATE" =~ $date_regex ]]; then
            log_error "Dates must be in YYYY-MM-DD format"
            exit 1
        fi
        
        # Validate date range
        if [[ "$START_DATE" > "$END_DATE" ]]; then
            log_error "Start date must be before end date"
            exit 1
        fi
    fi
}

# Check prerequisites
check_prerequisites() {
    log_step "Checking prerequisites..."
    
    # Check if lean.json exists
    if [[ ! -f "lean.json" ]]; then
        log_error "lean.json configuration file not found"
        log_info "Run: ./scripts/setup.sh to create it"
        exit 1
    fi

    # Check if data files exist
    if [[ ! -f "data/market-hours/market-hours-database.json" ]]; then
        log_warn "Market hours database not found"
        log_info "Run: ./scripts/setup.sh to download required files"
    fi

    # Check if C# project exists
    if [[ ! -f "src/csharp/Alaris.Lean.csproj" ]]; then
        log_error "C# project file not found: src/csharp/Alaris.Lean.csproj"
        exit 1
    fi

    # Check if QuantLib process is available
    if [[ ! -f "build/bin/quantlib-process" && ! -f "build/quantlib-process" ]]; then
        log_error "QuantLib process not found. Run: ./scripts/build.sh first"
        exit 1
    fi
    
    if [[ ! -f "$QUANTLIB_CONFIG_FILE" ]]; then
        log_warn "QuantLib config not found: $QUANTLIB_CONFIG_FILE"
        log_info "Using default configuration"
    fi

    log_info "✓ Prerequisites check passed"
}

# Start QuantLib process (MUST start first to create shared memory)
start_quantlib_process() {
    log_step "Starting QuantLib process (pricing & strategy engine)..."
    
    # Find the quantlib-process executable
    local quantlib_exe=""
    if [[ -f "build/bin/quantlib-process" ]]; then
        quantlib_exe="build/bin/quantlib-process"
    elif [[ -f "build/quantlib-process" ]]; then
        quantlib_exe="build/quantlib-process"
    else
        log_error "QuantLib process executable not found"
        exit 1
    fi
    
    # Create QuantLib config if it doesn't exist
    if [[ ! -f "$QUANTLIB_CONFIG_FILE" ]]; then
        log_info "Creating default QuantLib configuration..."
        mkdir -p "$(dirname "$QUANTLIB_CONFIG_FILE")"
        
        cat > "$QUANTLIB_CONFIG_FILE" << 'EOF'
process:
  priority: 80
  cpu_affinity: [2, 3]
  memory_lock: true
  start_trading_enabled: false

memory:
  pool_size_mb: 32

logging:
  file: "/tmp/alaris_quantlib.log"
  binary_mode: false

pricing:
  alo_engine:
    scheme: "accurate"

strategy:
  vol_arbitrage:
    entry_threshold: 0.05
    exit_threshold: 0.02
    confidence_threshold: 0.7
    max_position_size: 0.05
    risk_limit: 0.10
    model_selection: "ensemble"
EOF
        log_info "✓ Default QuantLib config created"
    fi
    
    # Start QuantLib process in background
    log_info "Launching QuantLib process..."
    "$quantlib_exe" "$QUANTLIB_CONFIG_FILE" &
    QUANTLIB_PROCESS_PID=$!
    
    # Wait for QuantLib to initialize and create shared memory
    log_info "Waiting for QuantLib process to initialize shared memory..."
    sleep 5
    
    # Check if QuantLib is still running
    if ! kill -0 $QUANTLIB_PROCESS_PID 2>/dev/null; then
        log_error "QuantLib process failed to start - check logs"
        log_error "Log file should be at: /tmp/alaris_quantlib.log"
        exit 1
    fi
    
    # Verify shared memory was created (Linux specific check)
    if [[ -d "/dev/shm" ]]; then
        local shm_files=$(ls /dev/shm/ 2>/dev/null | grep -c "alaris" || echo "0")
        if [[ $shm_files -gt 0 ]]; then
            log_info "✓ QuantLib process started successfully (PID: $QUANTLIB_PROCESS_PID)"
            log_info "✓ Shared memory buffers created ($shm_files files)"
        else
            log_warn "QuantLib started but shared memory files not detected"
        fi
    else
        log_info "✓ QuantLib process started successfully (PID: $QUANTLIB_PROCESS_PID)"
    fi
}

# Check IB Gateway connection for live/paper trading
check_ib_gateway() {
    if [[ "$MODE" == "live" || "$MODE" == "paper" ]]; then
        log_step "Checking IB Gateway connection..."
        
        echo "=== IB Gateway Connection Diagnostics ==="
        log_info "Windows Host IP: $WINDOWS_HOST"
        log_info "WSL IP: $WSL_IP"
        log_info "Target Port: $IB_GATEWAY_PORT"
        echo

        # Try to connect to IB Gateway
        IB_GATEWAY_CONNECTED=false
        for host in "${IB_GATEWAY_HOSTS[@]}"; do
            log_info "Testing connection to $host:$IB_GATEWAY_PORT..."
            if timeout 5 nc -zv "$host" "$IB_GATEWAY_PORT" 2>/dev/null; then
                IB_GATEWAY_HOST=$host
                IB_GATEWAY_CONNECTED=true
                log_info "✅ Connected to IB Gateway at $host:$IB_GATEWAY_PORT"
                break
            else
                log_warn "❌ Connection failed to $host:$IB_GATEWAY_PORT"
            fi
        done

        if [[ "$IB_GATEWAY_CONNECTED" = false ]]; then
            log_error "Could not connect to IB Gateway"
            echo
            log_warn "Troubleshooting Steps:"
            echo "1. Verify IB Gateway is running on Windows"
            echo "2. Check IB Gateway API Settings:"
            echo "   - Enable ActiveX and Socket Clients"
            echo "   - Socket port: $IB_GATEWAY_PORT"
            echo "   - Trusted IP addresses: 127.0.0.1, host.docker.internal"
            echo "3. Restart IB Gateway after making changes"
            exit 1
        fi
    fi
}

# Cleanup function
cleanup() {
    echo ""
    log_step "Shutting down Alaris system..."
    
    if [[ -n "$QUANTLIB_PROCESS_PID" ]]; then
        log_info "Stopping QuantLib process (PID: $QUANTLIB_PROCESS_PID)"
        
        # Send SIGTERM first for graceful shutdown
        kill -TERM "$QUANTLIB_PROCESS_PID" 2>/dev/null || true
        
        # Wait up to 10 seconds for graceful shutdown
        local count=0
        while kill -0 "$QUANTLIB_PROCESS_PID" 2>/dev/null && [[ $count -lt 10 ]]; do
            sleep 1
            ((count++))
        done
        
        # Force kill if still running
        if kill -0 "$QUANTLIB_PROCESS_PID" 2>/dev/null; then
            log_warn "Force killing QuantLib process"
            kill -KILL "$QUANTLIB_PROCESS_PID" 2>/dev/null || true
        fi
        
        wait "$QUANTLIB_PROCESS_PID" 2>/dev/null || true
        log_info "✓ QuantLib process stopped"
    fi
    
    # Clean up shared memory files (Linux)
    if [[ -d "/dev/shm" ]]; then
        local cleaned=$(ls /dev/shm/ 2>/dev/null | grep "alaris" | wc -l || echo "0")
        if [[ $cleaned -gt 0 ]]; then
            rm -f /dev/shm/alaris_* 2>/dev/null || true
            log_info "✓ Cleaned up $cleaned shared memory files"
        fi
    fi
    
    log_info "Alaris system shutdown complete"
    exit 0
}

# Set up cleanup on script exit
trap cleanup INT TERM

# Main execution
main() {
    echo "========================================"
    echo "    Alaris Integrated Trading System"
    echo "      QuantLib + QuantConnect Lean"
    echo "========================================"
    echo ""
    
    validate_inputs
    check_prerequisites
    
    # Print configuration
    log_step "Starting Alaris with configuration:"
    echo "  Symbol: $SYMBOL"
    echo "  Mode: $MODE"
    echo "  Strategy: $STRATEGY"
    echo "  Frequency: $FREQUENCY"
    echo "  Debug: $DEBUG"
    if [[ "$MODE" == "backtest" ]]; then
        echo "  Start date: $START_DATE"
        echo "  End date: $END_DATE"
    fi
    echo ""
    
    # For live/paper trading, check IB Gateway first
    if [[ "$MODE" == "live" || "$MODE" == "paper" ]]; then
        check_ib_gateway
    fi
    
    # CRITICAL: Start QuantLib process FIRST (creates shared memory)
    start_quantlib_process
    
    # Build the dotnet command for Lean process
    log_step "Starting Lean process (market data & execution engine)..."

    # Use the new output path for Lean launcher
    local lean_launcher_path="build/external/lean/release/QuantConnect.Lean.Launcher.dll"
    if [[ ! -f "$lean_launcher_path" ]]; then
        log_error "Lean launcher not found at $lean_launcher_path"
        log_error "You may need to run ./scripts/build.sh first."
        cleanup
        exit 1
    fi

    CMD="dotnet $lean_launcher_path"
    CMD="$CMD --symbol $SYMBOL"
    CMD="$CMD --mode $MODE"
    CMD="$CMD --strategy $STRATEGY"
    CMD="$CMD --frequency $FREQUENCY"

    if [[ "$DEBUG" == "true" ]]; then
        CMD="$CMD --debug"
    fi

    if [[ "$MODE" == "backtest" ]]; then
        CMD="$CMD --start-date $START_DATE"
        CMD="$CMD --end-date $END_DATE"
    fi

    # Execute the Lean process
    log_info "Executing: $CMD"
    echo ""

    if eval "$CMD"; then
        log_info "✓ Alaris completed successfully"
    else
        log_error "Alaris execution failed"
        cleanup
        exit 1
    fi
    
    # For live/paper trading, wait for user interrupt
    if [[ "$MODE" == "live" || "$MODE" == "paper" ]]; then
        log_info "Live/Paper trading mode - Press Ctrl+C to stop..."
        echo ""
        log_info "System Status:"
        log_info "  QuantLib Process: Running (PID: $QUANTLIB_PROCESS_PID)"
        log_info "  Lean Process: Connected"
        log_info "  IB Gateway: Connected to $IB_GATEWAY_HOST:$IB_GATEWAY_PORT"
        echo ""
        
        # Wait for QuantLib process or user interrupt
        while kill -0 "$QUANTLIB_PROCESS_PID" 2>/dev/null; do
            sleep 1
        done
        
        log_warn "QuantLib process has stopped unexpectedly"
    fi
    
    cleanup
}

# Run main function
main "$@"
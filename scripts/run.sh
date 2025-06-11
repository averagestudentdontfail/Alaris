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
    exit 0
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -s|--symbol) SYMBOL="$2"; shift 2;;
        -m|--mode) MODE="$2"; shift 2;;
        -t|--strategy) STRATEGY="$2"; shift 2;;
        -sd|--start-date) START_DATE="$2"; shift 2;;
        -ed|--end-date) END_DATE="$2"; shift 2;;
        -f|--frequency) FREQUENCY="$2"; shift 2;;
        -d|--debug) DEBUG="true"; shift;;
        -h|--help) show_help;;
        *) echo "Unknown option: $1"; show_help; exit 1;;
    esac
done

# Validate inputs
validate_inputs() {
    if [[ ! "$MODE" =~ ^(live|paper|backtest)$ ]]; then
        log_error "Invalid mode: $MODE. Must be one of: live, paper, backtest"
        exit 1
    fi
}

# Check prerequisites
check_prerequisites() {
    log_step "Checking prerequisites..."
    if [[ ! -f "build/bin/quantlib-process" ]]; then
        log_error "QuantLib process not found. Run: ./scripts/build.sh first"
        exit 1
    fi
    # UPDATED: Check for your custom C# launcher
    if [[ ! -f "build/csharp/Alaris.Lean.dll" ]]; then
        log_error "Alaris Lean launcher not found. Run: ./scripts/build.sh first"
        exit 1
    fi
    log_info "✓ Prerequisites check passed"
}

# Start QuantLib process
start_quantlib_process() {
    log_step "Starting QuantLib process (pricing & strategy engine)..."
    build/bin/quantlib-process "$QUANTLIB_CONFIG_FILE" &
    QUANTLIB_PROCESS_PID=$!
    log_info "Waiting for QuantLib process to initialize shared memory..."
    sleep 5
    if ! kill -0 $QUANTLIB_PROCESS_PID 2>/dev/null; then
        log_error "QuantLib process failed to start - check logs"
        exit 1
    fi
    log_info "✓ QuantLib process started successfully (PID: $QUANTLIB_PROCESS_PID)"
}

# Cleanup function
cleanup() {
    echo ""
    log_step "Shutting down Alaris system..."
    if [[ -n "$QUANTLIB_PROCESS_PID" ]]; then
        log_info "Stopping QuantLib process (PID: $QUANTLIB_PROCESS_PID)"
        kill -TERM "$QUANTLIB_PROCESS_PID" 2>/dev/null || true
        wait "$QUANTLIB_PROCESS_PID" 2>/dev/null
        log_info "✓ QuantLib process stopped"
    fi
    if [[ -d "/dev/shm" ]]; then
        rm -f /dev/shm/alaris_* 2>/dev/null || true
    fi
    log_info "Alaris system shutdown complete"
    exit 0
}

trap cleanup INT TERM

# Main execution
main() {
    echo "========================================"
    echo "    Alaris Integrated Trading System"
    echo "========================================"
    echo ""
    
    validate_inputs
    check_prerequisites
    
    # Print configuration
    log_step "Starting Alaris with configuration:"
    echo "  Symbol: $SYMBOL, Mode: $MODE, Strategy: $STRATEGY"
    if [[ "$MODE" == "backtest" ]]; then
        echo "  Period: $START_DATE to $END_DATE, Frequency: $FREQUENCY"
    fi
    echo ""
    
    start_quantlib_process
    
    log_step "Starting Lean process (market data & execution engine)..."

    # --- CORRECTED LAUNCHER PATH ---
    # Point to your custom Alaris Lean executable, not the generic QuantConnect one
    local lean_launcher_path="build/csharp/Alaris.Lean.dll"

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

    log_info "Executing: $CMD"
    echo ""

    # Execute the Lean process
    if eval "$CMD"; then
        log_info "✓ Alaris completed successfully"
    else
        log_error "Alaris execution failed"
        cleanup
        exit 1
    fi
    
    if [[ "$MODE" == "live" || "$MODE" == "paper" ]]; then
        log_info "Live/Paper trading mode is active. Press Ctrl+C to stop."
        wait "$QUANTLIB_PROCESS_PID"
    fi
    
    cleanup
}

main "$@"

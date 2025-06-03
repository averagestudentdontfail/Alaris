#!/bin/bash

# Configuration
WINDOWS_HOST=$(cat /etc/resolv.conf | grep nameserver | awk '{print $2}')
WSL_IP=$(ip addr show eth0 | grep "inet\b" | awk '{print $2}' | cut -d/ -f1)
# Use host.docker.internal which is WSL2's special DNS name for Windows host
IB_GATEWAY_HOSTS=("host.docker.internal" "localhost" "127.0.0.1" "$WINDOWS_HOST")  
IB_GATEWAY_PORT="4002"
ALARIS_CONFIG_FILE="config/quantlib_process.yaml"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Helper functions
log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }

# Default values
SYMBOL="SPY"
MODE="paper"
STRATEGY="deltaneutral"
START_DATE=""
END_DATE=""
FREQUENCY="minute"  # Default to minute frequency
DEBUG="false"      # Default to no debug logging

# Help message
show_help() {
    echo "Usage: $0 [options]"
    echo "Options:"
    echo "  -s, --symbol SYMBOL     Trading symbol (default: SPY)"
    echo "  -m, --mode MODE         Trading mode: live, paper, or backtest (default: backtest)"
    echo "  -t, --strategy STRAT    Strategy mode: deltaneutral, gammascalping, volatilitytiming, or relativevalue (default: deltaneutral)"
    echo "  -sd, --start-date DATE  Backtest start date (YYYY-MM-DD)"
    echo "  -ed, --end-date DATE    Backtest end date (YYYY-MM-DD)"
    echo "  -f, --frequency FREQ    Data frequency: minute, hour, or daily (default: minute)"
    echo "  -d, --debug            Enable debug logging"
    echo "  -h, --help              Show this help message"
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

# Validate mode
if [[ ! "$MODE" =~ ^(live|paper|backtest)$ ]]; then
    echo "Error: Invalid mode. Must be one of: live, paper, backtest"
    exit 1
fi

# Validate strategy
if [[ ! "$STRATEGY" =~ ^(deltaneutral|gammascalping|volatilitytiming|relativevalue)$ ]]; then
    echo "Error: Invalid strategy. Must be one of: deltaneutral, gammascalping, volatilitytiming, relativevalue"
    exit 1
fi

# Validate dates for backtest mode
if [[ "$MODE" == "backtest" ]]; then
    if [[ -z "$START_DATE" || -z "$END_DATE" ]]; then
        echo "Error: Start date and end date are required for backtest mode"
        exit 1
    fi
    
    # Validate date format
    date_regex="^[0-9]{4}-[0-9]{2}-[0-9]{2}$"
    if [[ ! "$START_DATE" =~ $date_regex ]] || [[ ! "$END_DATE" =~ $date_regex ]]; then
        echo "Error: Dates must be in YYYY-MM-DD format"
        exit 1
    fi
fi

# Validate frequency
if [[ ! "$FREQUENCY" =~ ^(minute|hour|daily)$ ]]; then
    echo "Error: Invalid frequency. Must be one of: minute, hour, daily"
    exit 1
fi

# Print configuration
echo "Starting Alaris with configuration:"
echo "Symbol: $SYMBOL"
echo "Mode: $MODE"
echo "Strategy: $STRATEGY"
echo "Frequency: $FREQUENCY"
echo "Debug: $DEBUG"
if [[ "$MODE" == "backtest" ]]; then
    echo "Start date: $START_DATE"
    echo "End date: $END_DATE"
fi
echo

# For live/paper trading, check IB Gateway connection
if [[ "$MODE" == "live" || "$MODE" == "paper" ]]; then
    # Display connection information
    echo "=== IB Gateway Connection Diagnostics ==="
    log_info "Using WSL2 host.docker.internal for Windows connection"
    log_info "Windows Host IP: $WINDOWS_HOST (fallback)"
    log_info "WSL IP: $WSL_IP"
    log_info "Target Port: $IB_GATEWAY_PORT"
    echo

    # Try to connect to IB Gateway
    IB_GATEWAY_CONNECTED=false
    for host in "${IB_GATEWAY_HOSTS[@]}"; do
        log_info "Attempting connection to $host:$IB_GATEWAY_PORT..."
        if nc -zv -w 5 $host $IB_GATEWAY_PORT 2>/dev/null; then
            IB_GATEWAY_HOST=$host
            IB_GATEWAY_CONNECTED=true
            log_info "✅ Connected to IB Gateway at $host:$IB_GATEWAY_PORT"
            break
        else
            log_warn "❌ Connection failed to $host:$IB_GATEWAY_PORT"
        fi
    done

    if [ "$IB_GATEWAY_CONNECTED" = false ]; then
        log_error "Could not connect to IB Gateway on any of: ${IB_GATEWAY_HOSTS[*]}:$IB_GATEWAY_PORT"
        echo
        log_warn "Troubleshooting Steps:"
        echo "1. Verify IB Gateway is running on Windows"
        echo "2. Check IB Gateway API Settings:"
        echo "   - Enable ActiveX and Socket Clients"
        echo "   - Socket port: $IB_GATEWAY_PORT"
        echo "   - Trusted IP addresses should include:"
        echo "     • 127.0.0.1"
        echo "     • host.docker.internal (WSL2 Windows host)"
        echo "3. Restart IB Gateway after making changes"
        echo
        log_warn "Note: Using host.docker.internal for WSL2 to Windows communication"
        exit 1
    fi

    # Start QuantLib process for live/paper trading
    log_info "Starting quantlib-process..."
    ./build/bin/quantlib-process $ALARIS_CONFIG_FILE &
    QUANTLIB_PID=$!

    # Wait for QuantLib to initialize
    sleep 2

    # Check if QuantLib is still running
    if ! kill -0 $QUANTLIB_PID 2>/dev/null; then
        log_error "quantlib-process failed to start"
        exit 1
    fi

    # Set up cleanup on script exit
    cleanup() {
        echo "Shutting down..."
        kill $QUANTLIB_PID 2>/dev/null || true
        exit 0
    }

    trap cleanup INT TERM
fi

# Build command for the algorithm
CMD="dotnet run --project src/csharp/Alaris.Lean.csproj"

# Add parameters
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
elif [[ "$MODE" == "live" || "$MODE" == "paper" ]]; then
    CMD="$CMD --ib-gateway-host=$IB_GATEWAY_HOST"
    CMD="$CMD --ib-gateway-port=$IB_GATEWAY_PORT"
    CMD="$CMD --market-data-farm=hfarm"
    CMD="$CMD --historical-data-farm=apachmds"
fi

# Run the algorithm
log_info "Executing: $CMD"
eval $CMD

# For live/paper trading, wait for QuantLib process
if [[ "$MODE" == "live" || "$MODE" == "paper" ]]; then
    wait $QUANTLIB_PID
fi

# Update all references to use 'data' and 'results' (lowercase)
#!/bin/bash

# Configuration
IB_GATEWAY_HOSTS=("172.31.16.1")
IB_GATEWAY_PORT="4002"
ALARIS_CONFIG_FILE="config/quantlib_process.yaml"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Helper functions
log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Try to connect to IB Gateway
IB_GATEWAY_CONNECTED=false
for host in "${IB_GATEWAY_HOSTS[@]}"; do
    if nc -z $host $IB_GATEWAY_PORT; then
        IB_GATEWAY_HOST=$host
        IB_GATEWAY_CONNECTED=true
        log_info "Connected to IB Gateway at $host:$IB_GATEWAY_PORT"
        break
    fi
done

if [ "$IB_GATEWAY_CONNECTED" = false ]; then
    log_error "Could not connect to IB Gateway on any of: ${IB_GATEWAY_HOSTS[*]}:$IB_GATEWAY_PORT"
    echo "Please ensure IB Gateway is running and configured for:"
    echo "  - Port: $IB_GATEWAY_PORT"
    echo "  - Market Data Farm: hfarm"
    echo "  - Historical Data Farm: apachmds"
    echo "  - Trusted IP addresses include: 127.0.0.1"
    exit 1
fi

# Start QuantLib process
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

# Start Lean process
log_info "Starting lean-process..."
dotnet run --project src/csharp/Alaris.Lean.csproj -- \
    --ib-gateway-host=$IB_GATEWAY_HOST \
    --ib-gateway-port=$IB_GATEWAY_PORT \
    --market-data-farm=hfarm \
    --historical-data-farm=apachmds &
LEAN_PID=$!

# Set up cleanup on script exit
cleanup() {
    echo "Shutting down..."
    kill $QUANTLIB_PID 2>/dev/null || true
    kill $LEAN_PID 2>/dev/null || true
    exit 0
}

trap cleanup INT TERM

# Wait for processes
log_info "Alaris system is running. Press Ctrl+C to stop."
wait 
#!/bin/bash

# Configuration
IB_GATEWAY_HOST="localhost"
IB_GATEWAY_PORT="4002"
ALARIS_CONFIG_FILE="config/quantlib_process.yaml"

# Colors for output
GREEN='\033[0;32m'
NC='\033[0m' # No Color

# Helper function
log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }

# Check if IB Gateway is running
if ! nc -z $IB_GATEWAY_HOST $IB_GATEWAY_PORT; then
    echo "Error: IB Gateway is not running on $IB_GATEWAY_HOST:$IB_GATEWAY_PORT"
    echo "Please start IB Gateway first and ensure it's configured for:"
    echo "  - Port: $IB_GATEWAY_PORT"
    echo "  - Market Data Farm: hfarm"
    echo "  - Historical Data Farm: apachmds"
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
    echo "Error: quantlib-process failed to start"
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
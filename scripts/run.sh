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
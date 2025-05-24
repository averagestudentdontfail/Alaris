#!/bin/bash
# Alaris Startup Script

set -e

echo "🚀 Starting Alaris Trading System..."

ALARIS_ROOT="/opt/alaris"
CONFIG_DIR="./config"

# Check if running locally or installed
if [ -f "./build/src/quantlib/quantlib_process" ]; then
    QUANTLIB_BINARY="./build/src/quantlib/quantlib_process"
    CONFIG_DIR="./config"
    echo "📍 Running from development directory"
else
    QUANTLIB_BINARY="/opt/alaris/bin/quantlib_process"
    CONFIG_DIR="/opt/alaris/config"
    echo "📍 Running from installed location"
fi

# Check if binary exists
if [ ! -f "$QUANTLIB_BINARY" ]; then
    echo "❌ QuantLib binary not found: $QUANTLIB_BINARY"
    echo "💡 Run ./scripts/build.sh first"
    exit 1
fi

# Check if config exists
if [ ! -f "$CONFIG_DIR/quantlib_process.yaml" ]; then
    echo "❌ Configuration not found: $CONFIG_DIR/quantlib_process.yaml"
    exit 1
fi

echo "🔧 Setting up shared memory..."
sudo mkdir -p /dev/shm/alaris 2>/dev/null || true
sudo chmod 777 /dev/shm/alaris 2>/dev/null || echo "⚠️  Could not set shared memory permissions"

echo "🚀 Starting QuantLib process..."
echo "📋 Process: $QUANTLIB_BINARY"
echo "📋 Config:  $CONFIG_DIR/quantlib_process.yaml"
echo ""

# Start with real-time priority if possible
if [[ $EUID -eq 0 ]]; then
    echo "🔑 Running with root privileges for real-time optimization"
    taskset -c 2,3 nice -n -20 \
        "$QUANTLIB_BINARY" "$CONFIG_DIR/quantlib_process.yaml" &
else
    echo "⚠️  Running without root - real-time optimization limited"
    "$QUANTLIB_BINARY" "$CONFIG_DIR/quantlib_process.yaml" &
fi

QUANTLIB_PID=$!
echo "✅ QuantLib process started with PID: $QUANTLIB_PID"

# Note: Lean process would be started separately in production
echo ""
echo "💡 Next: Start the Lean process separately"
echo "   cd src/csharp && dotnet run"
echo ""
echo "🛑 To stop: kill $QUANTLIB_PID"

# Wait for process
echo "🔄 QuantLib process running. Press Ctrl+C to stop."
trap "kill $QUANTLIB_PID; exit" INT
wait $QUANTLIB_PID

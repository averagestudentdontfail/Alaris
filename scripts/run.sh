#!/bin/bash

# Configuration
IB_GATEWAY_HOST="localhost"
IB_GATEWAY_PORT="4002"
ALARIS_CONFIG_FILE="config/quantlib_process.yaml"

# Check if IB Gateway is running
if ! nc -z $IB_GATEWAY_HOST $IB_GATEWAY_PORT; then
    echo "Error: IB Gateway is not running on $IB_GATEWAY_HOST:$IB_GATEWAY_PORT"
    exit 1
fi

# Start QuantLib process
echo "Starting Alaris QuantLib process..."
./build/bin/quantlib_process $ALARIS_CONFIG_FILE &

# Start Lean process
echo "Starting Alaris Lean process..."
dotnet run --project src/csharp/Alaris.Lean.csproj -- \
    --ib-gateway-host=$IB_GATEWAY_HOST \
    --ib-gateway-port=$IB_GATEWAY_PORT \
    --market-data-farm=hfarm \
    --historical-data-farm=apachmds

# Wait for processes
wait 
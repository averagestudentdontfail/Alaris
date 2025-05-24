#!/bin/bash
# Health check for Alaris QuantLib process

PROCESS_NAME="quantlib_process"
LOG_FILE="/var/log/alaris/quantlib.log"

# Check if process is running
if ! pgrep -f "$PROCESS_NAME" > /dev/null; then
    echo "ERROR: $PROCESS_NAME not running"
    exit 1
fi

# Check shared memory
if [ ! -d "/dev/shm" ]; then
    echo "ERROR: Shared memory not available"
    exit 1
fi

# Check recent log activity (within last 60 seconds)
if [ -f "$LOG_FILE" ]; then
    if [ $(($(date +%s) - $(stat -c %Y "$LOG_FILE" 2>/dev/null || echo 0))) -gt 60 ]; then
        echo "WARNING: Log file not updated recently"
        exit 1
    fi
fi

echo "OK: QuantLib process healthy"
exit 0

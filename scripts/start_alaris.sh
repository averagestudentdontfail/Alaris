#!/bin/bash
# scripts/start_alaris.sh
# Start the Alaris Trading System

set -e

# Colors for output
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly RED='\033[0;31m'
readonly NC='\033[0m'

# Script directory
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Helper functions
log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Default configuration
USE_DOCKER=false
USE_SYSTEMD=false
CONFIG_DIR="$PROJECT_ROOT/config"
BUILD_DIR="$PROJECT_ROOT/build"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --docker)
            USE_DOCKER=true
            shift
            ;;
        --systemd)
            USE_SYSTEMD=true
            shift
            ;;
        --config)
            CONFIG_DIR="$2"
            shift 2
            ;;
        --build-dir)
            BUILD_DIR="$2"
            shift 2
            ;;
        --help|-h)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --docker      Use Docker Compose to start services"
            echo "  --systemd     Use systemd to start services (requires root)"
            echo "  --config DIR  Configuration directory (default: ./config)"
            echo "  --build-dir   Build directory (default: ./build)"
            echo "  --help        Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0                    # Start locally from build directory"
            echo "  $0 --docker           # Start using Docker Compose"
            echo "  $0 --systemd          # Start using systemd services"
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Function to check prerequisites
check_prerequisites() {
    if [[ $USE_DOCKER == true ]]; then
        if ! command -v docker &>/dev/null; then
            log_error "Docker not found. Please install Docker."
            exit 1
        fi
        if ! command -v docker-compose &>/dev/null; then
            log_error "Docker Compose not found. Please install Docker Compose."
            exit 1
        fi
    fi
    
    if [[ $USE_SYSTEMD == true ]]; then
        if [[ $EUID -ne 0 ]]; then
            log_error "Systemd mode requires root privileges. Please run with sudo."
            exit 1
        fi
        if ! command -v systemctl &>/dev/null; then
            log_error "Systemd not found. This option is only available on systemd-based systems."
            exit 1
        fi
    fi
    
    if [[ $USE_DOCKER == false && $USE_SYSTEMD == false ]]; then
        # Check for local binaries
        if [[ ! -f "$BUILD_DIR/bin/quantlib_process" ]]; then
            log_error "QuantLib process not found at $BUILD_DIR/bin/quantlib_process"
            log_error "Please build the project first: ./scripts/build.sh"
            exit 1
        fi
        if [[ ! -f "$CONFIG_DIR/quantlib_process.yaml" ]]; then
            log_error "Configuration not found at $CONFIG_DIR/quantlib_process.yaml"
            exit 1
        fi
    fi
}

# Function to setup shared memory
setup_shared_memory() {
    log_info "Setting up shared memory..."
    
    # Create shared memory directory
    if [[ ! -d /dev/shm/alaris ]]; then
        if [[ $EUID -eq 0 ]]; then
            mkdir -p /dev/shm/alaris
            chmod 777 /dev/shm/alaris
        else
            sudo mkdir -p /dev/shm/alaris
            sudo chmod 777 /dev/shm/alaris
        fi
    fi
    
    # Clean up any existing shared memory segments
    rm -f /dev/shm/alaris_* 2>/dev/null || true
}

# Function to start with Docker
start_docker() {
    log_info "Starting Alaris with Docker Compose..."
    
    cd "$PROJECT_ROOT"
    
    # Build images if needed
    docker-compose build
    
    # Start services
    docker-compose up -d
    
    # Wait for services to be healthy
    log_info "Waiting for services to be healthy..."
    local timeout=60
    local elapsed=0
    
    while [[ $elapsed -lt $timeout ]]; do
        if docker-compose ps | grep -E "(quantlib|lean)" | grep -q "healthy"; then
            log_info "Services are healthy"
            break
        fi
        sleep 2
        elapsed=$((elapsed + 2))
    done
    
    # Show status
    docker-compose ps
    
    log_info ""
    log_info "Services started successfully!"
    log_info ""
    log_info "View logs:"
    log_info "  docker-compose logs -f quantlib-process"
    log_info "  docker-compose logs -f lean-process"
    log_info ""
    log_info "Stop services:"
    log_info "  docker-compose down"
}

# Function to start with systemd
start_systemd() {
    log_info "Starting Alaris with systemd..."
    
    # Start services
    systemctl start alaris-quantlib
    systemctl start alaris-lean
    
    # Check status
    sleep 2
    if systemctl is-active --quiet alaris-quantlib; then
        log_info "QuantLib service started successfully"
    else
        log_error "QuantLib service failed to start"
        systemctl status alaris-quantlib
        exit 1
    fi
    
    if systemctl is-active --quiet alaris-lean; then
        log_info "Lean service started successfully"
    else
        log_warn "Lean service failed to start (may require QuantLib to be fully initialized)"
    fi
    
    log_info ""
    log_info "Services started successfully!"
    log_info ""
    log_info "View logs:"
    log_info "  journalctl -fu alaris-quantlib"
    log_info "  journalctl -fu alaris-lean"
    log_info ""
    log_info "Stop services:"
    log_info "  systemctl stop alaris-quantlib alaris-lean"
}

# Function to start locally
start_local() {
    log_info "Starting Alaris locally..."
    
    setup_shared_memory
    
    # Start QuantLib process
    log_info "Starting QuantLib process..."
    cd "$BUILD_DIR/bin"
    ./quantlib_process "$CONFIG_DIR/quantlib_process.yaml" &
    QUANTLIB_PID=$!
    
    log_info "QuantLib process started with PID: $QUANTLIB_PID"
    
    # Wait for QuantLib to initialize
    sleep 3
    
    # Check if QuantLib is still running
    if ! kill -0 $QUANTLIB_PID 2>/dev/null; then
        log_error "QuantLib process exited unexpectedly"
        exit 1
    fi
    
    # Start Lean process if available
    if [[ -f "$BUILD_DIR/bin/lean/Alaris.Lean.dll" ]]; then
        log_info "Starting Lean process..."
        cd "$BUILD_DIR/bin/lean"
        dotnet Alaris.Lean.dll &
        LEAN_PID=$!
        log_info "Lean process started with PID: $LEAN_PID"
    else
        log_warn "Lean process not found (C# components may not be built)"
        LEAN_PID=""
    fi
    
    log_info ""
    log_info "Alaris Trading System started successfully!"
    log_info ""
    log_info "Processes:"
    log_info "  QuantLib PID: $QUANTLIB_PID"
    [[ -n $LEAN_PID ]] && log_info "  Lean PID: $LEAN_PID"
    log_info ""
    log_info "Monitoring:"
    log_info "  Logs: tail -f $PROJECT_ROOT/logs/*.log"
    log_info "  Shared Memory: ls -la /dev/shm/alaris*"
    log_info ""
    log_info "Press Ctrl+C to stop..."
    
    # Set up signal handler
    trap cleanup INT TERM
    
    # Wait for processes
    wait $QUANTLIB_PID $LEAN_PID
}

# Cleanup function
cleanup() {
    log_info ""
    log_info "Shutting down Alaris..."
    
    if [[ -n $QUANTLIB_PID ]]; then
        kill $QUANTLIB_PID 2>/dev/null || true
    fi
    
    if [[ -n $LEAN_PID ]]; then
        kill $LEAN_PID 2>/dev/null || true
    fi
    
    # Clean up shared memory
    rm -f /dev/shm/alaris_* 2>/dev/null || true
    
    log_info "Alaris stopped"
    exit 0
}

# Main
main() {
    log_info "Starting Alaris Trading System"
    
    check_prerequisites
    
    if [[ $USE_DOCKER == true ]]; then
        start_docker
    elif [[ $USE_SYSTEMD == true ]]; then
        start_systemd
    else
        start_local
    fi
}

# Run main function
main
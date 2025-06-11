#!/bin/bash

set -e  # Exit on any error

# Colors for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly PURPLE='\033[0;35m'
readonly CYAN='\033[0;36m'
readonly NC='\033[0m' # No Color

# Script configuration
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
readonly BUILD_DIR="${PROJECT_ROOT}/build"
readonly EXTERNAL_DIR="${PROJECT_ROOT}/external"

# Default values
BUILD_TYPE="Release"
JOBS=$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 4)
VERBOSE=false
CLEAN_BUILD=false
INSTALL=false
UPDATE_SUBMODULES=false
USE_CCACHE=true
USE_NINJA=false
ENABLE_SANITIZERS=false
ENABLE_COVERAGE=false

# Helper functions
log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Function to show usage
show_usage() {
    cat << EOF
Usage: $0 [OPTIONS]

OPTIONS:
    -t, --type TYPE          Build type (Debug|Release) [default: Release]
    -j, --jobs N             Number of parallel jobs [default: auto-detected]
    -c, --clean              Clean build directory before building
    -i, --install            Install after building
    -u, --update             Update git submodules before building
    -v, --verbose            Enable verbose output
    -n, --ninja              Use Ninja generator instead of Make
    --no-ccache              Disable ccache even if available
    --sanitize               Enable address and undefined behavior sanitizers (Debug builds)
    --coverage               Enable code coverage (Debug builds)
    -h, --help               Show this help message

EXAMPLES:
    $0                       # Build with default settings (Release)
    $0 -t Debug              # Debug build
    $0 -c -j 8               # Clean build with 8 parallel jobs
    $0 --update --install    # Update submodules, build, and install
    $0 --sanitize -t Debug   # Debug build with sanitizers

EOF
}

# Parse command line arguments
parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -t|--type)
                BUILD_TYPE="$2"
                shift 2
                ;;
            -j|--jobs)
                JOBS="$2"
                shift 2
                ;;
            -c|--clean)
                CLEAN_BUILD=true
                shift
                ;;
            -i|--install)
                INSTALL=true
                shift
                ;;
            -u|--update)
                UPDATE_SUBMODULES=true
                shift
                ;;
            -v|--verbose)
                VERBOSE=true
                shift
                ;;
            -n|--ninja)
                USE_NINJA=true
                shift
                ;;
            --no-ccache)
                USE_CCACHE=false
                shift
                ;;
            --sanitize)
                ENABLE_SANITIZERS=true
                shift
                ;;
            --coverage)
                ENABLE_COVERAGE=true
                shift
                ;;
            -h|--help)
                show_usage
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                show_usage
                exit 1
                ;;
        esac
    done

    # Validate build type
    case $BUILD_TYPE in
        Debug|Release)
            ;;
        *)
            log_error "Invalid build type: $BUILD_TYPE"
            exit 1
            ;;
    esac
}

# Function to detect platform
detect_platform() {
    case "$(uname -s)" in
        Linux*)   PLATFORM=Linux;;
        Darwin*)  PLATFORM=Mac;;
        MINGW*)   PLATFORM=Windows;;
        MSYS*)    PLATFORM=Windows;;
        CYGWIN*)  PLATFORM=Windows;;
        *)        PLATFORM="Unknown";;
    esac

    log_info "Detected platform: $PLATFORM"
}

# Function to check system requirements
check_requirements() {
    log_info "Checking build requirements"

    # Check for required tools
    local required_tools=("git" "cmake" "dotnet")

    if [[ $USE_NINJA == true ]]; then
        required_tools+=("ninja")
    else
        required_tools+=("make")
    fi

    for tool in "${required_tools[@]}"; do
        if ! command -v "$tool" &> /dev/null; then
            log_error "$tool is required but not installed"
            exit 1
        fi
    done

    # Check CMake version
    local cmake_version
    cmake_version=$(cmake --version | head -n1 | sed 's/cmake version //')
    log_info "CMake version: $cmake_version"

    if ! cmake --version | head -n1 | grep -qE 'cmake version ([3-9]\.(2[0-9]|[3-9][0-9])|[4-9]\.[0-9]+|[1-9][0-9]+\.[0-9]+)'; then
        log_error "CMake 3.20 or higher is required, found $cmake_version"
        exit 1
    fi

    # Check C++ compiler
    if command -v g++ &> /dev/null; then
        local gcc_version
        gcc_version=$(g++ --version | head -n1)
        log_info "Compiler: $gcc_version"
    elif command -v clang++ &> /dev/null; then
        local clang_version
        clang_version=$(clang++ --version | head -n1)
        log_info "Compiler: $clang_version"
    else
        log_error "No C++ compiler found (g++ or clang++)"
        exit 1
    fi

    # Check for optional tools
    if command -v ccache &> /dev/null && [[ $USE_CCACHE == true ]]; then
        log_info "ccache found: $(ccache --version | head -n1)"
    elif [[ $USE_CCACHE == true ]]; then
        log_warn "ccache not found - builds will be slower"
        USE_CCACHE=false
    fi
}

# Function to update git submodules
update_submodules() {
    if [[ $UPDATE_SUBMODULES == true ]]; then
        log_info "Updating git submodules"
        cd "$PROJECT_ROOT"

        git submodule update --init --recursive
        git submodule update --recursive --remote

        log_info "Submodules updated successfully"
    fi
}

# Function to setup build directory
setup_build_directory() {
    log_info "Setting up build directory"

    if [[ $CLEAN_BUILD == true && -d "$BUILD_DIR" ]]; then
        log_info "Cleaning build directory: $BUILD_DIR"
        rm -rf "$BUILD_DIR"
    fi

    mkdir -p "$BUILD_DIR"
    cd "$BUILD_DIR"
}

# Function to configure CMake
configure_cmake() {
    log_info "Configuring with CMake"

    local cmake_args=(
        "-DCMAKE_BUILD_TYPE=$BUILD_TYPE"
        "-DCMAKE_CXX_STANDARD=20"
        "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON"
    )

    if [[ $USE_NINJA == true ]]; then
        cmake_args+=("-G" "Ninja")
    fi

    if [[ $USE_CCACHE == true ]] && command -v ccache &> /dev/null; then
        cmake_args+=("-DCMAKE_CXX_COMPILER_LAUNCHER=ccache")
    fi

    if [[ $BUILD_TYPE == "Debug" ]]; then
        if [[ $ENABLE_SANITIZERS == true ]]; then
            cmake_args+=("-DENABLE_SANITIZERS=ON")
        fi
        if [[ $ENABLE_COVERAGE == true ]]; then
            cmake_args+=("-DENABLE_COVERAGE=ON")
        fi
    fi

    cmake "${cmake_args[@]}" "$PROJECT_ROOT"
}

# Function to build the project
build_project() {
    log_info "Building project with $JOBS parallel jobs"

    cmake --build . -- -j"$JOBS"
}

# Function to build the Lean .NET engine (submodule)
build_lean_engine() {
    log_info "Building QuantConnect Lean engine submodule (.NET)"
    local lean_solution_path="$EXTERNAL_DIR/lean/QuantConnect.Lean.sln"
    local lean_output_dir="$BUILD_DIR/external/lean/release"
    mkdir -p "$lean_output_dir"

    log_info "Running: dotnet build \"$lean_solution_path\" -c $BUILD_TYPE -o \"$lean_output_dir\""
    if dotnet build "$lean_solution_path" -c "$BUILD_TYPE" -o "$lean_output_dir"; then
        log_info "Lean engine submodule built successfully."
    else
        log_error "Failed to build Lean engine submodule."
        exit 1
    fi
}

# ADDED: Function to build the custom Alaris Lean process
build_alaris_lean_process() {
    log_info "Building Alaris Lean process (.NET)"
    local alaris_project_path="${PROJECT_ROOT}/src/csharp/Alaris.Lean.csproj"
    local alaris_output_dir="${BUILD_DIR}/csharp"
    mkdir -p "$alaris_output_dir"

    if [[ ! -f "$alaris_project_path" ]]; then
        log_error "Alaris C# project not found at: $alaris_project_path"
        exit 1
    fi

    log_info "Running: dotnet build \"$alaris_project_path\" -c $BUILD_TYPE -o \"$alaris_output_dir\""
    if dotnet build "$alaris_project_path" -c "$BUILD_TYPE" -o "$alaris_output_dir"; then
        log_info "Alaris Lean process built successfully. Binaries are in $alaris_output_dir"
    else
        log_error "Failed to build Alaris Lean process."
        exit 1
    fi
}

# Function to install
install_project() {
    if [[ $INSTALL == true ]]; then
        log_info "Installing project"
        cd "$BUILD_DIR"
        cmake --install .
    fi
}

# Function to handle errors
handle_error() {
    log_error "Build script failed on line $1"
    exit 1
}

# Main function
main() {
    trap 'handle_error $LINENO' ERR

    log_info "Alaris Trading System Build Script"
    parse_arguments "$@"
    detect_platform
    check_requirements
    update_submodules
    setup_build_directory
    configure_cmake
    build_project
    
    # ADDED: Build both C# projects
    build_lean_engine
    build_alaris_lean_process

    install_project
    log_info "Build script completed successfully!"
}

main "$@"
```

And here is the corresponding `run.sh` script, which now executes your custom launcher.


```bash
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

main
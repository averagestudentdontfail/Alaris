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
    local required_tools=("git" "cmake")

    if [[ $USE_NINJA == true ]]; then
        required_tools+=("ninja")
    else
        required_tools+=("make")
    fi

    for tool in "${required_tools[@]}"; do
        if ! command -v "$tool" &> /dev/null; then
            log_error "$tool is required but not installed"

            # Provide installation hints
            case $tool in
                cmake)
                    log_error "Install with:"
                    log_error "  Ubuntu/Debian: sudo apt-get install cmake"
                    log_error "  CentOS/RHEL:   sudo yum install cmake"
                    log_error "  macOS:         brew install cmake"
                    log_error "  Windows:       Download from https://cmake.org"
                    ;;
                make)
                    log_error "Install with:"
                    log_error "  Ubuntu/Debian: sudo apt-get install build-essential"
                    log_error "  CentOS/RHEL:   sudo yum groupinstall 'Development Tools'"
                    log_error "  macOS:         xcode-select --install"
                    ;;
                ninja)
                    log_error "Install with:"
                    log_error "  Ubuntu/Debian: sudo apt-get install ninja-build"
                    log_error "  CentOS/RHEL:   sudo yum install ninja-build"
                    log_error "  macOS:         brew install ninja"
                    ;;
                git)
                    log_error "Install with:"
                    log_error "  Ubuntu/Debian: sudo apt-get install git"
                    log_error "  CentOS/RHEL:   sudo yum install git"
                    log_error "  macOS:         brew install git"
                    ;;
            esac
            exit 1
        fi
    done

    # Check CMake version
    local cmake_version
    cmake_version=$(cmake --version | head -n1 | sed 's/cmake version //')
    log_info "CMake version: $cmake_version"

    # Check minimum CMake version (3.20)
    if ! cmake --version | head -n1 | grep -qE 'cmake version ([3-9]\.(2[0-9]|[3-9][0-9])|[4-9]\.[0-9]+|[1-9][0-9]+\.[0-9]+)'; then
        log_error "CMake 3.20 or higher is required, found $cmake_version"
        exit 1
    fi

    # Check C++ compiler
    if command -v g++ &> /dev/null; then
        local gcc_version
        gcc_version=$(g++ --version | head -n1)
        log_info "Compiler: $gcc_version"

        # Check for C++20 support (GCC 10+)
        local gcc_major
        gcc_major=$(g++ -dumpversion | cut -d. -f1)
        if [[ $gcc_major -lt 10 ]]; then
            log_warn "GCC 10+ recommended for full C++20 support, found GCC $gcc_major"
        fi
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

    # Check for Boost libraries
    if [[ $PLATFORM == "Linux" ]]; then
        if ! ldconfig -p | grep -q libboost_system; then
            log_warn "Boost libraries may not be installed or not in ldconfig cache."
            log_warn "Install with: sudo apt-get install libboost-all-dev  # Ubuntu/Debian"
            log_warn "              or: sudo yum install boost-devel       # CentOS/RHEL"
            log_warn "After installation, you might need to run 'sudo ldconfig'."
        fi
    fi
}

# Function to update git submodules
update_submodules() {
    if [[ $UPDATE_SUBMODULES == true ]]; then
        log_info "Updating git submodules"
        cd "$PROJECT_ROOT"

        git submodule update --init --recursive

        # Update to latest versions
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

    # Generator selection
    if [[ $USE_NINJA == true ]]; then
        cmake_args+=("-G" "Ninja")
    fi

    # CCache configuration
    if [[ $USE_CCACHE == true ]] && command -v ccache &> /dev/null; then
        cmake_args+=("-DCMAKE_CXX_COMPILER_LAUNCHER=ccache")
    fi

    # Debug build options
    if [[ $BUILD_TYPE == "Debug" ]]; then
        if [[ $ENABLE_SANITIZERS == true ]]; then
            cmake_args+=("-DENABLE_SANITIZERS=ON")
            log_info "Sanitizers enabled for Debug build."
        fi

        if [[ $ENABLE_COVERAGE == true ]]; then
            cmake_args+=("-DENABLE_COVERAGE=ON")
            log_info "Code coverage enabled for Debug build."
        fi
    elif [[ $ENABLE_SANITIZERS == true || $ENABLE_COVERAGE == true ]]; then
        log_warn "Sanitizers and Code Coverage are typically used with Debug builds. Current build type: $BUILD_TYPE"
    fi

    # Verbose configuration
    if [[ $VERBOSE == true ]]; then
        cmake_args+=("-DCMAKE_VERBOSE_MAKEFILE=ON")
    fi

    # Platform-specific configuration
    case $PLATFORM in
        Mac)
            cmake_args+=("-DCMAKE_OSX_DEPLOYMENT_TARGET=10.15")
            ;;
        Windows)
            cmake_args+=("-DCMAKE_WINDOWS_EXPORT_ALL_SYMBOLS=ON")
            ;;
    esac

    log_info "CMake configuration arguments:"
    for arg in "${cmake_args[@]}"; do
        log_info "  $arg"
    done

    # Run CMake
    cmake "${cmake_args[@]}" "$PROJECT_ROOT"

    if [[ $? -eq 0 ]]; then
        log_info "CMake configuration successful"
    else
        log_error "CMake configuration failed"
        exit 1
    fi
}

# Function to build the project
build_project() {
    log_info "Building project with $JOBS parallel jobs"

    local build_args=()
    mkdir -p "$BUILD_DIR"
    local output_file="${BUILD_DIR}/compilation_output_$(date +%Y%m%d_%H%M%S).txt"

    if [[ $USE_NINJA == true ]]; then
        build_args+=("ninja")
        if [[ $VERBOSE == true ]]; then
            build_args+=("-v")
        fi
    else
        build_args+=("make" "-j$JOBS")
        if [[ $VERBOSE == true ]]; then
            build_args+=("VERBOSE=1")
        fi
    fi

    log_info "Compilation output will be saved to: $output_file"

    local start_time
    start_time=$(date +%s)

    local cmd_to_run=("${build_args[@]}")
    
    if eval "${cmd_to_run[*]}" 2>&1 | tee "$output_file"; then
        local build_status=${PIPESTATUS[0]}
    else
        local build_status=${PIPESTATUS[0]}
    fi

    local end_time
    end_time=$(date +%s)
    local build_duration=$((end_time - start_time))

    if [[ $build_status -eq 0 ]]; then
        log_info "Build completed successfully in ${build_duration}s"
        show_build_artifacts
    else
        log_error "Build failed after ${build_duration}s (Exit code: $build_status)"
        log_error "Check the output above and in '$output_file' for error details"
        exit 1
    fi
}

# Function to show built artifacts
show_build_artifacts() {
    log_info "Built artifacts (relative to $BUILD_DIR):"

    local current_dir_artifacts=$(pwd)
    if [[ "$current_dir_artifacts" != "$BUILD_DIR" ]]; then
        log_warn "Not in BUILD_DIR ($BUILD_DIR), artifact paths might be incorrect if relative."
    fi

    local artifacts=(
        "src/quantlib/quantlib_process"
    )

    for artifact_rel_path in "${artifacts[@]}"; do
        local artifact_full_path="${BUILD_DIR}/${artifact_rel_path}"
        if [[ -f "$artifact_full_path" ]]; then
            echo -e "  ${GREEN}✓${NC} ${artifact_rel_path}"
            ls -lh "$artifact_full_path" | awk '{print "      Size: " $5 ", Modified: " $6 " " $7 " " $8}'
        else
            if [[ -f "$artifact_rel_path" ]]; then
                echo -e "  ${GREEN}✓${NC} ${artifact_rel_path} (found at PWD)"
                ls -lh "$artifact_rel_path" | awk '{print "      Size: " $5 ", Modified: " $6 " " $7 " " $8}'
            else
                echo -e "  ${RED}✗${NC} ${artifact_rel_path} (not found at $artifact_full_path or PWD)"
            fi
        fi
    done
}

# Function to install
install_project() {
    if [[ $INSTALL == true ]]; then
        log_info "Installing project"
        cd "$BUILD_DIR"

        if [[ $USE_NINJA == true ]]; then
            if command -v ninja &> /dev/null; then
                ninja install
            else
                log_error "Ninja not found, cannot install with Ninja."
                exit 1
            fi
        else
            if command -v make &> /dev/null; then
                make install
            else
                log_error "Make not found, cannot install with Make."
                exit 1
            fi
        fi

        if [[ $? -eq 0 ]]; then
            log_info "Installation completed successfully"
        else
            log_error "Installation failed"
            exit 1
        fi
    fi
}

# Function to print build summary
print_summary() {
    log_info "Build Summary"

    echo -e "${CYAN}Project:${NC}          Alaris Trading System"
    echo -e "${CYAN}Build Type:${NC}       $BUILD_TYPE"
    echo -e "${CYAN}Platform:${NC}         $PLATFORM"
    echo -e "${CYAN}Jobs:${NC}             $JOBS"
    echo -e "${CYAN}Generator:${NC}        $(if [[ $USE_NINJA == true ]]; then echo "Ninja"; else echo "Make"; fi)"
    echo -e "${CYAN}CCache:${NC}           $(if [[ $USE_CCACHE == true ]] && command -v ccache &> /dev/null; then echo "Enabled"; else echo "Disabled"; fi)"
    if [[ $BUILD_TYPE == "Debug" ]]; then
        echo -e "${CYAN}Sanitizers:${NC}       $(if [[ $ENABLE_SANITIZERS == true ]]; then echo "Enabled"; else echo "Disabled"; fi)"
        echo -e "${CYAN}Coverage:${NC}         $(if [[ $ENABLE_COVERAGE == true ]]; then echo "Enabled"; else echo "Disabled"; fi)"
    fi
    echo -e "${CYAN}Build Directory:${NC}  $BUILD_DIR"

    echo ""
    echo -e "${CYAN}Next steps:${NC}"
    if [[ -f "${BUILD_DIR}/src/quantlib/quantlib_process" ]]; then
        echo -e "  ${GREEN}•${NC} To run the QuantLib process:"
        echo -e "    cd \"$BUILD_DIR\" && ./src/quantlib/quantlib_process"
    fi

    echo ""
    echo -e "  ${GREEN}•${NC} To clean and rebuild:"
    echo -e "    $0 --clean"
    echo ""
    echo -e "  ${GREEN}•${NC} Available build tool targets (from $BUILD_DIR):"
    if [[ $USE_NINJA == true ]]; then
        echo -e "    ninja -t list              # List all explicit targets"
        echo -e "    ninja -t targets           # List all targets including internal ones"
    else
        echo -e "    make help                  # Usually lists available targets"
    fi
    echo ""
    echo -e "  ${GREEN}•${NC} Compilation output log:"
    echo -e "    Check files like ${BUILD_DIR}/compilation_output_YYYYMMDD_HHMMSS.txt"
}

# Function to handle errors
handle_error() {
    local exit_code=$?
    if [[ $exit_code -ne 0 ]]; then
        log_error "Build script failed with exit code $exit_code on line $BASH_LINENO"
        echo ""
        echo -e "${YELLOW}Troubleshooting tips:${NC}"
        echo "1. Check that all dependencies are installed (run script with no options for initial checks)."
        echo "2. Try cleaning and rebuilding: $0 --clean"
        echo "3. Check the CMake configuration output in the scrollback for errors."
        echo "4. Examine any compilation log files in the build directory (e.g., compilation_output_*.txt)."
        echo "5. Ensure you have sufficient disk space and memory."
        echo "6. Run with --verbose for more detailed output: $0 --verbose [other_options]"
    fi
    exit $exit_code
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
    install_project

    print_summary

    log_info "Build script completed successfully!"
}

# Run main function with all arguments
main "$@"
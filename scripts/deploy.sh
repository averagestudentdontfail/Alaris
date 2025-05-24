#!/bin/bash

# Comprehensive Deployment Script for Alaris Trading System
# This script handles complete deployment from source to production

set -e  # Exit on any error

# Colors for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly PURPLE='\033[0;35m'
readonly CYAN='\033[0;36m'
readonly NC='\033[0m' # No Color

# Configuration
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Default values
DEPLOYMENT_TYPE="development"
BUILD_TYPE="Release"
INSTALL_PREFIX="/opt/alaris"
ENABLE_DOCKER=true
ENABLE_MONITORING=true
ENABLE_TESTING=true
SKIP_SYSTEM_SETUP=false
FORCE_REBUILD=false
VERBOSE=false
DRY_RUN=false

# Function to print colored output
print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_header() {
    echo -e "${BLUE}╔══════════════════════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║ $1${NC}"
    echo -e "${BLUE}╚══════════════════════════════════════════════════════════════════════════════════╝${NC}"
}

print_section() {
    echo -e "${CYAN}── $1 ──${NC}"
}

# Function to show usage
show_usage() {
    cat << EOF
Alaris Trading System - Comprehensive Deployment Script

Usage: $0 [OPTIONS] [DEPLOYMENT_TYPE]

DEPLOYMENT_TYPES:
    development     Deploy for development (default)
    testing         Deploy for testing environment
    staging         Deploy for staging environment
    production      Deploy for production environment

OPTIONS:
    -h, --help              Show this help message
    -b, --build-type TYPE   Build type (Debug|Release|RelWithDebInfo) [default: Release]
    -p, --prefix PATH       Installation prefix [default: /opt/alaris]
    -j, --jobs N           Number of parallel jobs [default: auto-detected]
    
    --docker                Enable Docker deployment [default: true]
    --no-docker             Disable Docker deployment
    --monitoring            Enable monitoring stack [default: true]
    --no-monitoring         Disable monitoring stack
    --testing               Enable testing [default: true]
    --no-testing            Skip testing
    
    --skip-system-setup     Skip system configuration
    --force-rebuild         Force complete rebuild
    --verbose               Enable verbose output
    --dry-run               Show what would be done without executing
    
    --interactive           Interactive deployment with prompts
    --config FILE           Use custom configuration file

EXAMPLES:
    $0                                    # Development deployment
    $0 production --prefix /usr/local    # Production deployment
    $0 testing --no-docker --verbose     # Testing without Docker
    $0 --dry-run production               # Show production deployment plan

EOF
}

# Parse command line arguments
parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -h|--help)
                show_usage
                exit 0
                ;;
            -b|--build-type)
                BUILD_TYPE="$2"
                shift 2
                ;;
            -p|--prefix)
                INSTALL_PREFIX="$2"
                shift 2
                ;;
            -j|--jobs)
                JOBS="$2"
                shift 2
                ;;
            --docker)
                ENABLE_DOCKER=true
                shift
                ;;
            --no-docker)
                ENABLE_DOCKER=false
                shift
                ;;
            --monitoring)
                ENABLE_MONITORING=true
                shift
                ;;
            --no-monitoring)
                ENABLE_MONITORING=false
                shift
                ;;
            --testing)
                ENABLE_TESTING=true
                shift
                ;;
            --no-testing)
                ENABLE_TESTING=false
                shift
                ;;
            --skip-system-setup)
                SKIP_SYSTEM_SETUP=true
                shift
                ;;
            --force-rebuild)
                FORCE_REBUILD=true
                shift
                ;;
            --verbose)
                VERBOSE=true
                shift
                ;;
            --dry-run)
                DRY_RUN=true
                shift
                ;;
            --interactive)
                INTERACTIVE=true
                shift
                ;;
            --config)
                CONFIG_FILE="$2"
                shift 2
                ;;
            development|testing|staging|production)
                DEPLOYMENT_TYPE="$1"
                shift
                ;;
            *)
                print_error "Unknown option: $1"
                show_usage
                exit 1
                ;;
        esac
    done
    
    # Set defaults based on deployment type
    case $DEPLOYMENT_TYPE in
        development)
            BUILD_TYPE=${BUILD_TYPE:-Debug}
            INSTALL_PREFIX=${INSTALL_PREFIX:-"$PROJECT_ROOT/install"}
            ;;
        testing)
            BUILD_TYPE=${BUILD_TYPE:-Release}
            ENABLE_TESTING=true
            ;;
        staging)
            BUILD_TYPE=${BUILD_TYPE:-Release}
            SKIP_SYSTEM_SETUP=${SKIP_SYSTEM_SETUP:-false}
            ;;
        production)
            BUILD_TYPE=${BUILD_TYPE:-Release}
            SKIP_SYSTEM_SETUP=${SKIP_SYSTEM_SETUP:-false}
            ENABLE_TESTING=${ENABLE_TESTING:-false}
            ;;
    esac
    
    # Auto-detect jobs if not specified
    if [[ -z "$JOBS" ]]; then
        JOBS=$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 4)
    fi
}

# Function to validate prerequisites
validate_prerequisites() {
    print_section "Validating Prerequisites"
    
    local missing_tools=()
    
    # Check required tools
    local required_tools=("git" "cmake" "make" "docker" "docker-compose")
    
    for tool in "${required_tools[@]}"; do
        if ! command -v "$tool" &> /dev/null; then
            if [[ "$tool" == "docker" || "$tool" == "docker-compose" ]] && [[ "$ENABLE_DOCKER" == "false" ]]; then
                continue
            fi
            missing_tools+=("$tool")
        fi
    done
    
    if [[ ${#missing_tools[@]} -gt 0 ]]; then
        print_error "Missing required tools: ${missing_tools[*]}"
        print_error "Please install them before continuing"
        exit 1
    fi
    
    # Check CMake version
    local cmake_version
    cmake_version=$(cmake --version | head -n1 | sed 's/cmake version //')
    if ! cmake --version | head -n1 | grep -qE 'cmake version ([3-9]\.[2-9][0-9]|[4-9]\.[0-9]+)'; then
        print_error "CMake 3.20+ required, found: $cmake_version"
        exit 1
    fi
    
    # Check Docker if enabled
    if [[ "$ENABLE_DOCKER" == "true" ]]; then
        if ! docker info &> /dev/null; then
            print_error "Docker is not running or accessible"
            exit 1
        fi
    fi
    
    # Check disk space
    local available_space
    available_space=$(df "$PROJECT_ROOT" | awk 'NR==2 {print $4}')
    if [[ $available_space -lt 2000000 ]]; then  # 2GB in KB
        print_warning "Low disk space available: $(($available_space/1024/1024))GB"
    fi
    
    print_status "Prerequisites validation completed"
}

# Function to setup system configuration
setup_system() {
    if [[ "$SKIP_SYSTEM_SETUP" == "true" ]]; then
        print_status "Skipping system setup"
        return
    fi
    
    print_section "System Setup"
    
    if [[ $EUID -eq 0 ]]; then
        print_warning "Running as root - some optimizations will be applied"
        
        # Set CPU performance governor
        echo performance | tee /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor 2>/dev/null || true
        
        # Increase shared memory limits
        echo 'kernel.shmmax = 68719476736' >> /etc/sysctl.conf || true
        echo 'kernel.shmall = 4294967296' >> /etc/sysctl.conf || true
        
        # Apply sysctl changes
        sysctl -p 2>/dev/null || true
        
        print_status "System optimizations applied"
    else
        print_warning "Not running as root - skipping system optimizations"
    fi
}

# Function to prepare source code
prepare_source() {
    print_section "Preparing Source Code"
    
    cd "$PROJECT_ROOT"
    
    # Update submodules
    print_status "Updating git submodules..."
    if [[ "$DRY_RUN" == "false" ]]; then
        git submodule update --init --recursive
    fi
    
    # Check git status
    if ! git diff-index --quiet HEAD --; then
        if [[ "$DEPLOYMENT_TYPE" == "production" ]]; then
            print_error "Repository has uncommitted changes (not allowed for production)"
            exit 1
        else
            print_warning "Repository has uncommitted changes"
        fi
    fi
    
    print_status "Source preparation completed"
}

# Function to build the project
build_project() {
    print_section "Building Project"
    
    local build_dir="$PROJECT_ROOT/build"
    
    if [[ "$FORCE_REBUILD" == "true" && -d "$build_dir" ]]; then
        print_status "Removing existing build directory"
        if [[ "$DRY_RUN" == "false" ]]; then
            rm -rf "$build_dir"
        fi
    fi
    
    if [[ "$DRY_RUN" == "false" ]]; then
        mkdir -p "$build_dir"
        cd "$build_dir"
        
        # Configure
        print_status "Configuring with CMake..."
        cmake .. \
            -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
            -DCMAKE_INSTALL_PREFIX="$INSTALL_PREFIX" \
            -DCMAKE_CXX_STANDARD=20 \
            -DCMAKE_EXPORT_COMPILE_COMMANDS=ON \
            ${VERBOSE:+-DCMAKE_VERBOSE_MAKEFILE=ON}
        
        # Build
        print_status "Building with $JOBS parallel jobs..."
        cmake --build . --config "$BUILD_TYPE" --parallel "$JOBS"
        
        print_status "Build completed successfully"
    else
        print_status "[DRY-RUN] Would build project with $JOBS jobs"
    fi
}

# Function to run tests
run_tests() {
    if [[ "$ENABLE_TESTING" != "true" ]]; then
        print_status "Testing disabled - skipping"
        return
    fi
    
    print_section "Running Tests"
    
    if [[ "$DRY_RUN" == "false" ]]; then
        cd "$PROJECT_ROOT/build"
        
        # Run CTest
        print_status "Running unit tests..."
        ctest --output-on-failure --parallel "$JOBS" --timeout 300
        
        # Run integration tests
        print_status "Running integration tests..."
        ctest -L integration --output-on-failure --timeout 600
        
        print_status "All tests passed"
    else
        print_status "[DRY-RUN] Would run test suite"
    fi
}

# Function to install the project
install_project() {
    print_section "Installing Project"
    
    if [[ "$DRY_RUN" == "false" ]]; then
        cd "$PROJECT_ROOT/build"
        
        # Install
        print_status "Installing to $INSTALL_PREFIX..."
        if [[ $EUID -eq 0 || "$INSTALL_PREFIX" == "$PROJECT_ROOT"* ]]; then
            cmake --install . --config "$BUILD_TYPE"
        else
            sudo cmake --install . --config "$BUILD_TYPE"
        fi
        
        # Run post-install script
        if [[ -x "$INSTALL_PREFIX/bin/post-install.sh" ]]; then
            print_status "Running post-installation setup..."
            if [[ $EUID -eq 0 ]]; then
                "$INSTALL_PREFIX/bin/post-install.sh"
            else
                sudo "$INSTALL_PREFIX/bin/post-install.sh"
            fi
        fi
        
        print_status "Installation completed"
    else
        print_status "[DRY-RUN] Would install to $INSTALL_PREFIX"
    fi
}

# Function to setup Docker deployment
setup_docker() {
    if [[ "$ENABLE_DOCKER" != "true" ]]; then
        print_status "Docker deployment disabled - skipping"
        return
    fi
    
    print_section "Setting up Docker Deployment"
    
    cd "$PROJECT_ROOT"
    
    if [[ "$DRY_RUN" == "false" ]]; then
        # Build Docker images
        print_status "Building Docker images..."
        docker-compose build --parallel
        
        # Start services
        print_status "Starting Docker services..."
        docker-compose up -d
        
        # Wait for health checks
        print_status "Waiting for services to be healthy..."
        local timeout=300  # 5 minutes
        local elapsed=0
        
        while [[ $elapsed -lt $timeout ]]; do
            if docker-compose ps | grep -E "(quantlib|lean)" | grep -v "Up (healthy)" | grep -q "Up"; then
                sleep 5
                elapsed=$((elapsed + 5))
            else
                break
            fi
        done
        
        if [[ $elapsed -ge $timeout ]]; then
            print_warning "Services may not be fully healthy yet"
        else
            print_status "All services are healthy"
        fi
        
        # Show status
        docker-compose ps
        
    else
        print_status "[DRY-RUN] Would build and start Docker services"
    fi
}

# Function to setup monitoring
setup_monitoring() {
    if [[ "$ENABLE_MONITORING" != "true" ]]; then
        print_status "Monitoring disabled - skipping"
        return
    fi
    
    print_section "Setting up Monitoring"
    
    if [[ "$DRY_RUN" == "false" ]]; then
        cd "$PROJECT_ROOT"
        
        # Start monitoring services
        print_status "Starting monitoring stack..."
        docker-compose up -d prometheus grafana
        
        # Wait for services
        sleep 10
        
        # Check service health
        if curl -s http://localhost:9090/-/healthy &>/dev/null; then
            print_status "✓ Prometheus is healthy"
        else
            print_warning "✗ Prometheus health check failed"
        fi
        
        if curl -s http://localhost:3000/api/health &>/dev/null; then
            print_status "✓ Grafana is healthy"
        else
            print_warning "✗ Grafana health check failed"
        fi
        
        print_status "Monitoring setup completed"
        print_status "Grafana: http://localhost:3000 (admin/alaris123)"
        print_status "Prometheus: http://localhost:9090"
        
    else
        print_status "[DRY-RUN] Would setup monitoring stack"
    fi
}

# Function to validate deployment
validate_deployment() {
    print_section "Validating Deployment"
    
    local errors=0
    
    if [[ "$DRY_RUN" == "true" ]]; then
        print_status "[DRY-RUN] Would validate deployment"
        return
    fi
    
    # Check if main executable exists
    if [[ -x "$INSTALL_PREFIX/bin/quantlib_process" ]]; then
        print_status "✓ QuantLib process executable found"
    else
        print_error "✗ QuantLib process executable not found"
        errors=$((errors + 1))
    fi
    
    # Check configuration
    if [[ -f "$INSTALL_PREFIX/etc/alaris/quantlib_process.yaml" ]]; then
        print_status "✓ Configuration file found"
        
        # Validate configuration
        if "$INSTALL_PREFIX/bin/alaris_config_validator" "$INSTALL_PREFIX/etc/alaris/quantlib_process.yaml" &>/dev/null; then
            print_status "✓ Configuration validation passed"
        else
            print_error "✗ Configuration validation failed"
            errors=$((errors + 1))
        fi
    else
        print_error "✗ Configuration file not found"
        errors=$((errors + 1))
    fi
    
    # Check system health
    if "$INSTALL_PREFIX/bin/alaris_sysinfo" --health-check &>/dev/null; then
        print_status "✓ System health check passed"
    else
        print_warning "! System health check returned warnings"
    fi
    
    # Check Docker services if enabled
    if [[ "$ENABLE_DOCKER" == "true" ]]; then
        if docker-compose ps | grep -q "Up"; then
            print_status "✓ Docker services are running"
        else
            print_error "✗ Docker services are not running"
            errors=$((errors + 1))
        fi
    fi
    
    if [[ $errors -eq 0 ]]; then
        print_status "Deployment validation passed"
        return 0
    else
        print_error "Deployment validation failed with $errors error(s)"
        return 1
    fi
}

# Function to show deployment summary
show_summary() {
    print_header "Deployment Summary"
    
    echo -e "${CYAN}Deployment Type:${NC} $DEPLOYMENT_TYPE"
    echo -e "${CYAN}Build Type:${NC} $BUILD_TYPE"
    echo -e "${CYAN}Install Prefix:${NC} $INSTALL_PREFIX"
    echo -e "${CYAN}Docker Enabled:${NC} $ENABLE_DOCKER"
    echo -e "${CYAN}Monitoring Enabled:${NC} $ENABLE_MONITORING"
    echo -e "${CYAN}Testing Enabled:${NC} $ENABLE_TESTING"
    echo ""
    
    if [[ "$DRY_RUN" == "true" ]]; then
        echo -e "${YELLOW}This was a dry run - no changes were made${NC}"
        echo ""
        return
    fi
    
    echo -e "${GREEN}Deployment completed successfully!${NC}"
    echo ""
    
    echo "📋 Next Steps:"
    echo ""
    
    if [[ "$ENABLE_DOCKER" == "true" ]]; then
        echo "🐳 Docker Services:"
        echo "   Status: docker-compose ps"
        echo "   Logs:   docker-compose logs -f"
        echo "   Stop:   docker-compose down"
    fi
    
    echo ""
    echo "🚀 Running Alaris:"
    echo "   Manual: $INSTALL_PREFIX/bin/quantlib_process"
    echo "   Config: $INSTALL_PREFIX/etc/alaris/quantlib_process.yaml"
    
    if command -v systemctl &>/dev/null && [[ -f "/etc/systemd/system/alaris-quantlib.service" ]]; then
        echo "   Service: systemctl start alaris-quantlib"
    fi
    
    echo ""
    echo "📊 Monitoring:"
    if [[ "$ENABLE_MONITORING" == "true" ]]; then
        echo "   Grafana:    http://localhost:3000 (admin/alaris123)"
        echo "   Prometheus: http://localhost:9090"
    else
        echo "   Monitoring disabled"
    fi
    
    echo ""
    echo "📝 Logs:"
    echo "   Location: /var/log/alaris/"
    echo "   Live:     tail -f /var/log/alaris/quantlib.log"
    
    echo ""
    echo "🔧 System Info:"
    echo "   Check: $INSTALL_PREFIX/bin/alaris_sysinfo"
    echo "   Health: $INSTALL_PREFIX/bin/alaris_sysinfo --health-check"
    
    echo ""
}

# Function to handle cleanup on exit
cleanup() {
    local exit_code=$?
    
    if [[ $exit_code -ne 0 ]]; then
        print_error "Deployment failed with exit code $exit_code"
        
        if [[ "$ENABLE_DOCKER" == "true" ]]; then
            print_status "Checking Docker services status..."
            docker-compose ps 2>/dev/null || true
            
            print_status "Recent Docker logs:"
            docker-compose logs --tail=20 2>/dev/null || true
        fi
    fi
    
    exit $exit_code
}

# Main deployment function
main() {
    trap cleanup EXIT
    
    print_header "Alaris Trading System - Comprehensive Deployment"
    
    parse_arguments "$@"
    
    echo "Deployment Configuration:"
    echo "  Type: $DEPLOYMENT_TYPE"
    echo "  Build: $BUILD_TYPE"
    echo "  Install: $INSTALL_PREFIX"
    echo "  Docker: $ENABLE_DOCKER"
    echo "  Monitoring: $ENABLE_MONITORING"
    echo "  Testing: $ENABLE_TESTING"
    echo "  Dry Run: $DRY_RUN"
    echo ""
    
    if [[ "${INTERACTIVE:-false}" == "true" ]]; then
        read -p "Continue with deployment? [y/N] " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            print_status "Deployment cancelled"
            exit 0
        fi
    fi
    
    # Execute deployment steps
    validate_prerequisites
    setup_system
    prepare_source
    build_project
    run_tests
    install_project
    setup_docker
    setup_monitoring
    validate_deployment
    show_summary
    
    print_status "🎉 Alaris deployment completed successfully!"
}

# Run main function with all arguments
main "$@"
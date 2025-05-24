#!/bin/bash

# Alaris Trading System Build Script
# This script ensures proper setup and building of the QuantLib-based trading system

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
BUILD_TYPE=${1:-Release}
BUILD_DIR="build"
EXTERNAL_DIR="external"
JOBS=$(nproc 2>/dev/null || echo 4)  # Default to 4 if nproc not available

echo -e "${BLUE}=== Alaris Trading System Build Script ===${NC}"
echo -e "${BLUE}Build type: ${BUILD_TYPE}${NC}"
echo -e "${BLUE}Using ${JOBS} parallel jobs${NC}"

# Function to print status
print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check for required tools
print_status "Checking build requirements..."

check_command() {
    if ! command -v $1 &> /dev/null; then
        print_error "$1 is required but not installed"
        case $1 in
            cmake)
                echo "Install with: sudo apt-get install cmake  # Ubuntu/Debian"
                echo "           or: sudo yum install cmake     # CentOS/RHEL"
                ;;
            make)
                echo "Install with: sudo apt-get install build-essential  # Ubuntu/Debian"
                echo "           or: sudo yum groupinstall 'Development Tools'  # CentOS/RHEL"
                ;;
            g++)
                echo "Install with: sudo apt-get install g++  # Ubuntu/Debian"
                echo "           or: sudo yum install gcc-c++ # CentOS/RHEL"
                ;;
            git)
                echo "Install with: sudo apt-get install git  # Ubuntu/Debian"
                echo "           or: sudo yum install git     # CentOS/RHEL"
                ;;
        esac
        exit 1
    fi
}

# Check required tools
check_command cmake
check_command make
check_command g++
check_command git

# Check CMake version
CMAKE_VERSION=$(cmake --version | head -n1 | sed 's/cmake version //')
print_status "Using CMake version: $CMAKE_VERSION"

# Check minimum CMake version (3.20)
cmake_major=$(echo $CMAKE_VERSION | cut -d. -f1)
cmake_minor=$(echo $CMAKE_VERSION | cut -d. -f2)
if [ "$cmake_major" -lt 3 ] || ([ "$cmake_major" -eq 3 ] && [ "$cmake_minor" -lt 20 ]); then
    print_error "CMake 3.20 or higher is required, found $CMAKE_VERSION"
    exit 1
fi

# Check C++ compiler version
if command -v g++ &> /dev/null; then
    GXX_VERSION=$(g++ --version | head -n1)
    print_status "Using compiler: $GXX_VERSION"
    
    # Check for C++20 support (GCC 10+)
    gcc_major=$(g++ -dumpversion | cut -d. -f1)
    if [ "$gcc_major" -lt 10 ]; then
        print_warning "GCC 10+ recommended for full C++20 support, found GCC $gcc_major"
    fi
fi

# Check for Boost libraries (required by QuantLib)
print_status "Checking for Boost libraries..."
if ! ldconfig -p | grep -q libboost; then
    print_warning "Boost libraries may not be installed"
    echo "Install with: sudo apt-get install libboost-all-dev  # Ubuntu/Debian"
    echo "           or: sudo yum install boost-devel          # CentOS/RHEL"
fi

# Setup external dependencies
print_status "Setting up external dependencies..."

# Create external directory if it doesn't exist
mkdir -p ${EXTERNAL_DIR}

# Check for QuantLib
if [ ! -d "${EXTERNAL_DIR}/quant" ]; then
    print_warning "QuantLib not found, cloning from GitHub..."
    cd ${EXTERNAL_DIR}
    
    # Clone QuantLib
    if ! git clone https://github.com/lballabio/QuantLib.git quant; then
        print_error "Failed to clone QuantLib repository"
        exit 1
    fi
    
    cd quant
    
    # Use a stable release
    if ! git checkout v1.32; then
        print_warning "Could not checkout v1.32, using default branch"
    fi
    
    cd ../..
    print_status "QuantLib cloned successfully"
else
    print_status "QuantLib found in ${EXTERNAL_DIR}/quant"
    
    # Check if it's a git repository and update if requested
    if [ "$2" == "update" ] && [ -d "${EXTERNAL_DIR}/quant/.git" ]; then
        print_status "Updating QuantLib..."
        cd ${EXTERNAL_DIR}/quant
        git fetch origin
        git checkout v1.32
        cd ../..
    fi
fi

# Check for yaml-cpp
if [ ! -d "${EXTERNAL_DIR}/yaml-cpp" ]; then
    print_warning "yaml-cpp not found, cloning from GitHub..."
    cd ${EXTERNAL_DIR}
    
    if ! git clone https://github.com/jbeder/yaml-cpp.git; then
        print_error "Failed to clone yaml-cpp repository"
        exit 1
    fi
    
    cd yaml-cpp
    
    # Use a stable release
    if ! git checkout yaml-cpp-0.7.0; then
        print_warning "Could not checkout yaml-cpp-0.7.0, using default branch"
    fi
    
    cd ../..
    print_status "yaml-cpp cloned successfully"
else
    print_status "yaml-cpp found in ${EXTERNAL_DIR}/yaml-cpp"
    
    # Update if requested
    if [ "$2" == "update" ] && [ -d "${EXTERNAL_DIR}/yaml-cpp/.git" ]; then
        print_status "Updating yaml-cpp..."
        cd ${EXTERNAL_DIR}/yaml-cpp
        git fetch origin
        git checkout yaml-cpp-0.7.0
        cd ../..
    fi
fi

# Clean build directory if requested
if [ "$2" == "clean" ] || [ "$3" == "clean" ]; then
    print_status "Cleaning build directory..."
    rm -rf ${BUILD_DIR}
fi

# Create build directory
mkdir -p ${BUILD_DIR}
cd ${BUILD_DIR}

# Configure with CMake
print_status "Configuring build with CMake..."

# Set additional CMake options based on build type
CMAKE_OPTIONS=""
if [ "$BUILD_TYPE" == "Debug" ]; then
    CMAKE_OPTIONS="-DCMAKE_VERBOSE_MAKEFILE=ON"
fi

cmake .. \
    -DCMAKE_BUILD_TYPE=${BUILD_TYPE} \
    -DCMAKE_CXX_STANDARD=20 \
    -DCMAKE_EXPORT_COMPILE_COMMANDS=ON \
    ${CMAKE_OPTIONS}

if [ $? -ne 0 ]; then
    print_error "CMake configuration failed!"
    exit 1
fi

# Build the project
print_status "Building project with ${JOBS} parallel jobs..."
make -j${JOBS}

BUILD_STATUS=$?

if [ $BUILD_STATUS -eq 0 ]; then
    print_status "Build completed successfully!"
    
    # Show built targets
    echo
    echo -e "${GREEN}Built targets:${NC}"
    
    if [ -f "src/quantlib/quantlib_process" ]; then
        echo "  ✓ quantlib_process"
        ls -lh src/quantlib/quantlib_process
    else
        echo "  ✗ quantlib_process (missing)"
    fi
    
    if [ -f "test/alaris_core_test" ]; then
        echo "  ✓ alaris_core_test"
    else
        echo "  ✗ alaris_core_test (missing)"
    fi
    
    if [ -f "test/alaris_integration_test" ]; then
        echo "  ✓ alaris_integration_test"
    else
        echo "  ✗ alaris_integration_test (missing)"
    fi
    
    if [ -f "test/alaris_performance_test" ]; then
        echo "  ✓ alaris_performance_test"
    else
        echo "  ✗ alaris_performance_test (missing)"
    fi
    
    echo
    
    # Run tests if requested
    if [ "$3" == "test" ] || [ "$2" == "test" ]; then
        print_status "Running tests..."
        if command -v ctest &> /dev/null; then
            ctest --output-on-failure -j${JOBS}
            TEST_STATUS=$?
            if [ $TEST_STATUS -eq 0 ]; then
                print_status "All tests passed!"
            else
                print_warning "Some tests failed (exit code: $TEST_STATUS)"
            fi
        else
            print_warning "ctest not available, running tests manually..."
            if [ -f "test/alaris_core_test" ]; then
                echo "Running core tests..."
                ./test/alaris_core_test
            fi
        fi
    fi
    
    # Install if requested
    if [ "$4" == "install" ] || [ "$3" == "install" ] || [ "$2" == "install" ]; then
        print_status "Installing..."
        make install
    fi
    
    echo
    echo -e "${GREEN}=== Build Complete ===${NC}"
    echo -e "${BLUE}To run the QuantLib process:${NC}"
    echo -e "  cd ${BUILD_DIR} && ./src/quantlib/quantlib_process"
    echo
    echo -e "${BLUE}To run tests:${NC}"
    echo -e "  cd ${BUILD_DIR} && ctest --output-on-failure"
    echo
    echo -e "${BLUE}Available make targets:${NC}"
    echo -e "  make core          # Build core library only"
    echo -e "  make pricing       # Build pricing library only"
    echo -e "  make volatility    # Build volatility library only"
    echo -e "  make strategy      # Build strategy library only"
    echo -e "  make test_core     # Run core tests only"
    echo -e "  make test_all      # Run all tests"
    echo
    
else
    print_error "Build failed with exit code: $BUILD_STATUS"
    echo
    echo -e "${YELLOW}Troubleshooting tips:${NC}"
    echo "1. Check that all dependencies are installed:"
    echo "   - libboost-all-dev (Ubuntu) or boost-devel (CentOS)"
    echo "   - build-essential (Ubuntu) or 'Development Tools' (CentOS)"
    echo "2. Try cleaning and rebuilding:"
    echo "   ./scripts/build.sh $BUILD_TYPE clean"
    echo "3. Check the CMake configuration output above for errors"
    echo "4. Ensure you have sufficient disk space and memory"
    echo
    exit 1
fi
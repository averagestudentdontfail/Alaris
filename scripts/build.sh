#!/bin/bash
# Alaris Build Script

set -e

echo "🔨 Building Alaris Trading System..."

# Check dependencies
check_dependency() {
    if ! command -v $1 &> /dev/null; then
        echo "❌ $1 is required but not installed"
        echo "   Ubuntu/Debian: sudo apt-get install $2"
        return 1
    fi
    return 0
}

echo "📋 Checking dependencies..."
check_dependency "cmake" "cmake" || exit 1
check_dependency "make" "build-essential" || exit 1
check_dependency "g++" "build-essential" || exit 1

# Create and enter build directory
mkdir -p build
cd build

echo "⚙️  Configuring with CMake..."
cmake -DCMAKE_BUILD_TYPE=Release \
      -DCMAKE_INSTALL_PREFIX=/opt/alaris \
      ..

echo "🔨 Building QuantLib process..."
make -j$(nproc) quantlib_process

echo "🧪 Building tests..."
make -j$(nproc) alaris_core_test
make -j$(nproc) alaris_pricing_test  
make -j$(nproc) alaris_integration_test

echo ""
echo "✅ Build completed successfully!"
echo ""
echo "📁 Executables built:"
echo "   QuantLib Process: build/src/quantlib/quantlib_process"
echo "   Core Tests:       build/test/alaris_core_test"
echo "   Pricing Tests:    build/test/alaris_pricing_test"
echo "   Integration Tests: build/test/alaris_integration_test"
echo ""
echo "🧪 Run tests with: cd build && make test"

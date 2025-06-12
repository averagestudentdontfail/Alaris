# Alaris Trading System

Alaris is a high-performance algorithmic trading system for quantitative strategies, specializing in options and volatility arbitrage.

**Architecture**: Process-isolated C++ engine + C# brokerage integration with lock-free IPC communication.

---

## System Overview

**Alaris combines two specialized processes:**

* **C++ QuantLib Process** - High-speed financial calculations, options pricing (ALO), and GARCH volatility modeling
* **C# Lean Process** - Portfolio management, order execution, and Interactive Brokers integration  
* **Shared Memory IPC** - Real-time communication between processes with minimal latency

**Key Benefits:**
- **Performance** - C++ optimized for real-time priority without C# GC interference
- **Stability** - Process isolation prevents cascading failures
- **Specialization** - Best tools for each task (C++ for computation, C# for brokerage APIs)

---

## Quick Start

### Prerequisites

**Ubuntu/Debian (recommended):**
```bash
sudo apt update
sudo apt install build-essential cmake libquantlib0-dev libyaml-cpp-dev dotnet-sdk-8.0
```

**Requirements:**
- GCC 10+ with C++20 support
- CMake 3.20+
- .NET SDK 8.0+
- QuantLib and yaml-cpp libraries
- Interactive Brokers TWS or Gateway

### Build & Setup

**Single automated build:**
```bash
# Clone and build
git clone <repository-url> && cd Alaris
cmake -S . -B build
cmake --build build
```

**The build system automatically:**
- Compiles C++ and C# components
- Detects and configures Linux capabilities tools
- Sets real-time capabilities (if sudo available)
- Configures local logging paths
- Generates startup script with networking detection
- Creates all necessary scripts and directories
- Provides clear feedback and troubleshooting guidance

**Available build targets:**
```bash
cmake --build build                 # Build everything
cmake --build build --target alaris-all      # Build all C++ components  
cmake --build build --target lean-process    # Build .NET components
cmake --build build --target verify-build    # Verify build completion
cmake --build build --target alaris-help     # Show detailed help
```

### Configure IBKR Connection

**Edit your account details:**
```bash
nano config/lean_process.yaml
```
Update the `brokerage.account` field with your IBKR account ID.

**For WSL users:** The system auto-detects Windows host IP, but verify:
```bash
# Test connectivity to your IB Gateway/TWS
cd build
nc -z <WINDOWS_HOST_IP> 4002  # Paper trading port
```

### Run the System

**Start Interactive Brokers first, then:**
```bash
cd build
./start-alaris.sh paper    # Paper trading
./start-alaris.sh live     # Live trading (caution!)
```

**Alternative manual startup:**
```bash
# Terminal 1 - QuantLib Process
./bin/quantlib-process ../config/quantlib_process.yaml

# Terminal 2 - Lean Process  
dotnet bin/Release/Alaris.Lean.dll --mode paper
```

---

## Configuration

### Trading Strategy
**Default configuration:** Delta-neutral volatility arbitrage across 25 symbols (ETFs, Tech, Finance, Energy, Healthcare)

**Key files:**
- `config/quantlib_process.yaml` - C++ engine settings, strategy parameters
- `config/lean_process.yaml` - C# process settings, IBKR connection, symbol universe
- `config/algorithm.json` - Algorithm-specific parameters

### Performance Optimization
**Automatic on Linux:**
- Real-time process priority (`CAP_SYS_NICE`)
- Memory locking (`CAP_IPC_LOCK`)
- Local logging (no `/var/log/` permissions needed)

**Manual optimization:**
```bash
# If automatic setup failed
sudo cmake --build build --target set-capabilities
# Or directly:
sudo ./build/set-capabilities.sh
```

---

## Operational Modes

| Mode | Purpose | Connection |
|------|---------|------------|
| `download` | Historical data retrieval | IBKR required |
| `backtest` | Strategy testing on historical data | Local data only |
| `paper` | Forward testing with simulated money | IBKR paper account |
| `live` | Real money trading | IBKR live account |

**Examples:**
```bash
# Download market data first
./start-alaris.sh download

# Backtest strategy
./start-alaris.sh backtest

# Paper trading
./start-alaris.sh paper

# Live trading (use with extreme caution)
./start-alaris.sh live
```

---

## Build System & Development

### Build Verification
```bash
# Comprehensive build check
cmake --build build --target verify-build

# Or use the standalone verification script
./verify-build.sh build
```

### Development Workflow
```bash
# Quick development cycle
make -C build dev              # Build + setup
./build/start-alaris.sh paper  # Test in paper mode

# Debugging
make -C build clean-shm        # Clean shared memory
cmake --build build --target alaris-setup  # Re-run setup
./verify-build.sh build       # Verify everything
```

### Available Make Targets
```bash
make -C build alaris-complete  # Build everything and run setup
make -C build set-capabilities # Set Linux capabilities
make -C build alaris-setup     # Run setup manually
make -C build verify-build     # Verify build completion
make -C build alaris-help      # Show detailed help
make -C build clean-shm        # Clean shared memory
make -C build test-ibkr        # Test IBKR connectivity
```

---

## Troubleshooting

### CMake Configuration Issues

**Reserved target name error:**
```bash
# If you see "target name help is reserved"
sed -i 's/add_custom_target(help/add_custom_target(alaris-help/' .cmake/BuildSystem.cmake
```

**Parse errors in configuration:**
```bash
# Clean configuration and rebuild
rm -rf build
cmake -S . -B build
cmake --build build
```

**Missing dependencies:**
```bash
# Ubuntu/Debian
sudo apt install build-essential cmake libquantlib0-dev libyaml-cpp-dev

# If QuantLib not found, build from source:
git clone https://github.com/lballabio/QuantLib.git external/QuantLib
```

### Build Issues
```bash
# Clean rebuild
rm -rf build && cmake -S . -B build && cmake --build build

# Verify dependencies
cmake --build build --target verify-build

# Check what was built
ls -la build/bin/
```

### IBKR Connection Issues
- IB Gateway/TWS running and logged in
- API enabled in IB settings (Global Config → API → Settings)
- "Allow connections from localhost only" unchecked (for WSL)
- Correct host IP in config files

### WSL Networking
```bash
# Find Windows host IP
ip route show default | awk '/default/ {print $3}'

# Update config files
sed -i 's/host: "127.0.0.1"/host: "YOUR_WINDOWS_IP"/g' config/*.yaml

# Test connectivity
cmake --build build --target test-ibkr
```

### Linux Capabilities Issues
```bash
# Check if capabilities are set
getcap build/bin/quantlib-process build/bin/alaris

# Set manually if automatic setup failed
sudo cmake --build build --target set-capabilities

# Or use the script directly
sudo ./build/set-capabilities.sh
```

### Process Management
```bash
# Check running processes
ps aux | grep -E "(quantlib-process|Alaris.Lean)"

# Stop all processes
pkill -f "quantlib-process|Alaris.Lean"
sudo rm -f /dev/shm/alaris_*

# Or use the build system
make -C build clean-shm
```

### Debugging Build System
```bash
# Show detailed build configuration
cmake --build build --target alaris-help

# Check build metadata
cat build/alaris_build_info.txt

# Verbose build output
cmake --build build --verbose
```

---

## System Monitoring

**Real-time status:**
- QuantLib process logs: `build/logs/quantlib.log`
- Lean process output: Terminal display
- Shared memory status: `ls /dev/shm/alaris_*`
- Build information: `cat build/alaris_build_info.txt`

**Success indicators:**
- "IBKR connection established"
- "Market data streaming" 
- "Strategy loaded: delta-neutral volatility arbitrage"
- "Trading signals generated"

**Health checks:**
```bash
# Test IBKR connectivity
make -C build test-ibkr

# Verify system readiness
cmake --build build --target verify-build

# Check shared memory utilization
ls -lah /dev/shm/alaris_*
```

---

## Production Deployment

**Installation:**
```bash
cmake --install build --prefix /opt/alaris
sudo /opt/alaris/bin/alaris-production-setup.sh
```

**System service:**
```bash
# Enable automatic startup
sudo systemctl enable alaris-quantlib.service
sudo systemctl enable alaris-lean.service
```

**Production monitoring:**
```bash
# Status dashboard
sudo systemctl status alaris-quantlib alaris-lean

# Log monitoring
sudo journalctl -u alaris-quantlib -f
sudo journalctl -u alaris-lean -f
```

---

## Getting Help

**Build system help:**
```bash
cmake --build build --target alaris-help
```

**Common commands:**
```bash
# Show all available targets
make -C build help

# Quick development build
make -C build dev

# Full system verification
./verify-build.sh build

# Show system configuration
cat build/alaris_build_info.txt
```

---

*The Alaris system is designed for those who know what they are doing. With that being said, have fun, and be safe!*
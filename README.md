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
- GCC 9.0+ with C++20 support
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

**The build automatically:**
- ✅ Compiles C++ and C# components
- ✅ Sets Linux real-time capabilities (if sudo available)
- ✅ Configures local logging paths
- ✅ Generates startup script with networking detection
- ✅ Creates all necessary scripts and directories

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
sudo ./set-capabilities.sh
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

## Troubleshooting

### Build Issues
```bash
# Clean rebuild
rm -rf build && cmake -S . -B build && cmake --build build
```

### IBKR Connection Issues
- ✅ IB Gateway/TWS running and logged in
- ✅ API enabled in IB settings (Global Config → API → Settings)
- ✅ "Allow connections from localhost only" unchecked (for WSL)
- ✅ Correct host IP in config files

### WSL Networking
```bash
# Find Windows host IP
ip route show default | awk '/default/ {print $3}'

# Update config files
sed -i 's/host: "127.0.0.1"/host: "YOUR_WINDOWS_IP"/g' config/*.yaml
```

### Process Management
```bash
# Check running processes
ps aux | grep -E "(quantlib-process|Alaris.Lean)"

# Stop all processes
pkill -f "quantlib-process|Alaris.Lean"
sudo rm -f /dev/shm/alaris_*
```

---

## System Monitoring

**Real-time status:**
- QuantLib process logs: `build/logs/quantlib.log`
- Lean process output: Terminal display
- Shared memory status: `ls /dev/shm/alaris_*`

**Success indicators:**
- ✅ "IBKR connection established"
- ✅ "Market data streaming" 
- ✅ "Strategy loaded: delta-neutral volatility arbitrage"
- ✅ "Trading signals generated"

---

## Production Deployment

**Installation:**
```bash
cmake --install build --prefix /opt/alaris
/opt/alaris/bin/alaris-production-setup.sh
```

**System service:**
```bash
# Enable automatic startup
sudo systemctl enable alaris-quantlib.service
sudo systemctl enable alaris-lean.service
```

---

*The Alaris system is designed for quantitative trading professionals. Ensure proper risk management and testing before live deployment.*
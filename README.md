# Alaris Trading System

Alaris is a professional-grade, high-performance algorithmic trading system designed for quantitative strategies, particularly in options and volatility arbitrage.

It uses a unique **process-isolated architecture** to achieve maximum performance and stability, combining the strengths of C++ for computation and C# for brokerage interaction.

---

## ► Overview

### What is Alaris?

Alaris is a complete infrastructure for developing and deploying sophisticated trading strategies. It is not just an algorithm, but a full-stack system that includes:

* **A C++ QuantLib Engine**: For high-speed, low-latency financial calculations, including options pricing (ALO engine) and advanced volatility modeling (GARCH).
* **A C# Lean Engine**: For robust portfolio management, order execution, and seamless integration with brokerages like Interactive Brokers.
* **A Lock-Free IPC Bridge**: The two processes communicate in real-time through a high-performance shared memory interface, ensuring minimal overhead.

### Why This Architecture?

This hybrid approach provides significant advantages over monolithic systems:

* **Performance**: Computationally intensive C++ tasks can be optimized with system-level tuning (e.g., real-time priority, CPU pinning) without being hindered by the C# garbage collector.
* **Stability**: The processes are isolated. A failure in the C# brokerage connection layer will not crash the core C++ pricing engine, and vice-versa, making the system more resilient for live trading.
* **Specialization**: It allows for using the best tool for the job: C++ for raw numerical speed and C# for its rich ecosystem and high-level abstractions for brokerage and data management.

---

## ► Getting Started

Follow these steps to build, configure, and run the Alaris system.

### Prerequisites

You must have the following software installed on your system (Ubuntu/Debian recommended):

* **GCC** (version 9.0+ with C++20 support)
* **CMake** (version 3.20+)
* **.NET SDK** (version 8.0+)
* **QuantLib** (`libquantlib0-dev`)
* **YAML-CPP** (`libyaml-cpp-dev`)
* **Interactive Brokers (IBKR)**: TWS or Gateway must be installed and running.

```bash
# Example installation on Ubuntu/Debian
sudo apt-get update
sudo apt-get install -y build-essential cmake libquantlib0-dev libyaml-cpp-dev dotnet-sdk-8.0
```

### Step 1: Build the System

The project uses a clean CMake build process. From the project's root directory:

```bash
# 1. Create a build directory
mkdir build && cd build

# 2. Configure the project
cmake ..

# 3. Compile all C++ and C# components
cmake --build .
```

This will create all executables in the `build/bin/` directory and set up the necessary data folder structure in `build/data/`.

### Step 2: Configure Your Brokerage

Before running the system, you must enter your Interactive Brokers account details.

1.  Open the file: `config/lean_process.yaml`
2.  Edit the `account` field under the `brokerage` section with your IBKR account ID (e.g., `DU1234567` for paper trading).

### Step 3: Populate Historical Data

After a successful build, you need to download historical data from IBKR This step is only required for backward trading and algorithm initialization, and is not needed for forward trading.

1.  **Launch and log in to IB Gateway or TWS.**
2.  From your `build` directory, run the C# application in **download mode**:

    ```bash
    dotnet bin/Alaris.Lean.dll --mode download
    ```

This command instructs the Lean process to connect to IBKR and download the historical data for all symbols defined in `config/lean_process.yaml`, saving it to the `build/data` directory.

### Step 4: Run the System

You are now ready to run the full trading system.

1.  **Ensure IB Gateway or TWS is running.**
2.  From your `build` directory, execute the provided startup script:

    ```bash
    ./start-alaris.sh
    ```

This script handles starting and managing both the C++ and C# processes, allowing them to communicate and begin trading operations.

---

## ► Operational Modes

The C# Lean process can be run in several modes via the `--mode` command-line flag. This is useful for development, testing, and live deployment.

* `--mode download`
    Connects to IBKR to download historical data.

* `--mode backtest`
    Runs the algorithm using the historical data stored locally in `build/data`. Does not connect to the brokerage.

* `--mode paper`
    Connects to an IBKR paper trading account for forward-testing with simulated money.

* `--mode live`
    Connects to an IBKR live trading account. **Use with caution, have fun, and avoid losing all your money!**

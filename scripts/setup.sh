#!/bin/bash

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_step() { echo -e "${BLUE}[STEP]${NC} $1"; }

echo "========================================"
echo "  Alaris Trading System Setup"
echo "========================================"
echo ""

# Create necessary directories for Lean
log_step "Setting up directory structure..."

# Base directories
mkdir -p data/market-hours
mkdir -p data/symbol-properties
mkdir -p data/equity/usa/map_files
mkdir -p data/equity/usa/factor_files
mkdir -p data/option/usa
mkdir -p results
mkdir -p cache

# Create additional data directories for comprehensive coverage
mkdir -p data/forex/fxcm
mkdir -p data/cfd/oanda
mkdir -p data/crypto
mkdir -p data/alternative

log_info "Directory structure created"

# Download essential Lean data files
log_step "Downloading market data files..."

echo "Downloading market hours database..."
if curl -L -f -o data/market-hours/market-hours-database.json \
  "https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/market-hours/market-hours-database.json"; then
    log_info "✓ Market hours database downloaded"
else
    log_error "Failed to download market hours database"
    echo "You can manually download from: https://github.com/QuantConnect/Lean/tree/master/Data/market-hours"
fi

echo "Downloading symbol properties database..."
if curl -L -f -o data/symbol-properties/symbol-properties-database.csv \
  "https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/symbol-properties/symbol-properties-database.csv"; then
    log_info "✓ Symbol properties database downloaded"
else
    log_error "Failed to download symbol properties database"
    echo "You can manually download from: https://github.com/QuantConnect/Lean/tree/master/Data/symbol-properties"
fi

# Create lean.json configuration file
log_step "Creating lean.json configuration..."

cat > lean.json << 'EOF'
{
  "_comment": "Alaris Trading System - QuantConnect Lean Configuration",
  
  "algorithm-type-name": "Alaris.Algorithm.ArbitrageAlgorithm",
  "algorithm-language": "CSharp",
  "algorithm-location": "Alaris.Lean.dll",
  
  "data-directory": "./data/",
  "cache-location": "./cache/", 
  "results-destination-folder": "./results/",
  
  "log-handler": "QuantConnect.Logging.CompositeLogHandler",
  "messaging-handler": "QuantConnect.Messaging.Messaging",
  "job-queue-handler": "QuantConnect.Queues.JobQueue",
  "api-handler": "QuantConnect.Api.Api",
  
  "map-file-provider": "QuantConnect.Data.Auxiliary.LocalDiskMapFileProvider",
  "factor-file-provider": "QuantConnect.Data.Auxiliary.LocalDiskFactorFileProvider",
  "data-provider": "QuantConnect.Lean.Engine.DataFeeds.DefaultDataProvider",
  "object-store": "QuantConnect.Lean.Engine.Storage.LocalObjectStore",
  "data-cache-provider": "QuantConnect.Lean.Engine.DataFeeds.SingleEntryDataCacheProvider",
  "data-permission-manager": "QuantConnect.Data.Auxiliary.DataPermissionManager",
  
  "debug-mode": false,
  "log-level": "Trace",
  "show-missing-data-logs": false,
  
  "maximum-data-points-per-chart-series": 100000,
  "maximum-chart-series": 30,
  "maximum-runtime-minutes": 0,
  "maximum-orders": 0,
  "force-exchange-always-open": true,
  "enable-automatic-indicator-warm-up": false,
  
  "environments": {
    "backtesting": {
      "live-mode": false,
      "setup-handler": "QuantConnect.Lean.Engine.Setup.ConsoleSetupHandler",
      "result-handler": "QuantConnect.Lean.Engine.Results.BacktestingResultHandler",
      "data-feed-handler": "QuantConnect.Lean.Engine.DataFeeds.FileSystemDataFeed", 
      "real-time-handler": "QuantConnect.Lean.Engine.RealTime.BacktestingRealTimeHandler",
      "history-provider": "QuantConnect.Lean.Engine.HistoryProvider.SubscriptionDataReaderHistoryProvider",
      "transaction-handler": "QuantConnect.Lean.Engine.TransactionHandlers.BacktestingTransactionHandler"
    },
    
    "live-trading": {
      "live-mode": true,
      "setup-handler": "QuantConnect.Lean.Engine.Setup.BrokerageSetupHandler",
      "result-handler": "QuantConnect.Lean.Engine.Results.LiveTradingResultHandler",
      "data-feed-handler": "QuantConnect.Lean.Engine.DataFeeds.LiveTradingDataFeed",
      "real-time-handler": "QuantConnect.Lean.Engine.RealTime.LiveTradingRealTimeHandler", 
      "transaction-handler": "QuantConnect.Lean.Engine.TransactionHandlers.BrokerageTransactionHandler",
      
      "live-mode-brokerage": "InteractiveBrokersBrokerage",
      "data-queue-handler": "QuantConnect.Brokerages.InteractiveBrokers.InteractiveBrokersDataQueueHandler",
      "ib-account": "DU123456",
      "ib-user-name": "",
      "ib-password": "",
      "ib-host": "127.0.0.1",
      "ib-port": "4002", 
      "ib-agent-description": "Individual"
    }
  },
  
  "job-user-id": "1",
  "job-project-id": "1",
  "job-organization-id": "1",
  "api-access-token": "",
  
  "alaris": {
    "quantlib-process": {
      "enabled": true,
      "shared-memory-prefix": "alaris",
      "market-data-buffer-size": 4096,
      "signal-buffer-size": 1024,
      "control-buffer-size": 256
    },
    "strategy": {
      "default-mode": "deltaneutral",
      "default-frequency": "minute",
      "risk-management": {
        "max-portfolio-exposure": 0.2,
        "max-daily-loss": 0.02,
        "max-position-size": 0.05
      }
    }
  }
}
EOF

log_info "✓ lean.json configuration created"

# Create basic map files for common symbols
log_step "Creating basic map and factor files..."

# SPY map file
cat > data/equity/usa/map_files/spy.csv << 'EOF'
20120101,SPY,SPY,SPDR S&P 500 ETF Trust
EOF

# QQQ map file 
cat > data/equity/usa/map_files/qqq.csv << 'EOF'
20120101,QQQ,QQQ,Invesco QQQ Trust
EOF

# AAPL map file
cat > data/equity/usa/map_files/aapl.csv << 'EOF'
20120101,AAPL,AAPL,Apple Inc.
EOF

# IWM map file
cat > data/equity/usa/map_files/iwm.csv << 'EOF'
20120101,IWM,IWM,iShares Russell 2000 ETF
EOF

# EFA map file
cat > data/equity/usa/map_files/efa.csv << 'EOF'
20120101,EFA,EFA,iShares MSCI EAFE ETF
EOF

# EEM map file
cat > data/equity/usa/map_files/eem.csv << 'EOF'
20120101,EEM,EEM,iShares MSCI Emerging Markets ETF
EOF

log_info "✓ Map files created for common symbols"

# Create basic factor files (no splits/dividends for simplicity)
for symbol in spy qqq aapl iwm efa eem; do
    cat > "data/equity/usa/factor_files/${symbol}.csv" << 'EOF'
20120101,1.0,1.0
EOF
done

log_info "✓ Factor files created for common symbols"

# Create enhanced security database
log_step "Creating enhanced security database..."

cat > data/symbol-properties/security-database.csv << 'EOF'
Symbol,Market,SecurityType,Name,LotSize,MinimumPriceVariation,PriceMagnifier
SPY,usa,Equity,SPDR S&P 500 ETF Trust,1,0.01,1
AAPL,usa,Equity,Apple Inc.,1,0.01,1
QQQ,usa,Equity,Invesco QQQ Trust,1,0.01,1
IWM,usa,Equity,iShares Russell 2000 ETF,1,0.01,1
EFA,usa,Equity,iShares MSCI EAFE ETF,1,0.01,1
EEM,usa,Equity,iShares MSCI Emerging Markets ETF,1,0.01,1
MSFT,usa,Equity,Microsoft Corporation,1,0.01,1
GOOGL,usa,Equity,Alphabet Inc. Class A,1,0.01,1
AMZN,usa,Equity,Amazon.com Inc.,1,0.01,1
TSLA,usa,Equity,Tesla Inc.,1,0.01,1
META,usa,Equity,Meta Platforms Inc.,1,0.01,1
NVDA,usa,Equity,NVIDIA Corporation,1,0.01,1
JPM,usa,Equity,JPMorgan Chase & Co.,1,0.01,1
JNJ,usa,Equity,Johnson & Johnson,1,0.01,1
V,usa,Equity,Visa Inc.,1,0.01,1
PG,usa,Equity,Procter & Gamble Co.,1,0.01,1
UNH,usa,Equity,UnitedHealth Group Inc.,1,0.01,1
HD,usa,Equity,The Home Depot Inc.,1,0.01,1
MA,usa,Equity,Mastercard Inc.,1,0.01,1
BAC,usa,Equity,Bank of America Corp.,1,0.01,1
EOF

log_info "✓ Enhanced security database created"

# Create sample algorithm config for testing
log_step "Creating sample configurations..."

mkdir -p config

cat > config/algorithm.json << 'EOF'
{
  "alaris": {
    "default_symbol": "SPY",
    "default_strategy": "deltaneutral",
    "default_frequency": "minute",
    "risk_management": {
      "max_portfolio_exposure": 0.2,
      "max_daily_loss": 0.02,
      "max_position_size": 0.05,
      "stop_loss_percent": 0.1,
      "take_profit_percent": 0.2
    },
    "strategy_parameters": {
      "deltaneutral": {
        "delta_threshold": 0.1,
        "gamma_threshold": 0.05,
        "vega_threshold": 0.15,
        "theta_threshold": -0.1
      },
      "gammascalping": {
        "gamma_threshold": 0.1,
        "delta_hedge_frequency": "1H",
        "profit_target": 0.02
      },
      "volatilitytiming": {
        "vol_lookback_days": 20,
        "vol_threshold": 0.25,
        "entry_signal_strength": 0.7
      },
      "relativevalue": {
        "skew_threshold": 0.1,
        "term_structure_threshold": 0.05,
        "correlation_threshold": 0.8
      }
    }
  }
}
EOF

log_info "✓ Sample algorithm configuration created"

# Set proper permissions
log_step "Setting permissions..."

chmod -R 755 data/
chmod -R 755 results/
chmod -R 755 cache/
chmod -R 755 config/
chmod 644 lean.json

log_info "✓ Permissions set"

# Verify setup
log_step "Verifying setup..."

# Check essential files
essential_files=(
    "lean.json"
    "data/market-hours/market-hours-database.json"
    "data/symbol-properties/symbol-properties-database.csv"
    "data/symbol-properties/security-database.csv"
    "data/equity/usa/map_files/spy.csv"
    "data/equity/usa/factor_files/spy.csv"
)

all_good=true
for file in "${essential_files[@]}"; do
    if [[ -f "$file" ]]; then
        log_info "✓ $file"
    else
        log_error "✗ $file"
        all_good=false
    fi
done

echo ""
if [[ "$all_good" == true ]]; then
    log_info "🎉 Alaris setup completed successfully!"
else
    log_warn "Setup completed with some missing files"
fi

echo ""
echo "========================================"
echo "           Setup Summary"
echo "========================================"
echo ""
echo "Files created:"
echo "  📄 lean.json                     - QuantConnect Lean configuration"
echo "  📁 data/                         - Market data directory structure"
echo "  📁 results/                      - Algorithm results output"
echo "  📁 cache/                        - Lean data cache"
echo "  📁 config/                       - Alaris configuration files"
echo ""
echo "Next steps:"
echo "  1. Build the project:    ./scripts/build.sh"
echo "  2. Run a backtest:       ./scripts/run.sh"
echo "  3. Check results in:     ./results/"
echo ""
echo "For live trading:"
echo "  1. Configure IB Gateway settings in lean.json"
echo "  2. Start IB Gateway/TWS"
echo "  3. Run with:             ./scripts/run.sh -m live"
echo ""
echo "For more data (optional):"
echo "  • Download sample data from QuantConnect"
echo "  • Configure additional data providers"
echo "  • Add custom data sources"
echo ""

log_info "Setup script completed!"
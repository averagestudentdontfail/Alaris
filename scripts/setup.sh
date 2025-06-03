#!/bin/bash

# Create necessary directories for Lean
echo "Setting up Lean data structure..."

# Create base directories
mkdir -p data/market-hours
mkdir -p data/symbol-properties
mkdir -p data/equity
mkdir -p results
mkdir -p cache

# Download essential Lean data files
echo "Downloading market hours database..."
curl -L -o data/market-hours/market-hours-database.json \
  "https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/market-hours/market-hours-database.json"

echo "Downloading symbol properties database..."
curl -L -o data/symbol-properties/symbol-properties-database.csv \
  "https://raw.githubusercontent.com/QuantConnect/Lean/master/Data/symbol-properties/symbol-properties-database.csv"

# Create a basic security database file
cat > data/symbol-properties/security-database.csv << 'EOF'
Symbol,Market,SecurityType,Name
SPY,usa,Equity,SPDR S&P 500 ETF Trust
AAPL,usa,Equity,Apple Inc.
QQQ,usa,Equity,Invesco QQQ Trust
IWM,usa,Equity,iShares Russell 2000 ETF
EFA,usa,Equity,iShares MSCI EAFE ETF
EEM,usa,Equity,iShares MSCI Emerging Markets ETF
EOF

# Set proper permissions
chmod -R 755 data/
chmod -R 755 results/
chmod -R 755 cache/

echo "Lean data structure setup complete!"
echo ""
echo "Directory structure created:"
echo "  data/"
echo "    market-hours/"
echo "    symbol-properties/"
echo "    equity/"
echo "  results/"
echo "  cache/"
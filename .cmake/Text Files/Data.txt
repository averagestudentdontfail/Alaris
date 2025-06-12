# .cmake/Data.cmake
# IBKR-Integrated Data Management for Alaris Trading System

# PRODUCTION-FIRST PHILOSOPHY: Default to IBKR real data
# Synthetic data generation exists purely for development and testing workflows

# Data configuration options - UPDATED: IBKR-first approach
option(ALARIS_USE_IBKR_DATA "Use Interactive Brokers for data (production)" ON)
option(ALARIS_DOWNLOAD_HISTORICAL "Download historical data via IBKR API" ON)
option(ALARIS_ENABLE_REALTIME "Enable real-time data streaming via IBKR API" ON)
option(ALARIS_MINIMAL_DATA "Use minimal data set (faster setup)" OFF)
option(ALARIS_CREATE_SAMPLE_DATA "Create synthetic data for development" OFF)
option(ALARIS_AUTO_SETUP_DATA "Automatically set up data during build process" ON)
option(ALARIS_DEVELOPMENT_MODE "Enable development-friendly defaults" OFF)

# Development mode overrides - when enabled, falls back to synthetic data
if(ALARIS_DEVELOPMENT_MODE)
    set(ALARIS_USE_IBKR_DATA OFF CACHE BOOL "Disable IBKR for development" FORCE)
    set(ALARIS_CREATE_SAMPLE_DATA ON CACHE BOOL "Create sample data for development" FORCE)
    set(ALARIS_AUTO_SETUP_DATA ON CACHE BOOL "Auto-setup for development" FORCE)
    message(STATUS "Data: Development mode enabled - using synthetic data instead of IBKR")
endif()

# IBKR Configuration
set(IBKR_GATEWAY_HOST "127.0.0.1")
set(IBKR_GATEWAY_PORT_PAPER "4002")
set(IBKR_GATEWAY_PORT_LIVE "4001")
set(IBKR_CLIENT_ID "999")  # Unique client ID for data operations

# Data resolution configuration
set(BACKTEST_RESOLUTION "1 day")
set(BACKTEST_DURATION "1 Y")           # 1 year for backtesting
set(FORWARD_TEST_RESOLUTION "1 sec")   # 1 second for forward testing
set(FORWARD_TEST_DURATION "1 D")       # 1 day buffer for real-time

# UPDATED: Professional symbol universe (25 symbols across sectors)
set(PRODUCTION_SYMBOLS_ETFS
    "SPY"   # SPDR S&P 500 ETF Trust
    "QQQ"   # Invesco QQQ Trust (NASDAQ)
    "IWM"   # iShares Russell 2000 ETF
    "EFA"   # iShares MSCI EAFE ETF (International)
    "VTI"   # Vanguard Total Stock Market ETF
)

set(PRODUCTION_SYMBOLS_TECH
    "AAPL"  # Apple Inc.
    "MSFT"  # Microsoft Corporation
    "GOOGL" # Alphabet Inc. Class A
    "AMZN"  # Amazon.com Inc.
    "NVDA"  # NVIDIA Corporation
)

set(PRODUCTION_SYMBOLS_FINANCIAL
    "JPM"   # JPMorgan Chase & Co.
    "BAC"   # Bank of America Corp.
    "WFC"   # Wells Fargo & Company
    "GS"    # The Goldman Sachs Group
    "MS"    # Morgan Stanley
)

set(PRODUCTION_SYMBOLS_ENERGY
    "XOM"   # Exxon Mobil Corporation
    "CVX"   # Chevron Corporation
    "COP"   # ConocoPhillips
    "EOG"   # EOG Resources Inc.
    "SLB"   # Schlumberger Limited
)

set(PRODUCTION_SYMBOLS_HEALTHCARE
    "JNJ"   # Johnson & Johnson
    "PFE"   # Pfizer Inc.
    "UNH"   # UnitedHealth Group Inc.
    "ABBV"  # AbbVie Inc.
    "MRK"   # Merck & Co. Inc.
)

# Combine all production symbols
set(PRODUCTION_SYMBOLS
    ${PRODUCTION_SYMBOLS_ETFS}
    ${PRODUCTION_SYMBOLS_TECH}
    ${PRODUCTION_SYMBOLS_FINANCIAL}
    ${PRODUCTION_SYMBOLS_ENERGY}
    ${PRODUCTION_SYMBOLS_HEALTHCARE}
)

# Essential symbols for minimal setup (development)
set(ESSENTIAL_SYMBOLS
    "SPY"   # S&P 500 ETF
    "QQQ"   # NASDAQ ETF
    "AAPL"  # Major tech stock
    "JPM"   # Major financial
    "XOM"   # Major energy
)

# Global variables for data paths
set(ALARIS_DATA_DIR "${CMAKE_BINARY_DIR}/data")
set(ALARIS_RESULTS_DIR "${CMAKE_BINARY_DIR}/results")
set(ALARIS_CACHE_DIR "${CMAKE_BINARY_DIR}/cache")
set(ALARIS_IBKR_DATA_DIR "${ALARIS_DATA_DIR}/ibkr")

# Function to create IBKR data fetcher Python script
function(create_ibkr_data_fetcher)
    set(IBKR_FETCHER_SCRIPT "${CMAKE_BINARY_DIR}/fetch_ibkr_data.py")
    
    set(FETCHER_CONTENT "#!/usr/bin/env python3
\"\"\"
Alaris IBKR Data Fetcher
Automatically downloads historical data and sets up real-time streaming
\"\"\"

import os
import sys
import time
import logging
import argparse
from datetime import datetime, timedelta
from typing import List, Dict
import threading
import queue

try:
    from ib_insync import IB, Stock, Contract, util
    import pandas as pd
except ImportError:
    print(\"Error: Required packages not installed\")
    print(\"Install with: pip install ib_insync pandas\")
    sys.exit(1)

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

class AlarisIBKRDataFetcher:
    def __init__(self, host='${IBKR_GATEWAY_HOST}', port=${IBKR_GATEWAY_PORT_PAPER}, client_id=${IBKR_CLIENT_ID}):
        self.ib = IB()
        self.host = host
        self.port = port
        self.client_id = client_id
        self.data_dir = '${ALARIS_IBKR_DATA_DIR}'
        self.connected = False
        
        # Symbol configuration
        self.symbols = [
            # ETFs
            'SPY', 'QQQ', 'IWM', 'EFA', 'VTI',
            # Tech
            'AAPL', 'MSFT', 'GOOGL', 'AMZN', 'NVDA', 
            # Financial
            'JPM', 'BAC', 'WFC', 'GS', 'MS',
            # Energy  
            'XOM', 'CVX', 'COP', 'EOG', 'SLB',
            # Healthcare
            'JNJ', 'PFE', 'UNH', 'ABBV', 'MRK'
        ]
        
        # Create data directories
        os.makedirs(self.data_dir, exist_ok=True)
        os.makedirs(f'{self.data_dir}/historical', exist_ok=True)
        os.makedirs(f'{self.data_dir}/realtime', exist_ok=True)
        
    def connect(self) -> bool:
        \"\"\"Connect to IB Gateway\"\"\"
        try:
            self.ib.connect(self.host, self.port, clientId=self.client_id, timeout=30)
            self.connected = True
            logger.info(f\"Connected to IB Gateway at {self.host}:{self.port}\")
            return True
        except Exception as e:
            logger.error(f\"Failed to connect to IB Gateway: {e}\")
            logger.error(\"Make sure IB Gateway is running and configured for API access\")
            return False
    
    def disconnect(self):
        \"\"\"Disconnect from IB Gateway\"\"\"
        if self.connected:
            self.ib.disconnect()
            self.connected = False
            logger.info(\"Disconnected from IB Gateway\")
    
    def create_contract(self, symbol: str) -> Contract:
        \"\"\"Create stock contract for symbol\"\"\"
        return Stock(symbol, 'SMART', 'USD')
    
    def fetch_historical_data(self, mode='backtest') -> bool:
        \"\"\"Fetch historical data for all symbols\"\"\"
        if not self.connected:
            logger.error(\"Not connected to IB Gateway\")
            return False
        
        # Configure based on mode
        if mode == 'backtest':
            duration = '${BACKTEST_DURATION}'
            bar_size = '${BACKTEST_RESOLUTION}'
            what_to_show = 'TRADES'
        else:  # forward test
            duration = '${FORWARD_TEST_DURATION}' 
            bar_size = '${FORWARD_TEST_RESOLUTION}'
            what_to_show = 'TRADES'
        
        logger.info(f\"Fetching historical data ({mode} mode): {duration} of {bar_size} bars\")
        
        successful_downloads = 0
        total_symbols = len(self.symbols)
        
        for i, symbol in enumerate(self.symbols, 1):
            try:
                logger.info(f\"[{i}/{total_symbols}] Fetching {symbol}...\")
                
                contract = self.create_contract(symbol)
                
                # Request historical data with rate limiting
                bars = self.ib.reqHistoricalData(
                    contract,
                    endDateTime='',
                    durationStr=duration,
                    barSizeSetting=bar_size,
                    whatToShow=what_to_show,
                    useRTH=True,
                    formatDate=1
                )
                
                if bars:
                    # Convert to DataFrame
                    df = util.df(bars)
                    
                    # Save to CSV in Lean format
                    filename = f\"{self.data_dir}/historical/{symbol.lower()}_{mode}.csv\"
                    
                    # Format for QuantConnect Lean: DateTime,Open,High,Low,Close,Volume
                    df_lean = df[['date', 'open', 'high', 'low', 'close', 'volume']].copy()
                    df_lean['date'] = pd.to_datetime(df_lean['date']).dt.strftime('%Y%m%d %H:%M')
                    
                    df_lean.to_csv(filename, index=False, header=False)
                    
                    logger.info(f\"✓ {symbol}: {len(df)} bars saved to {filename}\")
                    successful_downloads += 1
                    
                    # Rate limiting - IBKR allows max 60 requests per 10 minutes
                    if i < total_symbols:  # Don't sleep after last symbol
                        time.sleep(10.5)  # 10.5 seconds between requests = ~57 requests per 10 minutes
                        
                else:
                    logger.warning(f\"✗ {symbol}: No data received\")
                    
            except Exception as e:
                logger.error(f\"✗ {symbol}: Error fetching data - {e}\")
                continue
        
        logger.info(f\"Historical data fetch complete: {successful_downloads}/{total_symbols} symbols\")
        return successful_downloads > 0
    
    def setup_realtime_streaming(self) -> bool:
        \"\"\"Set up real-time data streaming\"\"\"
        if not self.connected:
            logger.error(\"Not connected to IB Gateway\") 
            return False
        
        logger.info(\"Setting up real-time data streaming...\")
        
        # Subscribe to real-time data for all symbols
        contracts = []
        for symbol in self.symbols:
            contract = self.create_contract(symbol)
            contracts.append(contract)
            
            # Request market data
            self.ib.reqMktData(contract, '', False, False)
            logger.info(f\"✓ Subscribed to real-time data for {symbol}\")
        
        # Set up tick data handler
        def on_tick_data(ticker):
            \"\"\"Handle incoming tick data\"\"\"
            try:
                symbol = ticker.contract.symbol
                timestamp = datetime.now().strftime('%Y%m%d %H:%M:%S')
                
                # Save tick data to CSV
                tick_file = f\"{self.data_dir}/realtime/{symbol.lower()}_ticks.csv\"
                
                with open(tick_file, 'a') as f:
                    f.write(f\"{timestamp},{ticker.last},{ticker.bid},{ticker.ask},{ticker.volume}\\n\")
                    
            except Exception as e:
                logger.error(f\"Error handling tick data: {e}\")
        
        # Register tick handler
        self.ib.pendingTickersEvent += on_tick_data
        
        logger.info(f\"Real-time streaming active for {len(self.symbols)} symbols\")
        logger.info(\"Tick data will be saved to: {}/realtime/\".format(self.data_dir))
        
        return True
    
    def create_lean_data_structure(self):
        \"\"\"Create QuantConnect Lean compatible data structure\"\"\"
        logger.info(\"Creating Lean-compatible data structure...\")
        
        lean_data_dir = '${ALARIS_DATA_DIR}/equity/usa/daily'
        os.makedirs(lean_data_dir, exist_ok=True)
        
        for symbol in self.symbols:
            symbol_lower = symbol.lower()
            symbol_dir = f\"{lean_data_dir}/{symbol_lower}\"
            os.makedirs(symbol_dir, exist_ok=True)
            
            # Copy historical data to Lean format
            historical_file = f\"{self.data_dir}/historical/{symbol_lower}_backtest.csv\"
            if os.path.exists(historical_file):
                lean_file = f\"{symbol_dir}/20230101_20241231_trade.csv\"
                
                # Read and reformat for Lean
                df = pd.read_csv(historical_file, header=None, 
                               names=['date', 'open', 'high', 'low', 'close', 'volume'])
                
                # Ensure Lean format: YYYYMMDD HH:MM,O,H,L,C,V
                df.to_csv(lean_file, index=False, header=False)
                logger.info(f\"✓ Created Lean data file: {lean_file}\")
        
        logger.info(\"Lean data structure created successfully\")

def main():
    parser = argparse.ArgumentParser(description='Alaris IBKR Data Fetcher')
    parser.add_argument('--mode', choices=['backtest', 'forward', 'realtime'], 
                       default='backtest', help='Data fetching mode')
    parser.add_argument('--host', default='${IBKR_GATEWAY_HOST}', 
                       help='IB Gateway host')
    parser.add_argument('--port', type=int, default=${IBKR_GATEWAY_PORT_PAPER}, 
                       help='IB Gateway port')
    parser.add_argument('--duration', type=int, default=300, 
                       help='Real-time streaming duration (seconds)')
    
    args = parser.parse_args()
    
    fetcher = AlarisIBKRDataFetcher(args.host, args.port)
    
    try:
        if not fetcher.connect():
            logger.error(\"Failed to connect to IB Gateway\")
            logger.error(\"Please ensure:\")
            logger.error(\"1. IB Gateway is running\")
            logger.error(\"2. API access is enabled\")
            logger.error(\"3. Port configuration is correct\")
            return 1
        
        if args.mode in ['backtest', 'forward']:
            # Fetch historical data
            success = fetcher.fetch_historical_data(args.mode)
            if success:
                fetcher.create_lean_data_structure()
                logger.info(f\"✓ Historical data fetch completed successfully ({args.mode} mode)\")
            else:
                logger.error(\"Historical data fetch failed\")
                return 1
                
        elif args.mode == 'realtime':
            # Set up real-time streaming
            if fetcher.setup_realtime_streaming():
                logger.info(f\"Real-time streaming for {args.duration} seconds...\")
                time.sleep(args.duration)
                logger.info(\"Real-time streaming completed\")
            else:
                logger.error(\"Failed to set up real-time streaming\")
                return 1
        
    except KeyboardInterrupt:
        logger.info(\"Interrupted by user\")
    except Exception as e:
        logger.error(f\"Unexpected error: {e}\")
        return 1
    finally:
        fetcher.disconnect()
    
    return 0

if __name__ == '__main__':
    sys.exit(main())
")
    
    file(WRITE "${IBKR_FETCHER_SCRIPT}" "${FETCHER_CONTENT}")
    
    # Make script executable
    execute_process(
        COMMAND chmod +x "${IBKR_FETCHER_SCRIPT}"
        ERROR_QUIET
    )
    
    message(STATUS "Data: Created IBKR data fetcher: ${IBKR_FETCHER_SCRIPT}")
endfunction()

# Function to create directory structure with IBKR support
function(create_data_directories)
    set(DATA_DIRS
        "${ALARIS_DATA_DIR}"
        "${ALARIS_DATA_DIR}/market-hours"
        "${ALARIS_DATA_DIR}/symbol-properties"
        "${ALARIS_DATA_DIR}/equity/usa/map_files"
        "${ALARIS_DATA_DIR}/equity/usa/factor_files"
        "${ALARIS_DATA_DIR}/equity/usa/daily"
        "${ALARIS_DATA_DIR}/option/usa"
        "${ALARIS_IBKR_DATA_DIR}"
        "${ALARIS_IBKR_DATA_DIR}/historical"
        "${ALARIS_IBKR_DATA_DIR}/realtime"
        "${ALARIS_RESULTS_DIR}"
        "${ALARIS_CACHE_DIR}"
    )
    
    foreach(dir ${DATA_DIRS})
        file(MAKE_DIRECTORY "${dir}")
    endforeach()
    
    message(STATUS "Data: Created directory structure with IBKR support")
endfunction()

# Function to create enhanced lean.json with IBKR integration
function(create_lean_config_file)
    set(LEAN_CONFIG_PATH "${CMAKE_BINARY_DIR}/lean.json")
    
    # Skip if file exists and is not in development mode
    if(EXISTS "${LEAN_CONFIG_PATH}" AND NOT ALARIS_DEVELOPMENT_MODE)
        message(STATUS "Data: lean.json already exists")
        return()
    endif()
    
    # Configuration based on data source
    if(ALARIS_USE_IBKR_DATA)
        set(CONFIG_COMMENT "Alaris Trading System - IBKR Production Configuration")
        set(DEBUG_MODE "false")
        set(LOG_LEVEL "Info")
        set(DATA_FEED_HANDLER "QuantConnect.Brokerages.InteractiveBrokers.InteractiveBrokersDataQueueHandler")
        set(BROKERAGE_NAME "InteractiveBrokersBrokerage")
    else()
        set(CONFIG_COMMENT "Alaris Trading System - Development Configuration")
        set(DEBUG_MODE "true") 
        set(LOG_LEVEL "Debug")
        set(DATA_FEED_HANDLER "QuantConnect.Lean.Engine.DataFeeds.FileSystemDataFeed")
        set(BROKERAGE_NAME "PaperTradingBrokerage")
    endif()
    
    set(LEAN_CONFIG_CONTENT "{
  \"_comment\": \"${CONFIG_COMMENT}\",
  
  \"algorithm-type-name\": \"Alaris.Algorithm.ArbitrageAlgorithm\",
  \"algorithm-language\": \"CSharp\",
  \"algorithm-location\": \"${CMAKE_BINARY_DIR}/csharp/Alaris.Lean.dll\",
  
  \"data-directory\": \"${ALARIS_DATA_DIR}/\",
  \"cache-location\": \"${ALARIS_CACHE_DIR}/\",
  \"results-destination-folder\": \"${ALARIS_RESULTS_DIR}/\",
  
  \"log-handler\": \"QuantConnect.Logging.CompositeLogHandler\",
  \"messaging-handler\": \"QuantConnect.Messaging.Messaging\",
  \"job-queue-handler\": \"QuantConnect.Queues.JobQueue\",
  \"api-handler\": \"QuantConnect.Api.Api\",
  
  \"map-file-provider\": \"QuantConnect.Data.Auxiliary.LocalDiskMapFileProvider\",
  \"factor-file-provider\": \"QuantConnect.Data.Auxiliary.LocalDiskFactorFileProvider\",
  \"data-provider\": \"QuantConnect.Lean.Engine.DataFeeds.DefaultDataProvider\",
  \"object-store\": \"QuantConnect.Lean.Engine.Storage.LocalObjectStore\",
  \"data-cache-provider\": \"QuantConnect.Lean.Engine.DataFeeds.SingleEntryDataCacheProvider\",
  \"data-permission-manager\": \"QuantConnect.Data.Auxiliary.DataPermissionManager\",
  
  \"debug-mode\": ${DEBUG_MODE},
  \"log-level\": \"${LOG_LEVEL}\",
  \"show-missing-data-logs\": true,
  
  \"maximum-data-points-per-chart-series\": 100000,
  \"maximum-chart-series\": 30,
  \"maximum-runtime-minutes\": 0,
  \"maximum-orders\": 0,
  \"force-exchange-always-open\": false,
  \"enable-automatic-indicator-warm-up\": true,
  
  \"environments\": {
    \"backtesting\": {
      \"live-mode\": false,
      \"setup-handler\": \"QuantConnect.Lean.Engine.Setup.ConsoleSetupHandler\",
      \"result-handler\": \"QuantConnect.Lean.Engine.Results.BacktestingResultHandler\",
      \"data-feed-handler\": \"${DATA_FEED_HANDLER}\",
      \"real-time-handler\": \"QuantConnect.Lean.Engine.RealTime.BacktestingRealTimeHandler\",
      \"history-provider\": \"QuantConnect.Lean.Engine.HistoryProvider.SubscriptionDataReaderHistoryProvider\",
      \"transaction-handler\": \"QuantConnect.Lean.Engine.TransactionHandlers.BacktestingTransactionHandler\"
    },
    
    \"live-trading\": {
      \"live-mode\": true,
      \"setup-handler\": \"QuantConnect.Lean.Engine.Setup.BrokerageSetupHandler\",
      \"result-handler\": \"QuantConnect.Lean.Engine.Results.LiveTradingResultHandler\",
      \"data-feed-handler\": \"QuantConnect.Lean.Engine.DataFeeds.LiveTradingDataFeed\",
      \"real-time-handler\": \"QuantConnect.Lean.Engine.RealTime.LiveTradingRealTimeHandler\",
      \"transaction-handler\": \"QuantConnect.Lean.Engine.TransactionHandlers.BrokerageTransactionHandler\",
      
      \"live-mode-brokerage\": \"${BROKERAGE_NAME}\",
      \"data-queue-handler\": \"QuantConnect.Brokerages.InteractiveBrokers.InteractiveBrokersDataQueueHandler\",
      \"ib-account\": \"DU123456\",
      \"ib-user-name\": \"\",
      \"ib-password\": \"\",
      \"ib-host\": \"${IBKR_GATEWAY_HOST}\",
      \"ib-port\": \"${IBKR_GATEWAY_PORT_PAPER}\",
      \"ib-agent-description\": \"Individual\"
    }
  },
  
  \"job-user-id\": \"1\",
  \"job-project-id\": \"1\",  
  \"job-organization-id\": \"1\",
  \"api-access-token\": \"\",
  
  \"alaris\": {
    \"mode\": \"${ALARIS_USE_IBKR_DATA}_production\",
    \"data-source\": \"${ALARIS_USE_IBKR_DATA}\",
    \"backtest-resolution\": \"${BACKTEST_RESOLUTION}\",
    \"forward-test-resolution\": \"${FORWARD_TEST_RESOLUTION}\",
    \"symbol-universe\": \"professional_25\",
    \"quantlib-process\": {
      \"enabled\": true,
      \"shared-memory-prefix\": \"alaris\",
      \"market-data-buffer-size\": 4096,
      \"signal-buffer-size\": 1024,
      \"control-buffer-size\": 256
    },
    \"strategy\": {
      \"default-mode\": \"deltaneutral\",
      \"default-frequency\": \"minute\",
      \"risk-management\": {
        \"max-portfolio-exposure\": 0.2,
        \"max-daily-loss\": 0.02,
        \"max-position-size\": 0.05
      }
    },
    \"ibkr\": {
      \"gateway-host\": \"${IBKR_GATEWAY_HOST}\",
      \"paper-port\": ${IBKR_GATEWAY_PORT_PAPER},
      \"live-port\": ${IBKR_GATEWAY_PORT_LIVE},
      \"client-id\": ${IBKR_CLIENT_ID},
      \"data-directory\": \"${ALARIS_IBKR_DATA_DIR}\"
    }
  }
}")

    file(WRITE "${LEAN_CONFIG_PATH}" "${LEAN_CONFIG_CONTENT}")
    
    if(ALARIS_USE_IBKR_DATA)
        message(STATUS "Data: ✓ lean.json configuration created for IBKR production")
    else()
        message(STATUS "Data: ✓ lean.json configuration created for development mode")
    endif()
endfunction()

# Function to create symbol properties with all 25 symbols
function(create_symbol_properties_database)
    set(SYMBOL_PROPS_PATH "${ALARIS_DATA_DIR}/symbol-properties/security-database.csv")
    
    # Skip if file exists
    if(EXISTS "${SYMBOL_PROPS_PATH}")
        message(STATUS "Data: security-database.csv already exists")
        return()
    endif()
    
    set(SYMBOL_PROPS_HEADER "Symbol,Market,SecurityType,Name,LotSize,MinimumPriceVariation,PriceMagnifier")
    
    # Enhanced symbol name mappings for all 25 symbols
    set(SYMBOL_NAMES_SPY "SPDR S&P 500 ETF Trust")
    set(SYMBOL_NAMES_QQQ "Invesco QQQ Trust")
    set(SYMBOL_NAMES_IWM "iShares Russell 2000 ETF")
    set(SYMBOL_NAMES_EFA "iShares MSCI EAFE ETF")
    set(SYMBOL_NAMES_VTI "Vanguard Total Stock Market ETF")
    set(SYMBOL_NAMES_AAPL "Apple Inc.")
    set(SYMBOL_NAMES_MSFT "Microsoft Corporation")
    set(SYMBOL_NAMES_GOOGL "Alphabet Inc. Class A")
    set(SYMBOL_NAMES_AMZN "Amazon.com Inc.")
    set(SYMBOL_NAMES_NVDA "NVIDIA Corporation")
    set(SYMBOL_NAMES_JPM "JPMorgan Chase & Co.")
    set(SYMBOL_NAMES_BAC "Bank of America Corp.")
    set(SYMBOL_NAMES_WFC "Wells Fargo & Company")
    set(SYMBOL_NAMES_GS "The Goldman Sachs Group")
    set(SYMBOL_NAMES_MS "Morgan Stanley")
    set(SYMBOL_NAMES_XOM "Exxon Mobil Corporation")
    set(SYMBOL_NAMES_CVX "Chevron Corporation")
    set(SYMBOL_NAMES_COP "ConocoPhillips")
    set(SYMBOL_NAMES_EOG "EOG Resources Inc.")
    set(SYMBOL_NAMES_SLB "Schlumberger Limited")
    set(SYMBOL_NAMES_JNJ "Johnson & Johnson")
    set(SYMBOL_NAMES_PFE "Pfizer Inc.")
    set(SYMBOL_NAMES_UNH "UnitedHealth Group Inc.")
    set(SYMBOL_NAMES_ABBV "AbbVie Inc.")
    set(SYMBOL_NAMES_MRK "Merck & Co. Inc.")
    
    set(SYMBOL_PROPS_CONTENT "${SYMBOL_PROPS_HEADER}\n")
    
    foreach(symbol ${PRODUCTION_SYMBOLS})
        set(symbol_name_var "SYMBOL_NAMES_${symbol}")
        if(DEFINED ${symbol_name_var})
            set(symbol_name "${${symbol_name_var}}")
        else()
            set(symbol_name "${symbol}")
        endif()
        
        string(APPEND SYMBOL_PROPS_CONTENT "${symbol},usa,Equity,${symbol_name},1,0.01,1\n")
    endforeach()
    
    file(WRITE "${SYMBOL_PROPS_PATH}" "${SYMBOL_PROPS_CONTENT}")
    
    list(LENGTH PRODUCTION_SYMBOLS symbol_count)
    message(STATUS "Data: ✓ Created security database with ${symbol_count} professional symbols")
endfunction()

# Function to create map files for all symbols
function(create_map_files)
    # Use all 25 production symbols
    set(SYMBOLS_TO_PROCESS ${PRODUCTION_SYMBOLS})
    
    foreach(symbol ${SYMBOLS_TO_PROCESS})
        string(TOLOWER "${symbol}" symbol_lower)
        set(MAP_FILE_PATH "${ALARIS_DATA_DIR}/equity/usa/map_files/${symbol_lower}.csv")
        set(FACTOR_FILE_PATH "${ALARIS_DATA_DIR}/equity/usa/factor_files/${symbol_lower}.csv")
        
        # Create basic map file (no corporate actions for simplicity)
        if(NOT EXISTS "${MAP_FILE_PATH}")
            file(WRITE "${MAP_FILE_PATH}" "20120101,${symbol},${symbol},${symbol}\n")
        endif()
        
        # Create corresponding factor file (no splits/dividends)
        if(NOT EXISTS "${FACTOR_FILE_PATH}")
            file(WRITE "${FACTOR_FILE_PATH}" "20120101,1.0,1.0\n")
        endif()
    endforeach()
    
    list(LENGTH SYMBOLS_TO_PROCESS symbol_count)
    message(STATUS "Data: ✓ Created map and factor files for ${symbol_count} symbols")
endfunction()

# Function to create synthetic data for development (fallback only)
function(create_sample_data)
    if(NOT ALARIS_CREATE_SAMPLE_DATA)
        return()
    endif()
    
    message(STATUS "Data: Creating synthetic historical data for development/testing...")
    message(STATUS "Data: ⚠️  WARNING: This is synthetic data for development only!")
    message(STATUS "Data: ⚠️  For production, use IBKR data fetching!")
    
    # Use essential symbols for development
    set(SYMBOLS_TO_PROCESS ${ESSENTIAL_SYMBOLS})
    
    # Create sample daily data for symbols
    foreach(symbol ${SYMBOLS_TO_PROCESS})
        string(TOLOWER "${symbol}" symbol_lower)
        set(sample_data_dir "${ALARIS_DATA_DIR}/equity/usa/daily/${symbol_lower}")
        file(MAKE_DIRECTORY "${sample_data_dir}")
        
        # Create realistic CSV data that Lean can read
        set(sample_csv_file "${sample_data_dir}/20240101_20241231_trade.csv")
        
        # Generate realistic price data
        create_realistic_price_data("${sample_csv_file}" "${symbol}")
        
        message(STATUS "Data: ✓ Created synthetic data for ${symbol}")
    endforeach()
    
    list(LENGTH SYMBOLS_TO_PROCESS symbol_count)
    message(STATUS "Data: ✓ Created synthetic data for ${symbol_count} symbols")
    message(STATUS "Data: ⚠️  Remember: This is development data only!")
endfunction()

# Function to create realistic price data (unchanged)
function(create_realistic_price_data OUTPUT_FILE SYMBOL)
    # Same implementation as before...
    if(SYMBOL STREQUAL "SPY")
        set(BASE_PRICE "400.00")
        set(VOLATILITY "LOW")
    elseif(SYMBOL STREQUAL "QQQ")
        set(BASE_PRICE "350.00")
        set(VOLATILITY "MEDIUM")
    elseif(SYMBOL STREQUAL "AAPL")
        set(BASE_PRICE "180.00")
        set(VOLATILITY "HIGH")
    elseif(SYMBOL STREQUAL "JPM")
        set(BASE_PRICE "150.00")
        set(VOLATILITY "MEDIUM")
    elseif(SYMBOL STREQUAL "XOM")
        set(BASE_PRICE "110.00")
        set(VOLATILITY "HIGH")
    else()
        set(BASE_PRICE "100.00")
        set(VOLATILITY "MEDIUM")
    endif()
    
    set(CSV_CONTENT "")
    
    # Generate approximately 250 trading days for 2024
    foreach(day_num RANGE 1 250)
        math(EXPR month_num "(${day_num} / 22) + 1")
        math(EXPR day_in_month "(${day_num} % 22) + 1")
        
        if(month_num GREATER 12)
            set(month_num 12)
        endif()
        
        if(month_num LESS 10)
            set(month_str "0${month_num}")
        else()
            set(month_str "${month_num}")
        endif()
        
        if(day_in_month LESS 10)
            set(day_str "0${day_in_month}")
        else()
            set(day_str "${day_in_month}")
        endif()
        
        set(date_str "2024${month_str}${day_str}")
        
        # Simple price variation
        math(EXPR price_var "${day_num} % 10")
        if(price_var GREATER 5)
            set(price_direction "+")
        else()
            set(price_direction "-")
        endif()
        
        # Create OHLC data (simplified)
        set(open_price "${BASE_PRICE}")
        set(high_price "${BASE_PRICE}")  
        set(low_price "${BASE_PRICE}")
        set(close_price "${BASE_PRICE}")
        set(volume "1000000")
        
        # Add to CSV content (Lean format: DateTime,Open,High,Low,Close,Volume)
        string(APPEND CSV_CONTENT "${date_str} 00:00,${open_price},${high_price},${low_price},${close_price},${volume}\n")
    endforeach()
    
    # Write the CSV file
    file(WRITE "${OUTPUT_FILE}" "${CSV_CONTENT}")
endfunction()

# Main data setup function
function(setup_alaris_data)
    if(ALARIS_USE_IBKR_DATA)
        message(STATUS "Data: Setting up Alaris IBKR data environment (PRODUCTION)...")
    else()
        message(STATUS "Data: Setting up Alaris development data environment (SYNTHETIC)...")
    endif()
    
    # Create directory structure
    create_data_directories()
    
    # Create IBKR data fetcher
    if(ALARIS_USE_IBKR_DATA)
        create_ibkr_data_fetcher()
    endif()
    
    # Create configuration files
    create_lean_config_file()
    
    # Create symbol-specific files
    create_map_files()
    create_symbol_properties_database()
    
    # Create sample data only if explicitly enabled (development mode)
    create_sample_data()
    
    # Print summary
    list(LENGTH PRODUCTION_SYMBOLS production_count)
    
    if(ALARIS_USE_IBKR_DATA)
        message(STATUS "Data: ✓ IBKR production data setup completed (${production_count} symbols)")
        message(STATUS "Data: ✓ IBKR data fetcher created: ${CMAKE_BINARY_DIR}/fetch_ibkr_data.py")
        message(STATUS "Data: ✓ Ready for automated historical and real-time data collection")
    else()
        message(STATUS "Data: ✓ Development data setup completed")
        if(ALARIS_CREATE_SAMPLE_DATA)
            message(STATUS "Data: ✓ Synthetic historical data created for development/testing")
            message(STATUS "Data: ⚠️  WARNING: Synthetic data is for development only!")
        endif()
    endif()
    
    message(STATUS "Data: Location: ${ALARIS_DATA_DIR}")
    message(STATUS "Data: lean.json: ${CMAKE_BINARY_DIR}/lean.json")
endfunction()

# Enhanced data targets with IBKR support
function(create_data_targets)
    # Target to set up data environment
    add_custom_target(setup-data
        COMMAND ${CMAKE_COMMAND} -E echo "Setting up Alaris data environment..."
        COMMAND ${CMAKE_COMMAND} 
            -DALARIS_DATA_DIR="${ALARIS_DATA_DIR}"
            -DALARIS_RESULTS_DIR="${ALARIS_RESULTS_DIR}"
            -DALARIS_CACHE_DIR="${ALARIS_CACHE_DIR}"
            -DALARIS_USE_IBKR_DATA=${ALARIS_USE_IBKR_DATA}
            -DALARIS_CREATE_SAMPLE_DATA=${ALARIS_CREATE_SAMPLE_DATA}
            -DALARIS_DEVELOPMENT_MODE=${ALARIS_DEVELOPMENT_MODE}
            -DCMAKE_BINARY_DIR="${CMAKE_BINARY_DIR}"
            -P "${CMAKE_CURRENT_SOURCE_DIR}/.cmake/DataInlineSetup.cmake"
        COMMENT "Setting up Alaris data environment"
        VERBATIM
    )
    
    # IBKR-specific targets
    if(ALARIS_USE_IBKR_DATA OR NOT ALARIS_DEVELOPMENT_MODE)
        # Target to fetch historical data for backtesting
        add_custom_target(fetch-backtest-data
            COMMAND ${CMAKE_COMMAND} -E echo "Fetching historical data for backtesting (1 year, daily)..."
            COMMAND python3 "${CMAKE_BINARY_DIR}/fetch_ibkr_data.py" --mode backtest
            DEPENDS setup-data
            COMMENT "Fetching IBKR historical data for backtesting"
            VERBATIM
        )
        
        # Target to fetch historical data for forward testing
        add_custom_target(fetch-forward-data
            COMMAND ${CMAKE_COMMAND} -E echo "Fetching recent data for forward testing (1 day, 1 second)..."
            COMMAND python3 "${CMAKE_BINARY_DIR}/fetch_ibkr_data.py" --mode forward
            DEPENDS setup-data
            COMMENT "Fetching IBKR recent data for forward testing"
            VERBATIM
        )
        
        # Target to start real-time data streaming
        add_custom_target(start-realtime-data
            COMMAND ${CMAKE_COMMAND} -E echo "Starting real-time data streaming..."
            COMMAND python3 "${CMAKE_BINARY_DIR}/fetch_ibkr_data.py" --mode realtime --duration 3600
            DEPENDS setup-data
            COMMENT "Starting IBKR real-time data streaming (1 hour)"
            VERBATIM
        )
        
        # Target to fetch all data (backtest + forward)
        add_custom_target(fetch-all-data
            DEPENDS fetch-backtest-data fetch-forward-data
            COMMENT "Fetching all IBKR historical data"
        )
    endif()
    
    # Development mode targets
    add_custom_target(enable-ibkr-mode
        COMMAND ${CMAKE_COMMAND} -E echo "Enabling IBKR production mode..."
        COMMAND ${CMAKE_COMMAND} -DALARIS_USE_IBKR_DATA=ON -DALARIS_DEVELOPMENT_MODE=OFF ..
        COMMENT "Enable IBKR production mode"
        VERBATIM
    )
    
    add_custom_target(enable-dev-mode
        COMMAND ${CMAKE_COMMAND} -E echo "Enabling development mode with synthetic data..."
        COMMAND ${CMAKE_COMMAND} -DALARIS_DEVELOPMENT_MODE=ON -DALARIS_USE_IBKR_DATA=OFF ..
        COMMENT "Enable development mode with synthetic data"
        VERBATIM
    )
    
    # Standard targets (clean, validate, etc.)
    add_custom_target(clean-data
        COMMAND ${CMAKE_COMMAND} -E remove_directory "${ALARIS_DATA_DIR}"
        COMMAND ${CMAKE_COMMAND} -E remove_directory "${ALARIS_RESULTS_DIR}"
        COMMAND ${CMAKE_COMMAND} -E remove_directory "${ALARIS_CACHE_DIR}"
        COMMAND ${CMAKE_COMMAND} -E remove -f "${CMAKE_BINARY_DIR}/lean.json"
        COMMAND ${CMAKE_COMMAND} -E remove -f "${CMAKE_BINARY_DIR}/fetch_ibkr_data.py"
        COMMENT "Cleaning Alaris data directories"
        VERBATIM
    )
    
    add_custom_target(validate-data
        COMMAND ${CMAKE_COMMAND} -E echo "Validating Alaris data setup..."
        COMMAND ${CMAKE_COMMAND}
            -DALARIS_DATA_DIR="${ALARIS_DATA_DIR}"
            -DALARIS_RESULTS_DIR="${ALARIS_RESULTS_DIR}"
            -DALARIS_CACHE_DIR="${ALARIS_CACHE_DIR}"
            -DALARIS_USE_IBKR_DATA=${ALARIS_USE_IBKR_DATA}
            -DALARIS_CREATE_SAMPLE_DATA=${ALARIS_CREATE_SAMPLE_DATA}
            -DCMAKE_BINARY_DIR="${CMAKE_BINARY_DIR}"
            -P "${CMAKE_CURRENT_SOURCE_DIR}/.cmake/DataInlineValidate.cmake"
        COMMENT "Validating Alaris data setup"
        VERBATIM
    )
    
    # Enhanced refresh target
    add_custom_target(refresh-data
        DEPENDS clean-data setup-data
        COMMENT "Refreshing Alaris data (clean and rebuild)"
    )
    
    # IBKR connection test target
    if(ALARIS_USE_IBKR_DATA OR NOT ALARIS_DEVELOPMENT_MODE)
        add_custom_target(test-ibkr-connection
            COMMAND ${CMAKE_COMMAND} -E echo "Testing IBKR Gateway connection..."
            COMMAND python3 -c "
from ib_insync import IB
ib = IB()
try:
    ib.connect('${IBKR_GATEWAY_HOST}', ${IBKR_GATEWAY_PORT_PAPER}, clientId=${IBKR_CLIENT_ID})
    print('✓ Successfully connected to IB Gateway')
    print(f'Account: {ib.accountSummary()}' if ib.accountSummary() else '  Account info not available')
    ib.disconnect()
except Exception as e:
    print(f'✗ Connection failed: {e}')
    print('Make sure IB Gateway is running and API access is enabled')
    exit(1)
"
            COMMENT "Testing IBKR Gateway connection"
            VERBATIM
        )
    endif()
    
    message(STATUS "Data: Created enhanced management targets with IBKR support")
    if(ALARIS_USE_IBKR_DATA OR NOT ALARIS_DEVELOPMENT_MODE)
        message(STATUS "Data: IBKR targets: fetch-backtest-data, fetch-forward-data, start-realtime-data")
        message(STATUS "Data: Connection test: test-ibkr-connection")
    endif()
    message(STATUS "Data: Mode targets: enable-ibkr-mode, enable-dev-mode")
endfunction()

# Auto-setup data during configuration if enabled
if(ALARIS_AUTO_SETUP_DATA)
    setup_alaris_data()
else()
    if(ALARIS_USE_IBKR_DATA)
        message(STATUS "Data: IBKR mode enabled but auto-setup disabled.")
        message(STATUS "Data: Run 'cmake --build . --target setup-data' to create IBKR environment.")
        message(STATUS "Data: Then run 'cmake --build . --target fetch-backtest-data' to fetch data.")
    else()
        message(STATUS "Data: Development mode - auto-setup disabled.")
        message(STATUS "Data: Run 'cmake --build . --target setup-data' to create environment.")
    endif()
endif()

# Always create targets
create_data_targets()

message(STATUS "Data: Enhanced configuration completed with IBKR integration")
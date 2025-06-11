# .cmake/Data.cmake
# Unified data management for Alaris trading system

# Data configuration options
option(ALARIS_DOWNLOAD_DATA "Download essential market data during build" ON)
option(ALARIS_MINIMAL_DATA "Use minimal data set (faster builds)" ON)
option(ALARIS_INCLUDE_SAMPLE_DATA "Include sample historical data" OFF)

# Data URLs and sources
set(LEAN_DATA_BASE_URL "https://raw.githubusercontent.com/QuantConnect/Lean/master/Data")

# Define essential data files
set(ESSENTIAL_DATA_FILES
    "market-hours/market-hours-database.json"
    "symbol-properties/symbol-properties-database.csv"
)

# Define essential symbols for minimal setup
set(ESSENTIAL_SYMBOLS
    "SPY"   # S&P 500 ETF
    "QQQ"   # NASDAQ ETF
    "IWM"   # Russell 2000 ETF
    "AAPL"  # Major individual stock
    "MSFT"  # Major individual stock
)

# Additional symbols for full setup
set(EXTENDED_SYMBOLS
    "EFA"   # International developed markets
    "EEM"   # Emerging markets
    "TLT"   # Long-term treasury
    "GLD"   # Gold ETF
    "VIX"   # Volatility index
    "GOOGL" "AMZN" "TSLA" "META" "NVDA"
    "JPM" "JNJ" "V" "PG" "UNH" "HD" "MA" "BAC"
)

# Global variables for data paths
set(ALARIS_DATA_DIR "${CMAKE_BINARY_DIR}/data")
set(ALARIS_RESULTS_DIR "${CMAKE_BINARY_DIR}/results")
set(ALARIS_CACHE_DIR "${CMAKE_BINARY_DIR}/cache")

# Function to create data directory structure
function(create_data_directories)
    set(DATA_DIRS
        "${ALARIS_DATA_DIR}"
        "${ALARIS_DATA_DIR}/market-hours"
        "${ALARIS_DATA_DIR}/symbol-properties"
        "${ALARIS_DATA_DIR}/equity/usa/map_files"
        "${ALARIS_DATA_DIR}/equity/usa/factor_files"
        "${ALARIS_DATA_DIR}/option/usa"
        "${ALARIS_RESULTS_DIR}"
        "${ALARIS_CACHE_DIR}"
    )
    
    foreach(dir ${DATA_DIRS})
        file(MAKE_DIRECTORY "${dir}")
    endforeach()
    
    message(STATUS "Data: Created directory structure")
endfunction()

# Function to download a file with error handling
function(download_file_safe URL OUTPUT_PATH DESCRIPTION)
    # Skip if file exists
    if(EXISTS "${OUTPUT_PATH}")
        message(STATUS "Data: ${DESCRIPTION} already exists")
        return()
    endif()
    
    message(STATUS "Data: Downloading ${DESCRIPTION}...")
    
    file(DOWNLOAD "${URL}" "${OUTPUT_PATH}"
         SHOW_PROGRESS
         STATUS download_status
         TIMEOUT 30)
    
    list(GET download_status 0 status_code)
    list(GET download_status 1 status_message)
    
    if(status_code EQUAL 0)
        message(STATUS "Data: ✓ ${DESCRIPTION} downloaded successfully")
    else()
        message(WARNING "Data: Failed to download ${DESCRIPTION}: ${status_message}")
        message(STATUS "Data: Manual download URL: ${URL}")
    endif()
endfunction()

# Function to create lean.json configuration
function(create_lean_config_file)
    set(LEAN_CONFIG_PATH "${CMAKE_BINARY_DIR}/lean.json")
    
    # Skip if file exists
    if(EXISTS "${LEAN_CONFIG_PATH}")
        message(STATUS "Data: lean.json already exists")
        return()
    endif()
    
    set(LEAN_CONFIG_CONTENT "{
  \"_comment\": \"Alaris Trading System - QuantConnect Lean Configuration\",
  
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
  
  \"debug-mode\": false,
  \"log-level\": \"Info\",
  \"show-missing-data-logs\": false,
  
  \"maximum-data-points-per-chart-series\": 100000,
  \"maximum-chart-series\": 30,
  \"maximum-runtime-minutes\": 0,
  \"maximum-orders\": 0,
  \"force-exchange-always-open\": true,
  \"enable-automatic-indicator-warm-up\": false,
  
  \"environments\": {
    \"backtesting\": {
      \"live-mode\": false,
      \"setup-handler\": \"QuantConnect.Lean.Engine.Setup.ConsoleSetupHandler\",
      \"result-handler\": \"QuantConnect.Lean.Engine.Results.BacktestingResultHandler\",
      \"data-feed-handler\": \"QuantConnect.Lean.Engine.DataFeeds.FileSystemDataFeed\", 
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
      
      \"live-mode-brokerage\": \"InteractiveBrokersBrokerage\",
      \"data-queue-handler\": \"QuantConnect.Brokerages.InteractiveBrokers.InteractiveBrokersDataQueueHandler\",
      \"ib-account\": \"DU123456\",
      \"ib-user-name\": \"\",
      \"ib-password\": \"\",
      \"ib-host\": \"127.0.0.1\",
      \"ib-port\": \"4002\", 
      \"ib-agent-description\": \"Individual\"
    }
  },
  
  \"job-user-id\": \"1\",
  \"job-project-id\": \"1\",
  \"job-organization-id\": \"1\",
  \"api-access-token\": \"\",
  
  \"alaris\": {
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
    }
  }
}")

    file(WRITE "${LEAN_CONFIG_PATH}" "${LEAN_CONFIG_CONTENT}")
    message(STATUS "Data: ✓ lean.json configuration created")
endfunction()

# Function to create map files for symbols
function(create_map_files)
    if(ALARIS_MINIMAL_DATA)
        set(SYMBOLS_TO_PROCESS ${ESSENTIAL_SYMBOLS})
    else()
        set(SYMBOLS_TO_PROCESS ${ESSENTIAL_SYMBOLS} ${EXTENDED_SYMBOLS})
    endif()
    
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

# Function to create minimal symbol properties database
function(create_symbol_properties_database)
    set(SYMBOL_PROPS_PATH "${ALARIS_DATA_DIR}/symbol-properties/security-database.csv")
    
    # Skip if file exists
    if(EXISTS "${SYMBOL_PROPS_PATH}")
        message(STATUS "Data: security-database.csv already exists")
        return()
    endif()
    
    set(SYMBOL_PROPS_HEADER "Symbol,Market,SecurityType,Name,LotSize,MinimumPriceVariation,PriceMagnifier")
    
    if(ALARIS_MINIMAL_DATA)
        set(SYMBOLS_TO_PROCESS ${ESSENTIAL_SYMBOLS})
    else()
        set(SYMBOLS_TO_PROCESS ${ESSENTIAL_SYMBOLS} ${EXTENDED_SYMBOLS})
    endif()
    
    # Symbol name mappings
    set(SYMBOL_NAMES_SPY "SPDR S&P 500 ETF Trust")
    set(SYMBOL_NAMES_QQQ "Invesco QQQ Trust")
    set(SYMBOL_NAMES_IWM "iShares Russell 2000 ETF")
    set(SYMBOL_NAMES_AAPL "Apple Inc.")
    set(SYMBOL_NAMES_MSFT "Microsoft Corporation")
    set(SYMBOL_NAMES_EFA "iShares MSCI EAFE ETF")
    set(SYMBOL_NAMES_EEM "iShares MSCI Emerging Markets ETF")
    set(SYMBOL_NAMES_TLT "iShares 20+ Year Treasury Bond ETF")
    set(SYMBOL_NAMES_GLD "SPDR Gold Trust")
    set(SYMBOL_NAMES_VIX "CBOE Volatility Index")
    set(SYMBOL_NAMES_GOOGL "Alphabet Inc. Class A")
    set(SYMBOL_NAMES_AMZN "Amazon.com Inc.")
    set(SYMBOL_NAMES_TSLA "Tesla Inc.")
    set(SYMBOL_NAMES_META "Meta Platforms Inc.")
    set(SYMBOL_NAMES_NVDA "NVIDIA Corporation")
    set(SYMBOL_NAMES_JPM "JPMorgan Chase & Co.")
    set(SYMBOL_NAMES_JNJ "Johnson & Johnson")
    set(SYMBOL_NAMES_V "Visa Inc.")
    set(SYMBOL_NAMES_PG "Procter & Gamble Co.")
    set(SYMBOL_NAMES_UNH "UnitedHealth Group Inc.")
    set(SYMBOL_NAMES_HD "The Home Depot Inc.")
    set(SYMBOL_NAMES_MA "Mastercard Inc.")
    set(SYMBOL_NAMES_BAC "Bank of America Corp.")
    
    set(SYMBOL_PROPS_CONTENT "${SYMBOL_PROPS_HEADER}\n")
    
    foreach(symbol ${SYMBOLS_TO_PROCESS})
        set(symbol_name_var "SYMBOL_NAMES_${symbol}")
        if(DEFINED ${symbol_name_var})
            set(symbol_name "${${symbol_name_var}}")
        else()
            set(symbol_name "${symbol}")
        endif()
        
        string(APPEND SYMBOL_PROPS_CONTENT "${symbol},usa,Equity,${symbol_name},1,0.01,1\n")
    endforeach()
    
    file(WRITE "${SYMBOL_PROPS_PATH}" "${SYMBOL_PROPS_CONTENT}")
    
    list(LENGTH SYMBOLS_TO_PROCESS symbol_count)
    message(STATUS "Data: ✓ Created security database with ${symbol_count} symbols")
endfunction()

# Function to download essential data files
function(download_essential_data)
    if(NOT ALARIS_DOWNLOAD_DATA)
        message(STATUS "Data: Download disabled, skipping...")
        return()
    endif()
    
    foreach(data_file ${ESSENTIAL_DATA_FILES})
        set(url "${LEAN_DATA_BASE_URL}/${data_file}")
        set(output_path "${ALARIS_DATA_DIR}/${data_file}")
        
        get_filename_component(file_name "${data_file}" NAME)
        download_file_safe("${url}" "${output_path}" "${file_name}")
    endforeach()
endfunction()

# Function to create sample data (if requested)
function(create_sample_data)
    if(NOT ALARIS_INCLUDE_SAMPLE_DATA)
        return()
    endif()
    
    message(STATUS "Data: Creating sample historical data...")
    
    # Create sample daily data for essential symbols
    foreach(symbol ${ESSENTIAL_SYMBOLS})
        string(TOLOWER "${symbol}" symbol_lower)
        set(sample_data_dir "${ALARIS_DATA_DIR}/equity/usa/daily/${symbol_lower}")
        file(MAKE_DIRECTORY "${sample_data_dir}")
        
        # Create a simple CSV with some sample data
        set(sample_file "${sample_data_dir}/20240101_20240131_trade.zip")
        
        # Note: In a real implementation, you'd want to download or generate
        # actual market data. This is just a placeholder.
        message(STATUS "Data: Sample data placeholder created for ${symbol}")
    endforeach()
endfunction()

# Function to validate data setup
function(validate_data_setup)
    set(required_files
        "${ALARIS_DATA_DIR}/market-hours/market-hours-database.json"
        "${ALARIS_DATA_DIR}/symbol-properties/symbol-properties-database.csv"
        "${ALARIS_DATA_DIR}/symbol-properties/security-database.csv"
        "${CMAKE_BINARY_DIR}/lean.json"
    )
    
    set(required_dirs
        "${ALARIS_DATA_DIR}"
        "${ALARIS_DATA_DIR}/equity/usa/map_files"
        "${ALARIS_DATA_DIR}/equity/usa/factor_files"
        "${ALARIS_RESULTS_DIR}"
        "${ALARIS_CACHE_DIR}"
    )
    
    set(missing_files "")
    set(missing_dirs "")
    
    foreach(file ${required_files})
        if(NOT EXISTS "${file}")
            list(APPEND missing_files "${file}")
        endif()
    endforeach()
    
    foreach(dir ${required_dirs})
        if(NOT IS_DIRECTORY "${dir}")
            list(APPEND missing_dirs "${dir}")
        endif()
    endforeach()
    
    # Print validation results
    if(missing_files OR missing_dirs)
        message(WARNING "Data: Validation found missing components:")
        foreach(file ${missing_files})
            message(WARNING "Data:   Missing file: ${file}")
        endforeach()
        foreach(dir ${missing_dirs})
            message(WARNING "Data:   Missing directory: ${dir}")
        endforeach()
        message(WARNING "Data: Run 'cmake --build . --target setup-data' to fix")
        return()
    endif()
    
    message(STATUS "Data: ✓ Validation passed - all required files and directories present")
endfunction()

# Function to clean data directory
function(clean_data_directory)
    set(dirs_to_clean
        "${ALARIS_DATA_DIR}"
        "${ALARIS_RESULTS_DIR}"
        "${ALARIS_CACHE_DIR}"
    )
    
    foreach(dir ${dirs_to_clean})
        if(EXISTS "${dir}")
            file(REMOVE_RECURSE "${dir}")
        endif()
    endforeach()
    
    if(EXISTS "${CMAKE_BINARY_DIR}/lean.json")
        file(REMOVE "${CMAKE_BINARY_DIR}/lean.json")
    endif()
    
    message(STATUS "Data: ✓ Cleaned data directories")
endfunction()

# Main data setup function (called during configuration)
function(setup_alaris_data)
    message(STATUS "Data: Setting up Alaris data...")
    
    # Create directory structure
    create_data_directories()
    
    # Download essential files
    download_essential_data()
    
    # Create configuration files
    create_lean_config_file()
    
    # Create symbol-specific files
    create_map_files()
    create_symbol_properties_database()
    
    # Create sample data if requested
    create_sample_data()
    
    # Print summary
    if(ALARIS_MINIMAL_DATA)
        message(STATUS "Data: ✓ Minimal data setup completed (${ESSENTIAL_SYMBOLS})")
    else()
        list(LENGTH ESSENTIAL_SYMBOLS essential_count)
        list(LENGTH EXTENDED_SYMBOLS extended_count)
        math(EXPR total_count "${essential_count} + ${extended_count}")
        message(STATUS "Data: ✓ Full data setup completed (${total_count} symbols)")
    endif()
    
    message(STATUS "Data: Location: ${ALARIS_DATA_DIR}")
endfunction()

# Create custom targets for data management
function(create_data_targets)
    # Target to set up data
    add_custom_target(setup-data
        COMMAND ${CMAKE_COMMAND} -E echo "Setting up Alaris data..."
        COMMAND ${CMAKE_COMMAND} -P "${CMAKE_CURRENT_SOURCE_DIR}/.cmake/DataSetupTarget.cmake"
        COMMENT "Setting up Alaris data"
        VERBATIM
    )
    
    # Target to clean data
    add_custom_target(clean-data
        COMMAND ${CMAKE_COMMAND} -E remove_directory "${ALARIS_DATA_DIR}"
        COMMAND ${CMAKE_COMMAND} -E remove_directory "${ALARIS_RESULTS_DIR}"
        COMMAND ${CMAKE_COMMAND} -E remove_directory "${ALARIS_CACHE_DIR}"
        COMMAND ${CMAKE_COMMAND} -E remove -f "${CMAKE_BINARY_DIR}/lean.json"
        COMMENT "Cleaning Alaris data directories"
        VERBATIM
    )
    
    # Target to validate data
    add_custom_target(validate-data
        COMMAND ${CMAKE_COMMAND} -P "${CMAKE_CURRENT_SOURCE_DIR}/.cmake/DataValidateTarget.cmake"
        COMMENT "Validating Alaris data setup"
        VERBATIM
    )
    
    message(STATUS "Data: Created management targets (setup-data, clean-data, validate-data)")
endfunction()

# Auto-setup data during configuration
setup_alaris_data()

# Create targets for manual data management
create_data_targets()

# Validate data setup
validate_data_setup()

message(STATUS "Data: Configuration completed")
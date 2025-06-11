# .cmake/Data.cmake
# Unified data management for Alaris trading system

# Data configuration options
option(ALARIS_DOWNLOAD_DATA "Download essential market data during build" ON)
option(ALARIS_MINIMAL_DATA "Use minimal data set (faster builds)" ON)
option(ALARIS_CREATE_SAMPLE_DATA "Create sample historical data for backtesting" ON)
option(ALARIS_AUTO_SETUP_DATA "Automatically set up data during build process" ON)

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
        "${ALARIS_DATA_DIR}/equity/usa/daily"
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

# Function to create realistic price data for a symbol
# Function to create realistic price data for a symbol
function(create_realistic_price_data OUTPUT_FILE SYMBOL)
    # Set base prices for different symbols (using integers, will scale later)
    if(SYMBOL STREQUAL "SPY")
        set(BASE_PRICE_INT 40000)  # Represents $400.00
    elseif(SYMBOL STREQUAL "QQQ")
        set(BASE_PRICE_INT 35000)  # Represents $350.00
    elseif(SYMBOL STREQUAL "AAPL")
        set(BASE_PRICE_INT 18000)  # Represents $180.00
    elseif(SYMBOL STREQUAL "MSFT")
        set(BASE_PRICE_INT 37000)  # Represents $370.00
    else()
        set(BASE_PRICE_INT 10000)  # Represents $100.00
    endif()
    
    # Convert to floating point for CSV output
    math(EXPR BASE_PRICE_MAJOR "${BASE_PRICE_INT} / 100")
    math(EXPR BASE_PRICE_MINOR "${BASE_PRICE_INT} % 100")
    if(BASE_PRICE_MINOR LESS 10)
        set(BASE_PRICE "${BASE_PRICE_MAJOR}.0${BASE_PRICE_MINOR}")
    else()
        set(BASE_PRICE "${BASE_PRICE_MAJOR}.${BASE_PRICE_MINOR}")
    endif()
    
    # Create CSV header (Lean format: DateTime, Open, High, Low, Close, Volume)
    set(CSV_CONTENT "20240102 00:00,${BASE_PRICE},${BASE_PRICE},${BASE_PRICE},${BASE_PRICE},1000000\n")
    
    # Generate daily data for 2024 (simple random walk using integer math)
    set(CURRENT_PRICE_INT ${BASE_PRICE_INT})
    
    # Generate 250 trading days (approximate year)
    foreach(day_num RANGE 1 250)
        # Simple price movement simulation using integer math
        math(EXPR day_offset "${day_num} % 30")
        math(EXPR price_change_basis_points "((${day_offset} - 15) * 10)")  # -150 to +150 basis points
        
        # Calculate price change (max ±1.5%)
        # Using integer math: price_change = current_price * basis_points / 10000
        math(EXPR price_change_numerator "${CURRENT_PRICE_INT} * ${price_change_basis_points}")
        math(EXPR price_change "${price_change_numerator} / 10000")
        math(EXPR new_price_int "${CURRENT_PRICE_INT} + ${price_change}")
        
        # Ensure price doesn't go below $1.00 (100 cents)
        if(new_price_int LESS 100)
            set(new_price_int 100)
        endif()
        
        # Calculate OHLC from close price using integer math
        math(EXPR high_price_int "${new_price_int} * 102 / 100")  # High is 2% above close
        math(EXPR low_price_int "${new_price_int} * 98 / 100")    # Low is 2% below close
        set(open_price_int ${CURRENT_PRICE_INT})                   # Open is previous close
        
        # Convert all prices to floating point for CSV
        math(EXPR open_major "${open_price_int} / 100")
        math(EXPR open_minor "${open_price_int} % 100")
        if(open_minor LESS 10)
            set(open_price "${open_major}.0${open_minor}")
        else()
            set(open_price "${open_major}.${open_minor}")
        endif()
        
        math(EXPR high_major "${high_price_int} / 100")
        math(EXPR high_minor "${high_price_int} % 100")
        if(high_minor LESS 10)
            set(high_price "${high_major}.0${high_minor}")
        else()
            set(high_price "${high_major}.${high_minor}")
        endif()
        
        math(EXPR low_major "${low_price_int} / 100")
        math(EXPR low_minor "${low_price_int} % 100")
        if(low_minor LESS 10)
            set(low_price "${low_major}.0${low_minor}")
        else()
            set(low_price "${low_major}.${low_minor}")
        endif()
        
        math(EXPR close_major "${new_price_int} / 100")
        math(EXPR close_minor "${new_price_int} % 100")
        if(close_minor LESS 10)
            set(close_price "${close_major}.0${close_minor}")
        else()
            set(close_price "${close_major}.${close_minor}")
        endif()
        
        # Format date (month/day calculation using integer math)
        math(EXPR month_num "(${day_num} / 22) + 1")  # Approximate 22 trading days per month
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
        
        # Generate volume (1M ± 50% using integer math)
        math(EXPR volume_base "1000000")
        math(EXPR volume_var "${day_num} % 500000")
        math(EXPR volume "${volume_base} + ${volume_var}")
        
        # Add to CSV content
        string(APPEND CSV_CONTENT "${date_str} 00:00,${open_price},${high_price},${low_price},${close_price},${volume}\n")
        
        set(CURRENT_PRICE_INT ${new_price_int})
    endforeach()
    
    # Write the CSV file
    file(WRITE "${OUTPUT_FILE}" "${CSV_CONTENT}")
endfunction()

# Function to create realistic sample data for backtesting
function(create_sample_data)
    message(STATUS "Data: Creating realistic sample historical data for backtesting...")
    
    if(ALARIS_MINIMAL_DATA)
        set(SYMBOLS_TO_PROCESS ${ESSENTIAL_SYMBOLS})
    else()
        set(SYMBOLS_TO_PROCESS ${ESSENTIAL_SYMBOLS} ${EXTENDED_SYMBOLS})
    endif()
    
    # Create sample daily data for essential symbols
    foreach(symbol ${SYMBOLS_TO_PROCESS})
        string(TOLOWER "${symbol}" symbol_lower)
        set(sample_data_dir "${ALARIS_DATA_DIR}/equity/usa/daily/${symbol_lower}")
        file(MAKE_DIRECTORY "${sample_data_dir}")
        
        # Create realistic CSV data that Lean can actually read
        set(sample_csv_file "${sample_data_dir}/20240101_20241231_trade.csv")
        
        # Generate realistic price data for the year 2024
        create_realistic_price_data("${sample_csv_file}" "${symbol}")
        
        message(STATUS "Data: ✓ Created sample data for ${symbol} (${sample_csv_file})")
    endforeach()
    
    list(LENGTH SYMBOLS_TO_PROCESS symbol_count)
    message(STATUS "Data: ✓ Created realistic sample data for ${symbol_count} symbols")
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
        "${ALARIS_DATA_DIR}/equity/usa/daily"
        "${ALARIS_RESULTS_DIR}"
        "${ALARIS_CACHE_DIR}"
    )
    
    # Check for sample data files if enabled
    set(required_data_files "")
    if(ALARIS_CREATE_SAMPLE_DATA)
        if(ALARIS_MINIMAL_DATA)
            set(symbols_to_check ${ESSENTIAL_SYMBOLS})
        else()
            set(symbols_to_check ${ESSENTIAL_SYMBOLS} ${EXTENDED_SYMBOLS})
        endif()
        
        foreach(symbol ${symbols_to_check})
            string(TOLOWER "${symbol}" symbol_lower)
            set(data_file "${ALARIS_DATA_DIR}/equity/usa/daily/${symbol_lower}/20240101_20241231_trade.csv")
            list(APPEND required_data_files "${data_file}")
        endforeach()
    endif()
    
    set(missing_files "")
    set(missing_dirs "")
    set(missing_data_files "")
    
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
    
    foreach(data_file ${required_data_files})
        if(NOT EXISTS "${data_file}")
            list(APPEND missing_data_files "${data_file}")
        endif()
    endforeach()
    
    # Print validation results
    if(missing_files OR missing_dirs OR missing_data_files)
        message(WARNING "Data: Validation found missing components:")
        foreach(file ${missing_files})
            message(WARNING "Data:   Missing config file: ${file}")
        endforeach()
        foreach(dir ${missing_dirs})
            message(WARNING "Data:   Missing directory: ${dir}")
        endforeach()
        foreach(data_file ${missing_data_files})
            message(WARNING "Data:   Missing data file: ${data_file}")
        endforeach()
        message(WARNING "Data: Run 'cmake --build . --target setup-data' to fix")
        return()
    endif()
    
    # Additional validation - check if data files have content
    set(empty_data_files "")
    foreach(data_file ${required_data_files})
        if(EXISTS "${data_file}")
            file(SIZE "${data_file}" file_size)
            if(file_size LESS 100)  # Files should be at least 100 bytes
                list(APPEND empty_data_files "${data_file}")
            endif()
        endif()
    endforeach()
    
    if(empty_data_files)
        message(WARNING "Data: Found empty or very small data files:")
        foreach(data_file ${empty_data_files})
            message(WARNING "Data:   Empty: ${data_file}")
        endforeach()
        message(WARNING "Data: Run 'cmake --build . --target setup-data' to regenerate")
        return()
    endif()
    
    message(STATUS "Data: ✓ Validation passed - all required files and directories present")
    
    # Print summary of what's available
    list(LENGTH required_data_files data_file_count)
    if(data_file_count GREATER 0)
        message(STATUS "Data: ✓ Historical data available for ${data_file_count} symbols")
        message(STATUS "Data: ✓ FileSystemDataFeed will find data in ${ALARIS_DATA_DIR}")
    endif()
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
    message(STATUS "Data: Setting up Alaris data environment...")
    
    # Create directory structure
    create_data_directories()
    
    # Download essential files
    download_essential_data()
    
    # Create configuration files
    create_lean_config_file()
    
    # Create symbol-specific files
    create_map_files()
    create_symbol_properties_database()
    
    # Always create sample data for backtesting (unless explicitly disabled)
    if(ALARIS_CREATE_SAMPLE_DATA)
        create_sample_data()
    endif()
    
    # Print summary
    if(ALARIS_MINIMAL_DATA)
        list(LENGTH ESSENTIAL_SYMBOLS essential_count)
        message(STATUS "Data: ✓ Minimal data setup completed (${essential_count} symbols)")
    else()
        list(LENGTH ESSENTIAL_SYMBOLS essential_count)
        list(LENGTH EXTENDED_SYMBOLS extended_count)
        math(EXPR total_count "${essential_count} + ${extended_count}")
        message(STATUS "Data: ✓ Full data setup completed (${total_count} symbols)")
    endif()
    
    if(ALARIS_CREATE_SAMPLE_DATA)
        message(STATUS "Data: ✓ Sample historical data created for backtesting")
    endif()
    
    message(STATUS "Data: Location: ${ALARIS_DATA_DIR}")
    message(STATUS "Data: lean.json: ${CMAKE_BINARY_DIR}/lean.json")
endfunction()

# Function to create DataSetupTarget.cmake script
function(create_data_setup_target_script)
    set(SETUP_SCRIPT_CONTENT "
# DataSetupTarget.cmake - Executed by setup-data target
message(STATUS \"Executing data setup...\")

# Re-run the main data setup function
include(\${CMAKE_CURRENT_SOURCE_DIR}/.cmake/Data.cmake)
setup_alaris_data()
validate_data_setup()

message(STATUS \"Data setup completed successfully!\")
")
    
    file(WRITE "${CMAKE_CURRENT_SOURCE_DIR}/.cmake/DataSetupTarget.cmake" "${SETUP_SCRIPT_CONTENT}")
endfunction()

# Function to create DataValidateTarget.cmake script  
function(create_data_validate_target_script)
    set(VALIDATE_SCRIPT_CONTENT "
# DataValidateTarget.cmake - Executed by validate-data target
message(STATUS \"Validating data setup...\")

# Set up the same variables as the main script
set(ALARIS_DATA_DIR \"${ALARIS_DATA_DIR}\")
set(ALARIS_RESULTS_DIR \"${ALARIS_RESULTS_DIR}\")
set(ALARIS_CACHE_DIR \"${ALARIS_CACHE_DIR}\")
set(ESSENTIAL_SYMBOLS \"${ESSENTIAL_SYMBOLS}\")
set(EXTENDED_SYMBOLS \"${EXTENDED_SYMBOLS}\")
set(ALARIS_MINIMAL_DATA ${ALARIS_MINIMAL_DATA})
set(ALARIS_CREATE_SAMPLE_DATA ${ALARIS_CREATE_SAMPLE_DATA})

# Re-run validation
include(\${CMAKE_CURRENT_SOURCE_DIR}/.cmake/Data.cmake)
validate_data_setup()
")
    
    file(WRITE "${CMAKE_CURRENT_SOURCE_DIR}/.cmake/DataValidateTarget.cmake" "${VALIDATE_SCRIPT_CONTENT}")
endfunction()

# Create custom targets for data management
function(create_data_targets)
    # Target to set up data
    add_custom_target(setup-data
        COMMAND ${CMAKE_COMMAND} -E echo "Setting up Alaris data environment..."
        COMMAND ${CMAKE_COMMAND} -P "${CMAKE_CURRENT_SOURCE_DIR}/.cmake/DataSetupTarget.cmake"
        COMMENT "Setting up Alaris data environment"
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
    
    # Target to refresh data (clean + setup)
    add_custom_target(refresh-data
        DEPENDS clean-data setup-data
        COMMENT "Refreshing Alaris data (clean and rebuild)"
    )
    
    # Target to verify data for Lean
    add_custom_target(verify-lean-data
        COMMAND ${CMAKE_COMMAND} -E echo "Verifying data for Lean FileSystemDataFeed..."
        COMMAND ${CMAKE_COMMAND} -E echo "Data directory: ${ALARIS_DATA_DIR}"
        COMMAND bash -c "find '${ALARIS_DATA_DIR}/equity/usa/daily' -name '*.csv' | head -5 | xargs -I {} echo 'Found: {}'"
        COMMAND bash -c "echo 'Total CSV files: ' && find '${ALARIS_DATA_DIR}/equity/usa/daily' -name '*.csv' | wc -l"
        COMMENT "Verifying Lean data availability"
        VERBATIM
    )
    
    message(STATUS "Data: Created management targets (setup-data, clean-data, validate-data, refresh-data, verify-lean-data)")
endfunction()

# Auto-setup data during configuration if enabled
if(ALARIS_AUTO_SETUP_DATA)
    setup_alaris_data()
else()
    message(STATUS "Data: Auto-setup disabled. Run 'cmake --build . --target setup-data' to setup manually.")
endif()

# Always create the target scripts and targets
create_data_setup_target_script()
create_data_validate_target_script()
create_data_targets()

# Always validate if data already exists
validate_data_setup()

message(STATUS "Data: Configuration completed")
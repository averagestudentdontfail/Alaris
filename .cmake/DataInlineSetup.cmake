
# DataInlineSetup.cmake - Inline data setup
message(STATUS "Executing inline data setup...")

# Set up symbol lists
set(ESSENTIAL_SYMBOLS "SPY;QQQ;IWM;AAPL;MSFT")
set(PRODUCTION_SYMBOLS "SPY;QQQ;IWM;EFA;EEM;TLT;GLD;VIX;AAPL;MSFT;GOOGL;AMZN;TSLA;META;NVDA;JPM;JNJ;V;PG;UNH;HD;MA;BAC;XLF;XLE;XLK;XLV;XLI;XLP;XLU;XLRE")

# Create directories
file(MAKE_DIRECTORY "${ALARIS_DATA_DIR}")
file(MAKE_DIRECTORY "${ALARIS_DATA_DIR}/market-hours")
file(MAKE_DIRECTORY "${ALARIS_DATA_DIR}/symbol-properties")
file(MAKE_DIRECTORY "${ALARIS_DATA_DIR}/equity/usa/map_files")
file(MAKE_DIRECTORY "${ALARIS_DATA_DIR}/equity/usa/factor_files")
file(MAKE_DIRECTORY "${ALARIS_DATA_DIR}/equity/usa/daily")
file(MAKE_DIRECTORY "${ALARIS_DATA_DIR}/option/usa")
file(MAKE_DIRECTORY "${ALARIS_RESULTS_DIR}")
file(MAKE_DIRECTORY "${ALARIS_CACHE_DIR}")

# Create lean.json if it doesn't exist
if(NOT EXISTS "${CMAKE_BINARY_DIR}/lean.json")
    if(ALARIS_DEVELOPMENT_MODE OR ALARIS_CREATE_SAMPLE_DATA)
        set(CONFIG_COMMENT "Alaris Trading System - Development Configuration")
        set(DEBUG_MODE "true")
        set(LOG_LEVEL "Debug")
    else()
        set(CONFIG_COMMENT "Alaris Trading System - Production Configuration")
        set(DEBUG_MODE "false")
        set(LOG_LEVEL "Info")
    endif()
    
    file(WRITE "${CMAKE_BINARY_DIR}/lean.json" "{
  \"_comment\": \"${CONFIG_COMMENT}\",
  \"algorithm-type-name\": \"Alaris.Algorithm.ArbitrageAlgorithm\",
  \"algorithm-language\": \"CSharp\",
  \"data-directory\": \"${ALARIS_DATA_DIR}/\",
  \"cache-location\": \"${ALARIS_CACHE_DIR}/\",
  \"results-destination-folder\": \"${ALARIS_RESULTS_DIR}/\",
  \"debug-mode\": ${DEBUG_MODE},
  \"log-level\": \"${LOG_LEVEL}\",
  \"environments\": {
    \"backtesting\": {
      \"live-mode\": false,
      \"data-feed-handler\": \"QuantConnect.Lean.Engine.DataFeeds.FileSystemDataFeed\"
    }
  }
}")
    message(STATUS "Created lean.json configuration")
endif()

# Create symbol databases and map files
if(ALARIS_MINIMAL_DATA)
    set(SYMBOLS_TO_PROCESS ${ESSENTIAL_SYMBOLS})
    set(MODE_DESC "minimal")
else()
    set(SYMBOLS_TO_PROCESS ${PRODUCTION_SYMBOLS})
    set(MODE_DESC "production")
endif()

# Create security database
set(SYMBOL_PROPS_PATH "${ALARIS_DATA_DIR}/symbol-properties/security-database.csv")
if(NOT EXISTS "${SYMBOL_PROPS_PATH}")
    set(SYMBOL_PROPS_CONTENT "Symbol,Market,SecurityType,Name,LotSize,MinimumPriceVariation,PriceMagnifier\n")
    foreach(symbol ${SYMBOLS_TO_PROCESS})
        string(APPEND SYMBOL_PROPS_CONTENT "${symbol},usa,Equity,${symbol},1,0.01,1\n")
    endforeach()
    file(WRITE "${SYMBOL_PROPS_PATH}" "${SYMBOL_PROPS_CONTENT}")
    message(STATUS "Created security database")
endif()

# Create map and factor files for each symbol
foreach(symbol ${SYMBOLS_TO_PROCESS})
    string(TOLOWER "${symbol}" symbol_lower)
    set(MAP_FILE_PATH "${ALARIS_DATA_DIR}/equity/usa/map_files/${symbol_lower}.csv")
    set(FACTOR_FILE_PATH "${ALARIS_DATA_DIR}/equity/usa/factor_files/${symbol_lower}.csv")
    
    if(NOT EXISTS "${MAP_FILE_PATH}")
        file(WRITE "${MAP_FILE_PATH}" "20120101,${symbol},${symbol},${symbol}\n")
    endif()
    
    if(NOT EXISTS "${FACTOR_FILE_PATH}")
        file(WRITE "${FACTOR_FILE_PATH}" "20120101,1.0,1.0\n")
    endif()
    
    # Create sample data if requested
    if(ALARIS_CREATE_SAMPLE_DATA)
        set(sample_data_dir "${ALARIS_DATA_DIR}/equity/usa/daily/${symbol_lower}")
        file(MAKE_DIRECTORY "${sample_data_dir}")
        
        set(sample_csv_file "${sample_data_dir}/20240101_20241231_trade.csv")
        if(NOT EXISTS "${sample_csv_file}")
            # Create simple synthetic data
            set(CSV_CONTENT "")
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
                string(APPEND CSV_CONTENT "${date_str} 00:00,100.00,100.50,99.50,100.25,1000000\n")
            endforeach()
            
            file(WRITE "${sample_csv_file}" "${CSV_CONTENT}")
        endif()
    endif()
endforeach()

list(LENGTH SYMBOLS_TO_PROCESS symbol_count)
message(STATUS "Data setup completed for ${symbol_count} symbols (${MODE_DESC} mode)")

if(ALARIS_CREATE_SAMPLE_DATA)
    message(STATUS "WARNING: Using synthetic data for development only!")
endif()

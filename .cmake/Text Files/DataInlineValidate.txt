
# DataInlineValidate.cmake - Inline data validation
message(STATUS "Validating Alaris data setup...")

set(required_files
    "${CMAKE_BINARY_DIR}/lean.json"
    "${ALARIS_DATA_DIR}/symbol-properties/security-database.csv"
)

set(required_dirs
    "${ALARIS_DATA_DIR}"
    "${ALARIS_DATA_DIR}/equity/usa/map_files"
    "${ALARIS_DATA_DIR}/equity/usa/factor_files"
    "${ALARIS_DATA_DIR}/equity/usa/daily"
    "${ALARIS_RESULTS_DIR}"
    "${ALARIS_CACHE_DIR}"
)

set(missing_files "")
set(missing_dirs "")

# Check required files
foreach(file ${required_files})
    if(NOT EXISTS "${file}")
        list(APPEND missing_files "${file}")
    endif()
endforeach()

# Check directories
foreach(dir ${required_dirs})
    if(NOT IS_DIRECTORY "${dir}")
        list(APPEND missing_dirs "${dir}")
    endif()
endforeach()

# Check for data files if synthetic data is enabled
if(ALARIS_CREATE_SAMPLE_DATA)
    if(ALARIS_MINIMAL_DATA)
        set(symbols_to_check "SPY;QQQ;IWM;AAPL;MSFT")
    else()
        set(symbols_to_check "SPY;QQQ;IWM;EFA;EEM;TLT;GLD;VIX;AAPL;MSFT;GOOGL;AMZN;TSLA;META;NVDA;JPM;JNJ;V;PG;UNH;HD;MA;BAC;XLF;XLE;XLK;XLV;XLI;XLP;XLU;XLRE")
    endif()
    
    set(missing_data_files "")
    foreach(symbol ${symbols_to_check})
        string(TOLOWER "${symbol}" symbol_lower)
        set(data_file "${ALARIS_DATA_DIR}/equity/usa/daily/${symbol_lower}/20240101_20241231_trade.csv")
        if(NOT EXISTS "${data_file}")
            list(APPEND missing_data_files "${data_file}")
        endif()
    endforeach()
    
    if(missing_data_files)
        list(APPEND missing_files ${missing_data_files})
    endif()
endif()

# Print validation results
if(missing_files OR missing_dirs)
    message(WARNING "Data validation found missing components:")
    foreach(file ${missing_files})
        message(WARNING "  Missing file: ${file}")
    endforeach()
    foreach(dir ${missing_dirs})
        message(WARNING "  Missing directory: ${dir}")
    endforeach()
    
    if(ALARIS_CREATE_SAMPLE_DATA)
        message(WARNING "Run 'cmake --build . --target setup-data' to create synthetic data")
    else()
        message(STATUS "Production mode - configure real data sources")
    endif()
    return()
endif()

message(STATUS "✓ Data validation passed - all required files and directories present")

if(ALARIS_CREATE_SAMPLE_DATA)
    message(STATUS "✓ Synthetic historical data available for development/testing")
    message(STATUS "⚠️  WARNING: Using synthetic data - for development/testing only!")
else()
    message(STATUS "✓ Production configuration ready")
endif()

message(STATUS "✓ FileSystemDataFeed will find data in ${ALARIS_DATA_DIR}")

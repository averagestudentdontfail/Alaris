
# DataValidateTarget.cmake - Executed by validate-data target
message(STATUS "Validating data setup...")

# Set up the same variables as the main script
set(ALARIS_DATA_DIR "/home/send2/.projects/Alaris/build/data")
set(ALARIS_RESULTS_DIR "/home/send2/.projects/Alaris/build/results")
set(ALARIS_CACHE_DIR "/home/send2/.projects/Alaris/build/cache")
set(ESSENTIAL_SYMBOLS "SPY;QQQ;IWM;AAPL;MSFT")
set(PRODUCTION_SYMBOLS "SPY;QQQ;IWM;EFA;EEM;TLT;GLD;VIX;AAPL;MSFT;GOOGL;AMZN;TSLA;META;NVDA;JPM;JNJ;V;PG;UNH;HD;MA;BAC;XLF;XLE;XLK;XLV;XLI;XLP;XLU;XLRE")
set(ALARIS_MINIMAL_DATA OFF)
set(ALARIS_CREATE_SAMPLE_DATA OFF)

# Re-run validation
include(${CMAKE_CURRENT_SOURCE_DIR}/.cmake/Data.cmake)
validate_data_setup()

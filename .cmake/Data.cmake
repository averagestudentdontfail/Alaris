# .cmake/Data.cmake
# Manages the creation of the data directory structure required by the Lean engine.

# Define global variables for data paths. These are used to ensure consistency.
set(ALARIS_DATA_DIR "${CMAKE_BINARY_DIR}/data" CACHE PATH "Root directory for Alaris market and auxiliary data")
set(ALARIS_RESULTS_DIR "${CMAKE_BINARY_DIR}/results" CACHE PATH "Directory for algorithm backtest and live results")
set(ALARIS_CACHE_DIR "${CMAKE_BINARY_DIR}/cache" CACHE PATH "Directory for Lean's internal cache")

# This function is called by the `setup-data` target.
# It creates all the directories that the Lean FileSystemDataFeed and other components expect.
function(setup_alaris_data_environment)
    message(STATUS "Data: Setting up Alaris data directory structure...")

    # List of required directories for Lean to function correctly.
    set(DATA_DIRS
        "${ALARIS_DATA_DIR}"
        "${ALARIS_DATA_DIR}/symbol-properties"
        "${ALARIS_DATA_DIR}/market-hours"
        "${ALARIS_DATA_DIR}/equity/usa/map_files"
        "${ALARIS_DATA_DIR}/equity/usa/factor_files"
        "${ALARIS_DATA_DIR}/equity/usa/daily"
        "${ALARIS_DATA_DIR}/equity/usa/hour"
        "${ALARIS_DATA_DIR}/equity/usa/minute"
        "${ALARIS_DATA_DIR}/equity/usa/second"
        "${ALARIS_DATA_DIR}/equity/usa/tick"
        "${ALARIS_DATA_DIR}/option/usa/daily"
        "${ALARIS_RESULTS_DIR}"
        "${ALARIS_CACHE_DIR}"
    )

    # Create each directory if it doesn't exist.
    foreach(dir ${DATA_DIRS})
        file(MAKE_DIRECTORY "${dir}")
    endforeach()
    
    message(STATUS "Data: Directory structure created successfully at ${ALARIS_DATA_DIR}")
endfunction()

# This function defines the user-facing build targets for data management.
function(create_data_management_targets)
    # Target to set up the data environment.
    # This simply calls the function to create the directories.
    add_custom_target(setup-data
        COMMAND ${CMAKE_COMMAND} -E echo "Executing data environment setup..."
        COMMAND ${CMAKE_COMMAND} -E cmake_echo_color --blue "--> Creating directories in ${ALARIS_DATA_DIR}"
        COMMAND ${CMAKE_COMMAND} -P "${CMAKE_CURRENT_SOURCE_DIR}/.cmake/DataInlineSetup.cmake"
        COMMENT "Setting up Alaris data directory structure"
        VERBATIM
    )

    # Target to clean all data-related directories.
    # Useful for starting from a clean slate.
    add_custom_target(clean-data
        COMMAND ${CMAKE_COMMAND} -E echo "Cleaning all data, results, and cache directories..."
        COMMAND ${CMAKE_COMMAND} -E remove_directory "${ALARIS_DATA_DIR}"
        COMMAND ${CMAKE_COMMAND} -E remove_directory "${ALARIS_RESULTS_DIR}"
        COMMAND ${CMAKE_COMMAND} -E remove_directory "${ALARIS_CACHE_DIR}"
        COMMENT "Cleaning Alaris data directories"
        VERBATIM
    )

    message(STATUS "Data: Created targets 'setup-data' and 'clean-data'.")
endfunction()

# Create an inline script for the setup-data target to call.
# This is a robust way to ensure the function is called with the correct context.
file(WRITE "${CMAKE_BINARY_DIR}/.cmake/DataInlineSetup.cmake"
"
# This script is executed by the setup-data target.
include(\"${CMAKE_CURRENT_SOURCE_DIR}/.cmake/Data.cmake\")
setup_alaris_data_environment()
"
)

# Always create the data management targets.
create_data_management_targets()

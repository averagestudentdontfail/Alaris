# .cmake/Text/Components.txt
# Component definitions and organization

# Define QuantLib components
set(QUANTLIB_COMPONENTS
    pricing
    volatility
    strategy
    ipc
    core
    tools
)

# Define QuantLib source files with correct paths
set(QUANTLIB_CORE_SOURCES # Renamed for clarity, as this will build 'quantlib_core'
    ${CMAKE_SOURCE_DIR}/src/quantlib/pricing/alo_engine.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/gjrgarch_wrapper.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/vol_forecast.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/strategy/vol_arb.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_memory.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_memory_manager.cpp # Added from file list
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/memory_pool.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/time_trigger.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/event_log.cpp
    # main.cpp for the 'alaris' executable is handled separately
)

# Define QuantLib header files (for reference, not for add_library sources)
# These are made available via target_include_directories
set(QUANTLIB_HEADERS_LIST
    ${CMAKE_SOURCE_DIR}/src/quantlib/pricing/alo_engine.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/gjrgarch_wrapper.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/vol_forecast.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/strategy/vol_arb.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_ring_buffer.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_memory.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/message_types.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/memory_pool.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/time_trigger.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/event_log.h
)

# Define ALL test source files for a single test executable with correct paths
set(ALARIS_TEST_SOURCES
    # Main runner for Google Test
    ${CMAKE_SOURCE_DIR}/test/main_runner.cpp
    # Core tests
    ${CMAKE_SOURCE_DIR}/test/core/event_log_test.cpp
    ${CMAKE_SOURCE_DIR}/test/core/memory_pool_test.cpp
    ${CMAKE_SOURCE_DIR}/test/core/time_trigger_test.cpp
    # IPC tests
    ${CMAKE_SOURCE_DIR}/test/ipc/shared_memory_test.cpp
    # QuantLib specific tests
    ${CMAKE_SOURCE_DIR}/test/quantlib/alo_engine_test.cpp
    ${CMAKE_SOURCE_DIR}/test/quantlib/pricing_test.cpp
    ${CMAKE_SOURCE_DIR}/test/quantlib/volatility_test.cpp
    ${CMAKE_SOURCE_DIR}/test/quantlib/memory_pool_test.cpp # This is test/quantlib/memory_pool_test.cpp
    # Integration tests
    ${CMAKE_SOURCE_DIR}/test/integration/end_to_end_test.cpp
    ${CMAKE_SOURCE_DIR}/test/integration/strategy_integration_test.cpp
    ${CMAKE_SOURCE_DIR}/test/integration/ipc_integration_test.cpp
    # Test helpers .cpp (if it contains definitions needed for linking)
    ${CMAKE_SOURCE_DIR}/test/test_helpers.cpp
)

# Function to create the quantlib_core library
function(create_component_library NAME)
    add_library(${NAME} STATIC
        ${QUANTLIB_CORE_SOURCES} # Only .cpp files
    )

    target_include_directories(${NAME} PUBLIC
        ${CMAKE_SOURCE_DIR}/src # Include directory for headers of the library itself
        ${CMAKE_SOURCE_DIR}/external/quant # For QuantLib headers from submodule
        # Add other necessary include directories for dependencies like Boost, yaml-cpp if not handled globally
    )

    target_link_libraries(${NAME} PUBLIC
        QuantLib::QuantLib # From external/quant
        yaml-cpp           # From external/yaml-cpp
        Threads::Threads
        # Add Boost::system etc. if needed by the library code
    )
    message(STATUS "${NAME} library configured with sources: ${QUANTLIB_CORE_SOURCES}")
endfunction()

# Updated function to create a single test executable for all C++ tests
function(create_alaris_tests_executable)
    if(BUILD_TESTS AND GTest_FOUND)
        message(STATUS "Configuring Alaris C++ tests (alaris_tests)")
        add_executable(alaris_tests ${ALARIS_TEST_SOURCES})

        target_include_directories(alaris_tests PRIVATE
            ${CMAKE_SOURCE_DIR}/src    # For source code headers being tested
            ${CMAKE_SOURCE_DIR}/test   # For test_helpers.h etc.
            ${CMAKE_SOURCE_DIR}/external/quant # For QuantLib headers
            # GTest_INCLUDE_DIRS is often handled by linking GTest::GTest
        )

        target_link_libraries(alaris_tests PRIVATE
            quantlib_core       # Link against our main library
            GTest::GTest
            GTest::Main         # Google Test main (provides main() function)
            Threads::Threads
            yaml-cpp
            # Add Boost or other dependencies if tests use them
        )

        add_test(NAME AlarisCppTests COMMAND alaris_tests)
        set_tests_properties(AlarisCppTests PROPERTIES WORKING_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

        message(STATUS "alaris_tests executable configured with sources: ${ALARIS_TEST_SOURCES}")
    else()
        message(STATUS "Skipping Alaris C++ tests (BUILD_TESTS is OFF or GTest not found).")
    endif()
endfunction()

# Function to configure all C++ components (libraries and executables)
function(configure_all_components)
    # Create the main quantlib_core library
    create_component_library(quantlib_core)

    # Configure the main 'alaris' executable
    add_executable(alaris ${CMAKE_SOURCE_DIR}/src/quantlib/main.cpp)
    target_link_libraries(alaris PRIVATE quantlib_core) # Links against our core library
    message(STATUS "alaris executable configured.")

    # Configure tool executables
    add_executable(alaris-config-validator ${CMAKE_SOURCE_DIR}/src/quantlib/tools/config_validator.cpp)
    target_link_libraries(alaris-config-validator PRIVATE quantlib_core yaml-cpp)
    message(STATUS "alaris-config-validator executable configured.")

    add_executable(alaris-system-info ${CMAKE_SOURCE_DIR}/src/quantlib/tools/system_info.cpp)
    target_link_libraries(alaris-system-info PRIVATE quantlib_core) # Assuming it might need core functionality
    message(STATUS "alaris-system-info executable configured.")

    # Configure the unified test executable
    create_alaris_tests_executable()
endfunction()
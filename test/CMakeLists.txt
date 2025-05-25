# test/CMakeLists.txt
# Testing framework for Alaris

message(STATUS "Configuring Alaris test suite...")

# Ensure testing is enabled (usually called in root CMakeLists.txt)
if(NOT CMAKE_PROJECT_NAME STREQUAL PROJECT_NAME AND NOT CTEST_PROJECT_NAME STREQUAL PROJECT_NAME)
    # If this is a subproject being built directly, enable testing.
    # Otherwise, assume root project has called enable_testing().
    enable_testing()
endif()

# Test configuration
set(TEST_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/test_output") # Changed from /test to /test_output for clarity
set(TEST_DATA_DIRECTORY "${CMAKE_SOURCE_DIR}/test/data") # Assuming test data is here

file(MAKE_DIRECTORY ${TEST_OUTPUT_DIRECTORY})
# file(MAKE_DIRECTORY ${TEST_DATA_DIRECTORY}) # Only create if it's meant to be populated by CMake

# Common test configuration (assuming these are set/available from root or build config modules)
# If not, they need to be defined here or included from a common test config file.
# Example:
# set(TEST_COMMON_INCLUDE_DIRS ${ALARIS_COMMON_INCLUDE_DIRS} ${CMAKE_SOURCE_DIR}/test)
# set(TEST_COMMON_COMPILE_OPTIONS ${ALARIS_COMMON_COMPILE_OPTIONS} $<$<CONFIG:Debug>:-DALARIS_TEST_DEBUG>)
# set(TEST_COMMON_LIBRARIES alaris_core alaris_pricing alaris_volatility alaris_strategy ${GTEST_BOTH_LIBRARIES} Threads::Threads)

# Ensure GTest is found (or your chosen test framework)
# This should ideally be in your root CMakeLists.txt or a dependency management module.
# find_package(GTest REQUIRED) # Example if GTest is used directly here

# Test helper library (links against main Alaris libs and test framework)
create_modern_library(
    NAME alaris_test_helpers
    SOURCES test_helpers.cpp
    PUBLIC_DEPS # Dependencies whose headers are included in test_helpers.h
        # Add Alaris libraries if test_helpers.h includes their headers directly
        # Example: alaris_core (if test_helpers.h uses types from core/memory_pool.h etc.)
    PRIVATE_DEPS # Dependencies used only in test_helpers.cpp
        alaris_core       # For internal use by helpers if not in public API of helpers
        alaris_pricing    # For OptionData, PricingResult if helpers use them
        alaris_volatility # For volatility related structs if used by helpers
        # ${GTEST_LIBRARIES} # If helpers use gtest utilities
        ${TEST_COMMON_LIBRARIES} # General dependencies like QuantLib, YAML, system libs
)
message(STATUS "  + Configured alaris_test_helpers library")

# Core component unit tests
set(CORE_TEST_SOURCES)

# Collect existing test files (QuantLib related tests)
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/quantlib/pricing_test.cpp")
    list(APPEND CORE_TEST_SOURCES "quantlib/pricing_test.cpp")
endif()
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/quantlib/volatility_test.cpp")
    list(APPEND CORE_TEST_SOURCES "quantlib/volatility_test.cpp") # This will need to be updated for GJR-GARCH only
endif()
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/quantlib/memory_pool_test.cpp")
    list(APPEND CORE_TEST_SOURCES "quantlib/memory_pool_test.cpp")
endif()
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/quantlib/alo_engine_test.cpp")
    list(APPEND CORE_TEST_SOURCES "quantlib/alo_engine_test.cpp") # Tests direct ALO Engine use
endif()

# Additional core tests (framework for these should exist or be created)
list(APPEND CORE_TEST_SOURCES
    "core/time_trigger_test.cpp"
    "core/event_log_test.cpp"
    "ipc/shared_memory_test.cpp" # Tests SharedMemoryManager and ring buffers
)

# Create minimal test files if they don't exist (original feature)
# Ensure these placeholder tests use a consistent testing framework (e.g., GTest)
foreach(test_file_relative ${CORE_TEST_SOURCES})
    set(test_file_absolute "${CMAKE_CURRENT_SOURCE_DIR}/${test_file_relative}")
    if(NOT EXISTS "${test_file_absolute}")
        get_filename_component(test_dir "${test_file_absolute}" DIRECTORY)
        file(MAKE_DIRECTORY ${test_dir})
        
        get_filename_component(test_name ${test_file_relative} NAME_WE)
        # Ensure generated file includes necessary headers (e.g., gtest.h, test_helpers.h)
        file(WRITE "${test_file_absolute}"
            "// Minimal test for ${test_name}\n"
            "#include \"../test_helpers.h\" // Assuming test_helpers.h is one level up or adjust path\n"
            "#include <gtest/gtest.h> // Example if using GTest\n"
            "\n"
            "TEST(${test_name}, BasicAssertion) {\n"
            "    ASSERT_TRUE(true);\n"
            "}\n"
            "\n"
            "// TODO: Add actual tests for ${test_name}\n"
        )
        message(STATUS "Created minimal test file: ${test_file_relative}")
    endif()
endforeach()

# Create core test executable
if(CORE_TEST_SOURCES)
    create_modern_test( # This custom function should handle linking GTest/framework
        NAME alaris_core_tests # Renamed for clarity (plural)
        SOURCES ${CORE_TEST_SOURCES}
        DEPS # Dependencies for these tests
            alaris_test_helpers
            alaris_core
            alaris_pricing    # For pricing_test, alo_engine_test
            alaris_volatility # For volatility_test
            # Add your test framework library here, e.g. ${GTEST_BOTH_LIBRARIES}
            # The DEPS for create_modern_test should include libraries needed by the SOURCES
    )
    
    set_tests_properties(alaris_core_tests PROPERTIES
        TIMEOUT 60
        LABELS "core;unit"
        WORKING_DIRECTORY ${TEST_OUTPUT_DIRECTORY} # Run tests from a clean output directory
        # ENVIRONMENT "ALARIS_TEST_DATA_DIR=${TEST_DATA_DIRECTORY};LD_LIBRARY_PATH=..."
    )
    message(STATUS "  + Configured alaris_core_tests executable")
else()
    message(WARNING "No core test sources found or specified.")
endif()

# Integration tests
set(INTEGRATION_TEST_SOURCES)

if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/integration/end_to_end_test.cpp")
    list(APPEND INTEGRATION_TEST_SOURCES "integration/end_to_end_test.cpp")
endif()
# Ensure strategy_integration_test.cpp is updated for the simplified strategy
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/integration/strategy_integration_test.cpp")
    list(APPEND INTEGRATION_TEST_SOURCES "integration/strategy_integration_test.cpp")
endif()
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/integration/ipc_integration_test.cpp")
    list(APPEND INTEGRATION_TEST_SOURCES "integration/ipc_integration_test.cpp")
endif()

# Create minimal integration test files (original feature)
foreach(test_file_relative ${INTEGRATION_TEST_SOURCES})
    set(test_file_absolute "${CMAKE_CURRENT_SOURCE_DIR}/${test_file_relative}")
    if(NOT EXISTS "${test_file_absolute}")
        get_filename_component(test_dir "${test_file_absolute}" DIRECTORY)
        file(MAKE_DIRECTORY ${test_dir})
        
        get_filename_component(test_name ${test_file_relative} NAME_WE)
        file(WRITE "${test_file_absolute}"
            "// Minimal integration test for ${test_name}\n"
            "#include \"../test_helpers.h\"\n"
            "#include <gtest/gtest.h> // Example if using GTest\n"
            "\n"
            "TEST(${test_name}, BasicIntegration) {\n"
            "    ASSERT_TRUE(true);\n"
            "}\n"
            "\n"
            "// TODO: Add actual integration tests for ${test_name}\n"
        )
        message(STATUS "Created minimal integration test: ${test_file_relative}")
    endif()
endforeach()

# Create integration test executable
if(INTEGRATION_TEST_SOURCES)
    create_modern_test(
        NAME alaris_integration_tests # Renamed for clarity
        SOURCES ${INTEGRATION_TEST_SOURCES}
        DEPS # Dependencies for integration tests
            alaris_test_helpers
            alaris_strategy   # Strategy integration tests will need this
            alaris_core       # Other Alaris libs as needed by the tests
            alaris_pricing
            alaris_volatility
            # Add your test framework library here
    )
    
    set_tests_properties(alaris_integration_tests PROPERTIES
        TIMEOUT 120
        LABELS "integration"
        WORKING_DIRECTORY ${TEST_OUTPUT_DIRECTORY}
        # ENVIRONMENT "ALARIS_TEST_DATA_DIR=${TEST_DATA_DIRECTORY};..."
    )
    message(STATUS "  + Configured alaris_integration_tests executable")
else()
    message(WARNING "No integration test sources found or specified.")
endif()

# Set up folder organization for IDE
set_target_properties(
    alaris_test_helpers
    PROPERTIES FOLDER "Alaris/Tests/Helpers"
)
if(TARGET alaris_core_tests)
    set_target_properties(alaris_core_tests PROPERTIES FOLDER "Alaris/Tests/Unit")
endif()
if(TARGET alaris_integration_tests)
    set_target_properties(alaris_integration_tests PROPERTIES FOLDER "Alaris/Tests/Integration")
endif()


# Export test targets to parent scope (optional, depends on how you manage builds/installs)
set(ALARIS_TEST_EXECUTABLES PARENT_SCOPE)
if(TARGET alaris_core_tests)
    list(APPEND ALARIS_TEST_EXECUTABLES alaris_core_tests)
endif()
if(TARGET alaris_integration_tests)
    list(APPEND ALARIS_TEST_EXECUTABLES alaris_integration_tests)
endif()
set(ALARIS_TEST_LIBRARIES alaris_test_helpers PARENT_SCOPE)


message(STATUS "Alaris test suite configuration finished.")
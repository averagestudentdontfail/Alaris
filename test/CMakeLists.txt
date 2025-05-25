# test/CMakeLists.txt
message(STATUS "Configuring Alaris test suite...")

# Find testing dependencies
find_package(Threads REQUIRED)

# Common test configuration
set(TEST_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/test_output")
file(MAKE_DIRECTORY ${TEST_OUTPUT_DIRECTORY})

# Test helper library
add_library(alaris_test_helpers STATIC
    test_helpers.cpp
    test_helpers.h
)
target_include_directories(alaris_test_helpers PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)
target_link_libraries(alaris_test_helpers PUBLIC
    alaris_core
    Threads::Threads
)
set_target_properties(alaris_test_helpers PROPERTIES
    CXX_STANDARD ${CMAKE_CXX_STANDARD}
    CXX_STANDARD_REQUIRED ON
    CXX_EXTENSIONS OFF
)

# Collect test sources
set(CORE_TEST_SOURCES
    core/time_trigger_test.cpp
    core/event_log_test.cpp
    ipc/shared_memory_test.cpp
)

# Add QuantLib-specific tests if they exist
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/quantlib/pricing_test.cpp")
    list(APPEND CORE_TEST_SOURCES quantlib/pricing_test.cpp)
endif()
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/quantlib/volatility_test.cpp")
    list(APPEND CORE_TEST_SOURCES quantlib/volatility_test.cpp)
endif()
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/quantlib/memory_pool_test.cpp")
    list(APPEND CORE_TEST_SOURCES quantlib/memory_pool_test.cpp)
endif()
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/quantlib/alo_engine_test.cpp")
    list(APPEND CORE_TEST_SOURCES quantlib/alo_engine_test.cpp)
endif()

# Core unit tests
if(CORE_TEST_SOURCES)
    add_executable(alaris_core_tests ${CORE_TEST_SOURCES})
    target_link_libraries(alaris_core_tests PRIVATE
        alaris_test_helpers
        alaris_core
        alaris_pricing
        alaris_volatility
        alaris_strategy
        ${YAML_CPP_TARGET}
        Threads::Threads
    )
    set_target_properties(alaris_core_tests PROPERTIES
        CXX_STANDARD ${CMAKE_CXX_STANDARD}
        CXX_STANDARD_REQUIRED ON
        CXX_EXTENSIONS OFF
        RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    )
    
    add_test(
        NAME AlarisCoreTests
        COMMAND alaris_core_tests
        WORKING_DIRECTORY ${TEST_OUTPUT_DIRECTORY}
    )
    set_tests_properties(AlarisCoreTests PROPERTIES
        TIMEOUT 60
        LABELS "core;unit"
    )
endif()

# Integration test sources
set(INTEGRATION_TEST_SOURCES)
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/integration/end_to_end_test.cpp")
    list(APPEND INTEGRATION_TEST_SOURCES integration/end_to_end_test.cpp)
endif()
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/integration/strategy_integration_test.cpp")
    list(APPEND INTEGRATION_TEST_SOURCES integration/strategy_integration_test.cpp)
endif()
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/integration/ipc_integration_test.cpp")
    list(APPEND INTEGRATION_TEST_SOURCES integration/ipc_integration_test.cpp)
endif()

# Integration tests
if(INTEGRATION_TEST_SOURCES)
    add_executable(alaris_integration_tests ${INTEGRATION_TEST_SOURCES})
    target_link_libraries(alaris_integration_tests PRIVATE
        alaris_test_helpers
        alaris_core
        alaris_pricing
        alaris_volatility
        alaris_strategy
        ${YAML_CPP_TARGET}
        ${QUANTLIB_TARGET}
        Threads::Threads
    )
    set_target_properties(alaris_integration_tests PROPERTIES
        CXX_STANDARD ${CMAKE_CXX_STANDARD}
        CXX_STANDARD_REQUIRED ON
        CXX_EXTENSIONS OFF
        RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    )
    
    add_test(
        NAME AlarisIntegrationTests
        COMMAND alaris_integration_tests
        WORKING_DIRECTORY ${TEST_OUTPUT_DIRECTORY}
    )
    set_tests_properties(AlarisIntegrationTests PROPERTIES
        TIMEOUT 120
        LABELS "integration"
    )
endif()

# Create a custom target to run all tests
add_custom_target(run_tests
    COMMAND ${CMAKE_CTEST_COMMAND} --output-on-failure
    DEPENDS alaris_core_tests alaris_integration_tests
    WORKING_DIRECTORY ${CMAKE_BINARY_DIR}
    COMMENT "Running all Alaris tests"
)

message(STATUS "Test suite configuration complete")
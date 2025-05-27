# .cmake/Text/Components.txt

# Define QuantLib components
set(QUANTLIB_COMPONENTS
    pricing
    volatility
    strategy
    ipc
    core
    tools
)

# Define QuantLib source files
set(QUANTLIB_SOURCES
    pricing/alo_engine.cpp
    volatility/gjrgarch_wrapper.cpp
    volatility/vol_forecast.cpp
    strategy/vol_arb.cpp
    ipc/shared_memory.cpp
    ipc/process_manager.cpp 
    core/memory_pool.cpp
    core/time_trigger.cpp
    core/event_log.cpp
    main.cpp
)

# Define QuantLib header files
set(QUANTLIB_HEADERS
    pricing/alo_engine.h
    volatility/gjrgarch_wrapper.h
    volatility/vol_forecast.h
    strategy/vol_arb.h
    ipc/shared_ring_buffer.h
    ipc/shared_memory.h
    ipc/message_types.h
    core/memory_pool.h
    core/time_trigger.h
    core/event_log.h
)

# Define ALL test source files for a single test executable
set(ALARIS_TEST_SOURCES
    # Main runner for Google Test
    test/main_runner.cpp

    # Core tests
    test/core/event_log_test.cpp
    test/core/memory_pool_test.cpp
    test/core/time_trigger_test.cpp

    # IPC tests
    test/ipc/shared_memory_test.cpp
    # test/ipc/ipc_integration_test.cpp # Combined or covered elsewhere

    # QuantLib specific tests
    test/quantlib/alo_engine_test.cpp
    test/quantlib/pricing_test.cpp
    test/quantlib/volatility_test.cpp
    # test/quantlib/memory_pool_test.cpp # This was duplicated, assuming it's core

    # Integration tests
    test/integration/end_to_end_test.cpp
    test/integration/strategy_integration_test.cpp
    test/integration/ipc_integration_test.cpp # Keeping this distinct if it has more complex setup
)

set(ALARIS_TEST_HEADERS
    test/test_helpers.h 
)

# Function to create a component library (remains unchanged)
function(create_component_library NAME)
    add_library(${NAME} STATIC
        ${QUANTLIB_SOURCES}
        ${QUANTLIB_HEADERS}
    )

    target_include_directories(${NAME} PUBLIC
        ${CMAKE_SOURCE_DIR}/src
    )

    target_link_libraries(${NAME} PUBLIC
        QuantLib::QuantLib
        yaml-cpp
        Threads::Threads
    )
endfunction()

# Updated function to create a single test executable for all C++ tests
function(create_alaris_tests_executable)
    if(BUILD_TESTS AND GTest_FOUND)
        message(STATUS "Configuring Alaris C++ tests (alaris_tests)")
        add_executable(alaris_tests ${ALARIS_TEST_SOURCES})

        target_include_directories(alaris_tests PRIVATE
            ${CMAKE_SOURCE_DIR}/src # For QuantLib headers
            ${CMAKE_SOURCE_DIR}/test # For test_helpers.h etc.
            ${PROJECT_SOURCE_DIR} # Access to root CMakeLists.txt defined variables if any.
            ${GTEST_INCLUDE_DIRS} # For GTest headers
        )

        target_link_libraries(alaris_tests PRIVATE
            quantlib_core       # Link against our main library
            GTest::GTest        # Google Test library
            GTest::Main         # Google Test main (provides main() function)
            Threads::Threads    # If tests use threads
            yaml-cpp            # If tests use yaml-cpp
            # Add other dependencies if tests need them (e.g. Boost)
        )
        
        # Add test to CTest
        # The COMMAND can be improved to run with arguments or specific configurations if needed
        add_test(NAME AlarisCppTests COMMAND alaris_tests)
        
        # Optionally, set properties for the test, like working directory
        set_tests_properties(AlarisCppTests PROPERTIES WORKING_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

        message(STATUS "alaris_tests executable configured.")
    else()
        message(STATUS "Skipping Alaris C++ tests (BUILD_TESTS is OFF or GTest not found).")
    endif()
endfunction()

# This function is part of the overall project setup.
function(configure_all_components)
    create_component_library(quantlib_core)
    message(STATUS "quantlib_core library configured.")
    if (TARGET quantlib_core) # Ensure core library is configured first
        add_executable(alaris src/quantlib/main.cpp)
        target_link_libraries(alaris PRIVATE quantlib_core GTest::Main) # GTest::Main added for main.cpp's include of event_log.h -> time_trigger.h -> gtest.h
        message(STATUS "alaris executable configured.")
    endif()
    # Configure the unified test executable
    create_alaris_tests_executable()
endfunction()
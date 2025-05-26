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

# Define test components
set(TEST_COMPONENTS
    core
    integration
    ipc
    quantlib
    csharp
)

# Define test source files
set(TEST_SOURCES
    test_helpers.cpp
    core/core_tests.cpp
    integration/integration_tests.cpp
    ipc/ipc_tests.cpp
    quantlib/quantlib_tests.cpp
)

# Define test header files
set(TEST_HEADERS
    test_helpers.h
)

# Function to create a component library
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

# Function to create test executable
function(create_test_executable NAME)
    add_executable(${NAME}
        ${TEST_SOURCES}
        ${TEST_HEADERS}
    )
    
    target_include_directories(${NAME} PRIVATE
        ${CMAKE_SOURCE_DIR}/test
    )
    
    target_link_libraries(${NAME} PRIVATE
        GTest::GTest
        GTest::Main
    )
    
    add_test(NAME ${NAME} COMMAND ${NAME})
endfunction() 
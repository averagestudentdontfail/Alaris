# .cmake/Components.cmake
# Component definitions and organization - Multi-target approach

# Define QuantLib components
set(QUANTLIB_COMPONENTS
    pricing
    volatility
    strategy
    ipc
    core
)

# Define QuantLib LIBRARY source files (excluding main.cpp and tools)
set(QUANTLIB_LIB_SOURCES
    pricing/alo_engine.cpp
    volatility/gjrgarch_wrapper.cpp
    volatility/vol_forecast.cpp
    strategy/vol_arb.cpp
    ipc/shared_memory.cpp
    ipc/shared_memory_manager.cpp
    core/memory_pool.cpp
    core/time_trigger.cpp
    core/event_log.cpp
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

# Define executable sources (each has main() function)
set(QUANTLIB_MAIN_SOURCE src/quantlib/main.cpp)
set(CONFIG_VALIDATOR_SOURCE src/quantlib/tools/config_validator.cpp)
set(SYSTEM_INFO_SOURCE src/quantlib/tools/system_info.cpp)

# Define test source files
set(TEST_SOURCES
    test_helpers.cpp
    core/time_trigger_test.cpp
    core/event_log_test.cpp
    ipc/shared_memory_test.cpp
    quantlib/alo_engine_test.cpp
    quantlib/memory_pool_test.cpp
    quantlib/pricing_test.cpp
    quantlib/volatility_test.cpp
    integration/end_to_end_test.cpp
    integration/ipc_integration_test.cpp
    integration/strategy_integration_test.cpp
)

# Function to create the QuantLib core library
function(create_quantlib_library)
    # Create shared library with all the core functionality
    add_library(quantlib_core STATIC)
    
    # Add source files with proper path prefixes
    foreach(SOURCE ${QUANTLIB_LIB_SOURCES})
        target_sources(quantlib_core PRIVATE ${CMAKE_SOURCE_DIR}/src/quantlib/${SOURCE})
    endforeach()
    
    # Add header files
    foreach(HEADER ${QUANTLIB_HEADERS})
        target_sources(quantlib_core PRIVATE ${CMAKE_SOURCE_DIR}/src/quantlib/${HEADER})
    endforeach()
    
    # Set include directories
    target_include_directories(quantlib_core 
        PUBLIC 
            ${CMAKE_SOURCE_DIR}/src
            ${CMAKE_SOURCE_DIR}/src/quantlib
        PRIVATE
            ${CMAKE_BINARY_DIR}
    )
    
    # Link against external dependencies
    target_link_libraries(quantlib_core 
        PUBLIC
            ${ALARIS_QUANTLIB_LIBRARIES}
            ${ALARIS_YAMLCPP_LIBRARIES}
            Threads::Threads
    )
    
    # Set compiler features
    target_compile_features(quantlib_core PUBLIC cxx_std_20)
    
    message(STATUS "Created quantlib_core library")
endfunction()

# Function to create main process executable
function(create_main_executable)
    add_executable(alaris ${QUANTLIB_MAIN_SOURCE})
    
    target_link_libraries(alaris 
        PRIVATE 
            quantlib_core
    )
    
    # Install the main executable
    install(TARGETS alaris
        RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
        COMPONENT Runtime
    )
    
    message(STATUS "Created alaris main executable")
endfunction()

# Function to create config validator tool
function(create_config_validator)
    add_executable(alaris-config-validator ${CONFIG_VALIDATOR_SOURCE})
    
    target_link_libraries(alaris-config-validator 
        PRIVATE 
            ${ALARIS_YAMLCPP_LIBRARIES}
            Threads::Threads
    )
    
    target_include_directories(alaris-config-validator 
        PRIVATE 
            ${CMAKE_SOURCE_DIR}/src
            ${CMAKE_BINARY_DIR}
    )
    
    # Install the config validator tool
    install(TARGETS alaris-config-validator
        RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
        COMPONENT Tools
    )
    
    message(STATUS "Created alaris-config-validator tool")
endfunction()

# Function to create system info tool
function(create_system_info_tool)
    add_executable(alaris-system-info ${SYSTEM_INFO_SOURCE})
    
    target_link_libraries(alaris-system-info 
        PRIVATE 
            Threads::Threads
    )
    
    target_include_directories(alaris-system-info 
        PRIVATE 
            ${CMAKE_SOURCE_DIR}/src
            ${CMAKE_BINARY_DIR}
    )
    
    # Install the system info tool
    install(TARGETS alaris-system-info
        RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
        COMPONENT Tools
    )
    
    message(STATUS "Created alaris-system-info tool")
endfunction()

# Function to create test executable
function(create_test_executable)
    if(NOT BUILD_TESTS OR NOT GTest_FOUND)
        message(STATUS "Skipping test creation - BUILD_TESTS=${BUILD_TESTS}, GTest_FOUND=${GTest_FOUND}")
        return()
    endif()
    
    add_executable(alaris_tests)
    
    # Add test source files with proper path prefixes
    foreach(SOURCE ${TEST_SOURCES})
        target_sources(alaris_tests PRIVATE ${CMAKE_SOURCE_DIR}/test/${SOURCE})
    endforeach()
    
    target_include_directories(alaris_tests 
        PRIVATE
            ${CMAKE_SOURCE_DIR}/test
            ${CMAKE_SOURCE_DIR}/src
    )
    
    target_link_libraries(alaris_tests 
        PRIVATE
            quantlib_core
            GTest::GTest
            GTest::Main
    )
    
    # Register tests with CTest
    add_test(NAME alaris_unit_tests COMMAND alaris_tests)
    
    message(STATUS "Created alaris_tests executable")
endfunction()

# Master function to configure all components
function(configure_all_components)
    # Create the core library first
    create_quantlib_library()
    
    # Create executables that depend on the library
    create_main_executable()
    create_config_validator()
    create_system_info_tool()
    
    # Create tests if enabled
    create_test_executable()
    
    message(STATUS "All components configured successfully")
endfunction()
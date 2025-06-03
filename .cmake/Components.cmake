# .cmake/Components.cmake
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
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/garch_wrapper.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/vol_forecast.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/strategy/vol_arb.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_memory.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_memory_manager.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/memory_pool.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/task_scheduler.cpp
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/event_log.cpp
    # main.cpp for the 'alaris' executable is handled separately
)

# Define QuantLib header files (for reference, not for add_library sources)
# These are made available via target_include_directories
set(QUANTLIB_HEADERS_LIST
    ${CMAKE_SOURCE_DIR}/src/quantlib/pricing/alo_engine.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/garch_wrapper.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/volatility/vol_forecast.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/strategy/vol_arb.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_ring_buffer.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/shared_memory.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/ipc/message_types.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/memory_pool.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/task_scheduler.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/time_type.h
    ${CMAKE_SOURCE_DIR}/src/quantlib/core/event_log.h
)

# Function to create the quantlib_core library
function(create_component_library NAME)
    add_library(${NAME} STATIC
        ${QUANTLIB_CORE_SOURCES} # Only .cpp files
    )

    target_include_directories(${NAME} PUBLIC
        ${CMAKE_SOURCE_DIR}/src # This allows includes like "quantlib/pricing/alo_engine.h" from within the library
        ${CMAKE_SOURCE_DIR}/external/quant # For QuantLib headers from submodule
    )

    target_link_libraries(${NAME} PUBLIC
        ${QUANTLIB_TARGET} # Use the variable holding the correct QuantLib target name
        yaml-cpp           # From external/yaml-cpp
        Threads::Threads
    )
    message(STATUS "${NAME} library configured with sources: ${QUANTLIB_CORE_SOURCES}")
endfunction()

# Function to configure Lean integration
function(configure_lean_integration)
    # Copy lean.json to build directory if it exists
    if(EXISTS "${CMAKE_SOURCE_DIR}/lean.json")
        configure_file(
            "${CMAKE_SOURCE_DIR}/lean.json"
            "${CMAKE_BINARY_DIR}/lean.json"
            COPYONLY
        )
        message(STATUS "lean.json copied to build directory")
    else()
        message(WARNING "lean.json not found - run scripts/setup.sh to create it")
    endif()
    
    # Copy config directory to build directory
    if(EXISTS "${CMAKE_SOURCE_DIR}/config")
        file(COPY "${CMAKE_SOURCE_DIR}/config"
             DESTINATION "${CMAKE_BINARY_DIR}")
        message(STATUS "Config directory copied to build directory")
    endif()
    
    # Create symbolic links to data and results directories in build dir
    # This allows the C# process to run from build directory
    set(BUILD_DATA_DIR "${CMAKE_BINARY_DIR}/data")
    set(BUILD_RESULTS_DIR "${CMAKE_BINARY_DIR}/results")
    set(BUILD_CACHE_DIR "${CMAKE_BINARY_DIR}/cache")
    
    if(NOT EXISTS "${BUILD_DATA_DIR}")
        if(UNIX)
            execute_process(
                COMMAND ${CMAKE_COMMAND} -E create_symlink
                "${CMAKE_SOURCE_DIR}/data"
                "${BUILD_DATA_DIR}"
                RESULT_VARIABLE symlink_result
            )
            if(symlink_result EQUAL 0)
                message(STATUS "Created symlink: ${BUILD_DATA_DIR} -> ${CMAKE_SOURCE_DIR}/data")
            else()
                # Fallback to copying if symlink fails
                file(COPY "${CMAKE_SOURCE_DIR}/data" DESTINATION "${CMAKE_BINARY_DIR}")
                message(STATUS "Copied data directory to build (symlink failed)")
            endif()
        else()
            # On Windows, just copy the directory
            file(COPY "${CMAKE_SOURCE_DIR}/data" DESTINATION "${CMAKE_BINARY_DIR}")
            message(STATUS "Copied data directory to build directory")
        endif()
    endif()
    
    # Create results and cache directories in build
    file(MAKE_DIRECTORY "${BUILD_RESULTS_DIR}")
    file(MAKE_DIRECTORY "${BUILD_CACHE_DIR}")
    
    # Create symbolic links for results and cache (or copy on Windows)
    if(UNIX)
        if(NOT EXISTS "${CMAKE_SOURCE_DIR}/results")
            file(MAKE_DIRECTORY "${CMAKE_SOURCE_DIR}/results")
        endif()
        if(NOT EXISTS "${CMAKE_SOURCE_DIR}/cache")
            file(MAKE_DIRECTORY "${CMAKE_SOURCE_DIR}/cache")
        endif()
        
        # Create symlinks back to source directory so results are preserved
        execute_process(
            COMMAND ${CMAKE_COMMAND} -E create_symlink
            "${CMAKE_SOURCE_DIR}/results"
            "${BUILD_RESULTS_DIR}"
            RESULT_VARIABLE results_symlink_result
        )
        execute_process(
            COMMAND ${CMAKE_COMMAND} -E create_symlink
            "${CMAKE_SOURCE_DIR}/cache"
            "${BUILD_CACHE_DIR}"
            RESULT_VARIABLE cache_symlink_result
        )
        
        if(results_symlink_result EQUAL 0)
            message(STATUS "Created symlink: ${BUILD_RESULTS_DIR} -> ${CMAKE_SOURCE_DIR}/results")
        endif()
        if(cache_symlink_result EQUAL 0)
            message(STATUS "Created symlink: ${BUILD_CACHE_DIR} -> ${CMAKE_SOURCE_DIR}/cache")
        endif()
    endif()
    
    message(STATUS "Lean integration configured")
endfunction()

# Function to configure all C++ components (libraries and executables)
function(configure_all_components)
    # Create the main quantlib library
    create_component_library(quantlib)

    # Configure the main 'alaris' executable
    add_executable(alaris ${CMAKE_SOURCE_DIR}/src/quantlib/main.cpp)
    # Includes for 'alaris' executable
    target_include_directories(alaris PRIVATE
        ${CMAKE_SOURCE_DIR}/src                # Allows #include "quantlib/..." for its own sources
        ${CMAKE_SOURCE_DIR}/external/quant     # For QuantLib headers
    )
    target_link_libraries(alaris PRIVATE quantlib) 
    message(STATUS "alaris executable configured.")

    # Configure the quantlib-process executable
    add_executable(quantlib-process ${CMAKE_SOURCE_DIR}/src/quantlib/main.cpp)
    target_include_directories(quantlib-process PRIVATE
        ${CMAKE_SOURCE_DIR}/src
        ${CMAKE_SOURCE_DIR}/external/quant
    )
    target_link_libraries(quantlib-process PRIVATE quantlib)
    message(STATUS "quantlib-process executable configured.")

    # Configure tool executables
    add_executable(alaris-config ${CMAKE_SOURCE_DIR}/src/quantlib/tools/config.cpp)
    target_include_directories(alaris-config PRIVATE
        ${CMAKE_SOURCE_DIR}/src
        ${CMAKE_SOURCE_DIR}/external/yaml-cpp/include # If yaml-cpp headers are needed directly
    )
    target_link_libraries(alaris-config PRIVATE quantlib yaml-cpp)
    message(STATUS "alaris-config executable configured.")

    add_executable(alaris-system ${CMAKE_SOURCE_DIR}/src/quantlib/tools/system.cpp)
    target_include_directories(alaris-system PRIVATE
        ${CMAKE_SOURCE_DIR}/src
    )
    target_link_libraries(alaris-system PRIVATE quantlib) 
    message(STATUS "alaris-system executable configured.")

    # Configure Lean integration at the end
    configure_lean_integration()
    
    message(STATUS "All components configured successfully")
endfunction()
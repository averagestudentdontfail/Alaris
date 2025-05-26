# external/CMakeLists.txt
# External dependencies management for Alaris

message(STATUS "Building external dependencies (QuantLib & yaml-cpp)...")

# Ensure we're building as static libraries for deterministic deployment
# This can also be set globally by the root CMakeLists.txt or BuildConfig.cmake
set(BUILD_SHARED_LIBS OFF CACHE BOOL "Build shared libraries for externals" FORCE)

# --- Configure QuantLib ---
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/quant/CMakeLists.txt")
    message(STATUS "Configuring QuantLib from source (external/quant)...")
    
    # QuantLib build configuration (these are passed to QuantLib's CMake)
    set(QL_BUILD_BENCHMARK OFF CACHE BOOL "Build QuantLib benchmark" FORCE)
    set(QL_BUILD_EXAMPLES OFF CACHE BOOL "Build QuantLib examples" FORCE)
    set(QL_BUILD_TEST_SUITE OFF CACHE BOOL "Build QuantLib test suite" FORCE)
    set(QL_ENABLE_SESSIONS OFF CACHE BOOL "Enable QuantLib sessions" FORCE)
    set(QL_ENABLE_THREAD_SAFE_OBSERVER_PATTERN OFF CACHE BOOL "Thread-safe observer" FORCE)
    set(QL_ENABLE_PARALLEL_UNIT_TEST_RUNNER OFF CACHE BOOL "Parallel unit tests" FORCE)
    set(QL_HIGH_RESOLUTION_DATE ON CACHE BOOL "High resolution dates" FORCE)
    set(QL_USE_STD_SHARED_PTR ON CACHE BOOL "Use std::shared_ptr" FORCE)
    set(QL_ERROR_FUNCTIONS OFF CACHE BOOL "Disable error functions for performance" FORCE) # For Release
    set(QL_ERROR_LINES OFF CACHE BOOL "Disable error line info for performance" FORCE)   # For Release
    set(QL_ENABLE_TRACING OFF CACHE BOOL "Disable tracing for performance" FORCE)        # For Release
    set(QL_USE_DISPOSABLE OFF CACHE BOOL "Disable disposable pattern for deterministic memory" FORCE)
    # set(QL_INSTALL OFF CACHE BOOL "Don't install QuantLib separately from Alaris build" FORCE) # Typically handled by Alaris install rules

    # Configure Boost usage within QuantLib
    # Boost_FOUND should be set by DependencyManager.cmake if system Boost is available
    if(Boost_FOUND)
        message(STATUS "System Boost found, configuring QuantLib to use external Boost.")
        set(QL_EXTERNAL_BOOST ON CACHE BOOL "Use external Boost" FORCE)
    else()
        message(STATUS "System Boost not found, QuantLib will use its internal/bundled Boost.")
        set(QL_EXTERNAL_BOOST OFF CACHE BOOL "Use QuantLib's internal Boost" FORCE)
    endif()
    
    add_subdirectory(quant) # This builds QuantLib from external/quant
    
    # Determine the actual QuantLib target name and create a consistent alias "QuantLib"
    # The log indicated "ql_library" was built.
    set(FOUND_QL_TARGET_NAME "")
    if(TARGET ql_library)
        set(FOUND_QL_TARGET_NAME ql_library)
    elseif(TARGET QuantLib)
        set(FOUND_QL_TARGET_NAME QuantLib)
    elseif(TARGET quantlib)
        set(FOUND_QL_TARGET_NAME quantlib)
    elseif(TARGET ql)
        set(FOUND_QL_TARGET_NAME ql)
    endif()
    
    if(NOT FOUND_QL_TARGET_NAME STREQUAL "")
        set(QUANTLIB_TARGET ${FOUND_QL_TARGET_NAME} PARENT_SCOPE)
        if(NOT TARGET QuantLib) # Create alias if 'QuantLib' isn't the actual name
            add_library(QuantLib ALIAS ${FOUND_QL_TARGET_NAME})
            message(STATUS "✓ QuantLib target: ${FOUND_QL_TARGET_NAME} (aliased as QuantLib)")
        else()
            message(STATUS "✓ QuantLib target is directly: ${FOUND_QL_TARGET_NAME}")
        endif()
        
        # Define QuantLib include directories for the parent scope (Alaris project)
        set(QuantLib_INCLUDE_DIRS
            "${CMAKE_CURRENT_SOURCE_DIR}/quant" # Source dir of QuantLib submodule
            "${CMAKE_CURRENT_BINARY_DIR}/quant"  # Build dir of QuantLib submodule (for generated headers)
            PARENT_SCOPE
        )
        
        # Apply specific compile definitions to the found QuantLib target
        # These ensure deterministic behavior and performance settings.
        target_compile_definitions(${FOUND_QL_TARGET_NAME} INTERFACE # Or PUBLIC if needed by users of QL includes
            QL_HIGH_RESOLUTION_DATE
            QL_USE_STD_SHARED_PTR
            $<$<CONFIG:Release>:QL_ERROR_FUNCTIONS=0>
            $<$<CONFIG:Release>:QL_ERROR_LINES=0>
            $<$<CONFIG:Release>:QL_ENABLE_TRACING=0>
            $<$<CONFIG:Release>:QL_USE_DISPOSABLE=0>
        )
        
        if(CMAKE_BUILD_TYPE STREQUAL "Release")
            target_compile_options(${FOUND_QL_TARGET_NAME} PRIVATE # Or INTERFACE if these should affect users
                -ffast-math
                -fno-finite-math-only # Important for some financial calculations
            )
        endif()
    else()
        message(FATAL_ERROR "Could not determine QuantLib target name after building from 'external/quant'. Expected one of ql_library, QuantLib, quantlib, or ql.")
    endif()
else()
    message(FATAL_ERROR "QuantLib submodule directory 'external/quant' does not contain CMakeLists.txt.")
endif()

# --- Configure yaml-cpp ---
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/yaml-cpp/CMakeLists.txt")
    message(STATUS "Configuring yaml-cpp from source (external/yaml-cpp)...")
    
    set(YAML_CPP_BUILD_TESTS OFF CACHE BOOL "Build yaml-cpp tests" FORCE)
    set(YAML_CPP_BUILD_TOOLS OFF CACHE BOOL "Build yaml-cpp tools" FORCE)
    set(YAML_CPP_BUILD_CONTRIB OFF CACHE BOOL "Build yaml-cpp contrib" FORCE)
    # set(YAML_CPP_INSTALL OFF CACHE BOOL "Install yaml-cpp separately" FORCE) # Let Alaris control installation
    set(YAML_BUILD_SHARED_LIBS OFF CACHE BOOL "Build yaml-cpp as static" FORCE) # Ensure static
    
    add_subdirectory(yaml-cpp) # This builds yaml-cpp from external/yaml-cpp
    
    if(TARGET yaml-cpp)
        set(YAML_CPP_TARGET yaml-cpp PARENT_SCOPE)
        set(yaml-cpp_INCLUDE_DIRS 
            "${CMAKE_CURRENT_SOURCE_DIR}/yaml-cpp/include"
            PARENT_SCOPE
        )
        message(STATUS "✓ yaml-cpp target: yaml-cpp")
    else()
        message(FATAL_ERROR "yaml-cpp target not found after building from 'external/yaml-cpp'.")
    endif()
else()
    message(FATAL_ERROR "yaml-cpp submodule directory 'external/yaml-cpp' does not contain CMakeLists.txt.")
endif()

# --- Lean Integration ---
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/lean")
    message(STATUS "Configuring Lean from source (external/lean)...")
    
    # Check for .NET SDK
    find_program(DOTNET_EXECUTABLE dotnet)
    if(NOT DOTNET_EXECUTABLE)
        message(FATAL_ERROR "dotnet executable not found. Please install .NET SDK.")
    endif()
    
    # Set Lean paths
    set(LEAN_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/lean")
    set(LEAN_AVAILABLE TRUE PARENT_SCOPE)
    set(LEAN_ROOT_DIR "${LEAN_ROOT}" PARENT_SCOPE)
    
    # Configure Lean build
    set(LEAN_BUILD_DIR "${CMAKE_CURRENT_BINARY_DIR}/lean")
    file(MAKE_DIRECTORY "${LEAN_BUILD_DIR}")
    
    # Add custom target for Lean build
    add_custom_target(lean_build
        COMMAND ${DOTNET_EXECUTABLE} build "${LEAN_ROOT}/QuantConnect.Lean.sln" -c Release
        WORKING_DIRECTORY "${LEAN_ROOT}"
        COMMENT "Building Lean from source"
    )
    
    # Set Lean output directories
    set(LEAN_BIN_DIR "${LEAN_ROOT}/Launcher/bin/Release" PARENT_SCOPE)
    set(LEAN_ALGORITHM_DIR "${LEAN_ROOT}/Algorithm.CSharp/bin/Release" PARENT_SCOPE)
    
    message(STATUS "✓ Lean configuration complete")
else()
    message(FATAL_ERROR "Lean submodule 'external/lean' not found or empty. This is required for the build.")
endif()

# Set up folder organization for IDE for external targets built here
if(TARGET ${QUANTLIB_TARGET})
    set_target_properties(${QUANTLIB_TARGET} PROPERTIES FOLDER "External/Libs")
endif()
if(TARGET yaml-cpp)
    set_target_properties(yaml-cpp PROPERTIES FOLDER "External/Libs")
endif()
if(TARGET lean_build)
    set_target_properties(lean_build PROPERTIES FOLDER "External/Libs")
endif()

message(STATUS "External dependencies configuration finished.")
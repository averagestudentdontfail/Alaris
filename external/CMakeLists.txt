# external/CMakeLists.txt
# External dependencies management for Alaris

message(STATUS "Building external dependencies...")

# Ensure we're building as static libraries for deterministic deployment
set(BUILD_SHARED_LIBS OFF)

# Configure QuantLib
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/quant/CMakeLists.txt")
    message(STATUS "Building QuantLib from source...")
    
    # QuantLib build configuration
    set(QL_BUILD_BENCHMARK OFF CACHE BOOL "Build QuantLib benchmark" FORCE)
    set(QL_BUILD_EXAMPLES OFF CACHE BOOL "Build QuantLib examples" FORCE)
    set(QL_BUILD_TEST_SUITE OFF CACHE BOOL "Build QuantLib test suite" FORCE)
    set(QL_ENABLE_SESSIONS OFF CACHE BOOL "Enable QuantLib sessions" FORCE)
    set(QL_ENABLE_THREAD_SAFE_OBSERVER_PATTERN OFF CACHE BOOL "Thread-safe observer" FORCE)
    set(QL_ENABLE_PARALLEL_UNIT_TEST_RUNNER OFF CACHE BOOL "Parallel unit tests" FORCE)
    set(QL_HIGH_RESOLUTION_DATE ON CACHE BOOL "High resolution dates" FORCE)
    set(QL_USE_STD_SHARED_PTR ON CACHE BOOL "Use std::shared_ptr" FORCE)
    
    # Performance and deterministic execution optimizations
    set(QL_ERROR_FUNCTIONS OFF CACHE BOOL "Disable error functions for performance" FORCE)
    set(QL_ERROR_LINES OFF CACHE BOOL "Disable error line info for performance" FORCE)
    set(QL_ENABLE_TRACING OFF CACHE BOOL "Disable tracing for performance" FORCE)
    
    # Memory management optimizations
    set(QL_USE_DISPOSABLE OFF CACHE BOOL "Disable disposable pattern for deterministic memory" FORCE)
    
    # Avoid building unnecessary targets
    set(QL_INSTALL OFF CACHE BOOL "Don't install QuantLib separately" FORCE)
    
    # Configure Boost usage within QuantLib
    if(Boost_FOUND)
        set(QL_EXTERNAL_BOOST ON CACHE BOOL "Use external Boost" FORCE)
    else()
        set(QL_EXTERNAL_BOOST OFF CACHE BOOL "Use QuantLib's internal Boost" FORCE)
    endif()
    
    # Add QuantLib with specific target name
    add_subdirectory(quant)
    
    # Ensure we have the right target name and export it
    if(TARGET QuantLib)
        set(QUANTLIB_TARGET QuantLib PARENT_SCOPE)
        message(STATUS "QuantLib target: QuantLib")
        
        # Add deterministic compilation flags to QuantLib
        target_compile_definitions(QuantLib PUBLIC
            QL_HIGH_RESOLUTION_DATE
            QL_USE_STD_SHARED_PTR
            $<$<CONFIG:Release>:QL_ERROR_FUNCTIONS=0>
            $<$<CONFIG:Release>:QL_ERROR_LINES=0>
            $<$<CONFIG:Release>:QL_ENABLE_TRACING=0>
        )
        
        # Optimize QuantLib for trading performance
        if(CMAKE_BUILD_TYPE STREQUAL "Release")
            target_compile_options(QuantLib PRIVATE
                -ffast-math
                -fno-finite-math-only  # Keep NaN/Inf handling for financial calculations
            )
        endif()
        
    elseif(TARGET quantlib)
        set(QUANTLIB_TARGET quantlib PARENT_SCOPE)
        message(STATUS "QuantLib target: quantlib")
    else()
        message(FATAL_ERROR "QuantLib target not found after build")
    endif()
    
    # Set include directories for parent scope
    set(QuantLib_INCLUDE_DIRS 
        "${CMAKE_CURRENT_SOURCE_DIR}/quant"
        "${CMAKE_CURRENT_BINARY_DIR}/quant"
        PARENT_SCOPE
    )
    
else()
    message(FATAL_ERROR "QuantLib submodule not found at ${CMAKE_CURRENT_SOURCE_DIR}/quant")
endif()

# Configure yaml-cpp
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/yaml-cpp/CMakeLists.txt")
    message(STATUS "Building yaml-cpp from source...")
    
    # yaml-cpp build configuration
    set(YAML_CPP_BUILD_TESTS OFF CACHE BOOL "Build yaml-cpp tests" FORCE)
    set(YAML_CPP_BUILD_TOOLS OFF CACHE BOOL "Build yaml-cpp tools" FORCE)
    set(YAML_CPP_BUILD_CONTRIB OFF CACHE BOOL "Build yaml-cpp contrib" FORCE)
    set(YAML_CPP_INSTALL OFF CACHE BOOL "Install yaml-cpp" FORCE)
    set(YAML_BUILD_SHARED_LIBS OFF CACHE BOOL "Build yaml-cpp as static" FORCE)
    
    # Add yaml-cpp
    add_subdirectory(yaml-cpp)
    
    set(YAML_CPP_TARGET yaml-cpp PARENT_SCOPE)
    set(yaml-cpp_INCLUDE_DIRS 
        "${CMAKE_CURRENT_SOURCE_DIR}/yaml-cpp/include"
        PARENT_SCOPE
    )
    
    message(STATUS "yaml-cpp configured successfully")
    
else()
    message(FATAL_ERROR "yaml-cpp submodule not found at ${CMAKE_CURRENT_SOURCE_DIR}/yaml-cpp")
endif()

# Handle Lean (documentation only - it's a .NET project)
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/lean")
    message(STATUS "Lean submodule found (managed separately via .NET)")
    set(LEAN_AVAILABLE TRUE PARENT_SCOPE)
    
    # Check for .NET SDK
    find_program(DOTNET_EXECUTABLE dotnet)
    if(DOTNET_EXECUTABLE)
        execute_process(
            COMMAND ${DOTNET_EXECUTABLE} --version
            OUTPUT_VARIABLE DOTNET_VERSION
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET
        )
        if(DOTNET_VERSION)
            message(STATUS "Found .NET SDK: ${DOTNET_VERSION}")
            set(DOTNET_VERSION ${DOTNET_VERSION} PARENT_SCOPE)
        endif()
    else()
        message(STATUS "Lean submodule found but .NET SDK not detected")
        message(STATUS "Install .NET 8 SDK to build C# components")
    endif()
else()
    message(STATUS "Lean submodule not found (optional for C++ components)")
    set(LEAN_AVAILABLE FALSE PARENT_SCOPE)
endif()

# Optional: Add any additional external libraries here
# For example, if we need testing frameworks, additional math libraries, etc.

# Google Test (if we want to add it later)
option(ALARIS_BUILD_WITH_GTEST "Build with Google Test" OFF)
if(ALARIS_BUILD_WITH_GTEST)
    message(STATUS "Configuring Google Test...")
    
    include(FetchContent)
    FetchContent_Declare(
        googletest
        GIT_REPOSITORY https://github.com/google/googletest.git
        GIT_TAG release-1.12.1
    )
    
    # For Windows: Prevent overriding the parent project's compiler/linker settings
    set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)
    
    FetchContent_MakeAvailable(googletest)
    
    set(GTEST_AVAILABLE TRUE PARENT_SCOPE)
    message(STATUS "Google Test configured")
else()
    set(GTEST_AVAILABLE FALSE PARENT_SCOPE)
endif()

# Benchmark library (optional for performance testing)
option(ALARIS_BUILD_WITH_BENCHMARK "Build with Google Benchmark" OFF)
if(ALARIS_BUILD_WITH_BENCHMARK)
    message(STATUS "Configuring Google Benchmark...")
    
    include(FetchContent)
    FetchContent_Declare(
        googlebenchmark
        GIT_REPOSITORY https://github.com/google/benchmark.git
        GIT_TAG v1.8.3
    )
    
    set(BENCHMARK_ENABLE_TESTING OFF CACHE BOOL "Disable benchmark testing" FORCE)
    set(BENCHMARK_ENABLE_GTEST_TESTS OFF CACHE BOOL "Disable gtest tests" FORCE)
    
    FetchContent_MakeAvailable(googlebenchmark)
    
    set(BENCHMARK_AVAILABLE TRUE PARENT_SCOPE)
    message(STATUS "Google Benchmark configured")
else()
    set(BENCHMARK_AVAILABLE FALSE PARENT_SCOPE)
endif()

# Create a consolidated external libraries target for convenience
add_library(External::All INTERFACE)

if(TARGET ${QUANTLIB_TARGET})
    target_link_libraries(External::All INTERFACE ${QUANTLIB_TARGET})
endif()

if(TARGET ${YAML_CPP_TARGET})
    target_link_libraries(External::All INTERFACE ${YAML_CPP_TARGET})
endif()

# Set up folder organization for IDE
if(TARGET ${QUANTLIB_TARGET})
    set_target_properties(${QUANTLIB_TARGET} PROPERTIES FOLDER "External")
endif()

if(TARGET ${YAML_CPP_TARGET})
    set_target_properties(${YAML_CPP_TARGET} PROPERTIES FOLDER "External")
endif()

# Export targets to parent scope
set(EXTERNAL_TARGETS
    External::All
    ${QUANTLIB_TARGET}
    ${YAML_CPP_TARGET}
    PARENT_SCOPE
)

# Print external dependencies summary
message(STATUS "External dependencies configured:")
if(TARGET ${QUANTLIB_TARGET})
    message(STATUS "  ✓ QuantLib: ${QUANTLIB_TARGET}")
endif()
if(TARGET ${YAML_CPP_TARGET})
    message(STATUS "  ✓ yaml-cpp: ${YAML_CPP_TARGET}")
endif()
if(LEAN_AVAILABLE)
    message(STATUS "  ✓ Lean: Available (.NET)")
    if(DOTNET_VERSION)
        message(STATUS "    .NET SDK: ${DOTNET_VERSION}")
    endif()
endif()
if(GTEST_AVAILABLE)
    message(STATUS "  ✓ Google Test: Available")
endif()
if(BENCHMARK_AVAILABLE)
    message(STATUS "  ✓ Google Benchmark: Available")
endif()
cmake_minimum_required(VERSION 3.20)

if(POLICY CMP0135)
    cmake_policy(SET CMP0135 NEW)
endif()

# Add external dependencies first to ensure they are available
# This will process external/CMakeLists.txt, building yaml-cpp and QuantLib
# and setting variables like YAML_CPP_TARGET and QuantLib_TARGET.
add_subdirectory(external) # Added this line

# Detect .NET SDK
find_program(DOTNET_EXECUTABLE dotnet)
if(DOTNET_EXECUTABLE)
    execute_process(
        COMMAND ${DOTNET_EXECUTABLE} --version
        OUTPUT_VARIABLE DOTNET_VERSION
        OUTPUT_STRIP_TRAILING_WHITESPACE
    )
    message(STATUS "Found .NET SDK version: ${DOTNET_VERSION}")
    set(CMAKE_DOTNET_VERSION ${DOTNET_VERSION} CACHE STRING ".NET SDK version")
else()
    message(WARNING ".NET SDK not found. C# components will be skipped.")
endif()

project(Alaris
    VERSION 1.0.0
    LANGUAGES CXX
    DESCRIPTION "High-Performance Derivatives Trading System"
)

# Add custom CMake module directories
list(APPEND CMAKE_MODULE_PATH
    "${CMAKE_SOURCE_DIR}/.cmake"
)

# Include build system configuration
# These files can now potentially use targets/variables defined by add_subdirectory(external)
include(${CMAKE_SOURCE_DIR}/.cmake/Config.cmake)
include(${CMAKE_SOURCE_DIR}/.cmake/BuildSystem.cmake)
include(${CMAKE_SOURCE_DIR}/.cmake/Deployment.cmake)
include(${CMAKE_SOURCE_DIR}/.cmake/GitInfo.cmake)

# Add source code subdirectories
add_subdirectory(src)

# Add C# components if .NET SDK is found
if(DOTNET_EXECUTABLE)
    # Note: external/CMakeLists.txt also has logic for Lean/dotnet.
    # Ensure this doesn't conflict or is coordinated.
    # The add_dotnet_project calls here might be redundant if external/CMakeLists.txt handles it,
    # or they might be for different C# projects.
    # Based on external/CMakeLists.txt, it adds a lean_build custom target.
    # The add_dotnet_project is a custom function, ensure it's defined if used here.
    # If Alaris.Lean.csproj is part of the main solution built by lean_build,
    # then explicitly adding it here again might be unnecessary.
    # For now, keeping as original:
    add_dotnet_project(lean_build "${CMAKE_SOURCE_DIR}/external/lean/QuantConnect.Lean.sln")
    add_dotnet_project(Alaris.Lean "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.Lean.csproj")
    message(STATUS "C# components configured (root CMakeLists).")
else()
    message(STATUS "C# components skipped - .NET SDK not found (root CMakeLists).")
endif()

# Add test subdirectory
if(EXISTS "${CMAKE_SOURCE_DIR}/test/CMakeLists.txt")
    enable_testing()
    add_subdirectory(test)
    message(STATUS "Test suite configured.")
else()
    message(STATUS "Test suite not found or not configured.")
endif()

# Configure compiler flags for external dependencies
function(set_external_compiler_flags target)
    if(NOT TARGET ${target})
        return()
    endif()
    
    get_target_property(target_type ${target} TYPE)
    if(NOT target_type STREQUAL "INTERFACE_LIBRARY")
        if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
            target_compile_options(${target} PRIVATE -w)
        endif()
    endif()
endfunction()

# Apply to all external dependencies
# These targets (ql_library, yaml-cpp) are defined in external/CMakeLists.txt
if(TARGET ql_library) # Or use ${QUANTLIB_TARGET} if that's the variable set by external/CMakeLists.txt
    set_external_compiler_flags(ql_library)
endif()

if(TARGET yaml-cpp) # Or use ${YAML_CPP_TARGET}
    set_external_compiler_flags(yaml-cpp)
endif()

message(STATUS "Alaris project configuration finished.")
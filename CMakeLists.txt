cmake_minimum_required(VERSION 3.20)

if(POLICY CMP0135)
    cmake_policy(SET CMP0135 NEW)
endif()

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

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "Build type (Debug, Release, RelWithDebInfo, MinSizeRel)" FORCE)
    set_property(CACHE CMAKE_BUILD_TYPE PROPERTY STRINGS "Debug" "Release" "RelWithDebInfo" "MinSizeRel")
endif()

message(STATUS "Project: ${PROJECT_NAME}")
message(STATUS "Version: ${PROJECT_VERSION}")
message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")

# Add custom CMake module directories
list(APPEND CMAKE_MODULE_PATH
    "${CMAKE_SOURCE_DIR}/cmake/modules"
    "${CMAKE_SOURCE_DIR}/cmake/utils"
    "${CMAKE_SOURCE_DIR}/cmake"
)

# Include helper functions and build configurations
include(${CMAKE_SOURCE_DIR}/cmake/utils/ModernCMake.cmake)
include(${CMAKE_SOURCE_DIR}/cmake/utils/DependencyManager.cmake)
include(${CMAKE_SOURCE_DIR}/cmake/utils/BuildConfig.cmake)
include(${CMAKE_SOURCE_DIR}/cmake/utils/CompilerFlags.cmake)
include(${CMAKE_SOURCE_DIR}/cmake/utils/Platform.cmake)
include(${CMAKE_SOURCE_DIR}/cmake/utils/Submodules.cmake)
include(${CMAKE_SOURCE_DIR}/cmake/utils/DotNetConfig.cmake)

# Configure build components
enforce_modern_cmake()
check_submodules()
configure_platform()
configure_build_options()
configure_compiler_flags()
configure_sanitizers()
configure_coverage()
manage_dependencies()
configure_dotnet_build()

# Include Git information
include(GitInfo)

# Add source code subdirectories
add_subdirectory(src)

# Add C# components if .NET SDK is found
if(DOTNET_EXECUTABLE)
    add_dotnet_project(lean_build "${CMAKE_SOURCE_DIR}/external/lean/QuantConnect.Lean.sln")
    add_dotnet_project(Alaris.Lean "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.Lean.csproj")
    message(STATUS "C# components configured.")
else()
    message(STATUS "C# components skipped - .NET SDK not found.")
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
if(TARGET ql_library)
    set_external_compiler_flags(ql_library)
endif()

if(TARGET yaml-cpp)
    set_external_compiler_flags(yaml-cpp)
endif()

# Include installation and packaging configuration
include(${CMAKE_SOURCE_DIR}/cmake/Install.cmake)
include(${CMAKE_SOURCE_DIR}/cmake/Package.cmake)

message(STATUS "Alaris project configuration finished.")
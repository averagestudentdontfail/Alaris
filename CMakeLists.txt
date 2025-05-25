cmake_minimum_required(VERSION 3.20)

if(POLICY CMP0135)
    cmake_policy(SET CMP0135 NEW)
endif()

project(Alaris
    VERSION 1.0.0
    LANGUAGES CXX # CSharp removed to resolve previous error
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

# Add custom CMake module directories to CMAKE_MODULE_PATH
# This allows 'include(ModuleName)' if ModuleName.cmake is in one of these paths.
list(APPEND CMAKE_MODULE_PATH
    "${CMAKE_SOURCE_DIR}/cmake/modules"
    "${CMAKE_SOURCE_DIR}/cmake/utils"
    "${CMAKE_SOURCE_DIR}/cmake" # For Install.cmake, Package.cmake, Summary.cmake if included by name
)

# Include helper functions and build configurations using explicit paths for clarity
# These files define functions that will be called subsequently.
include(${CMAKE_SOURCE_DIR}/cmake/utils/ModernCMake.cmake)
include(${CMAKE_SOURCE_DIR}/cmake/utils/DependencyManager.cmake) # Assumes this is from the comprehensive "Dependencies.txt"
include(${CMAKE_SOURCE_DIR}/cmake/utils/BuildConfig.cmake)
include(${CMAKE_SOURCE_DIR}/cmake/utils/CompilerFlags.cmake)
include(${CMAKE_SOURCE_DIR}/cmake/utils/Platform.cmake)     # Added Platform configuration
include(${CMAKE_SOURCE_DIR}/cmake/utils/Submodules.cmake)   # Added Submodule checks

# --- Call functions defined in the above utils ---
enforce_modern_cmake()      # From ModernCMake.cmake
check_submodules()          # From Submodules.cmake
configure_platform()        # From Platform.cmake
configure_build_options()   # From BuildConfig.cmake
configure_compiler_flags()  # From CompilerFlags.cmake
configure_sanitizers()      # From BuildConfig.cmake
configure_coverage()        # From BuildConfig.cmake
manage_dependencies()       # From DependencyManager.cmake (should call find_system_dependencies and configure_external_dependencies)

# The GitInfo.cmake module from cmake/modules/GitInfo.cmake auto-runs get_git_info()
# upon inclusion if 'include(GitInfo)' is used.
# If you want to explicitly call its functions, you can:
include(GitInfo) # Searches CMAKE_MODULE_PATH, finds cmake/modules/GitInfo.cmake
# get_git_info() # This is already called at the end of GitInfo.cmake
# setup_git_hooks() # Call this if you want to set up git hooks

# Add source code subdirectories
add_subdirectory(src)

# Add test subdirectory
if(EXISTS "${CMAKE_SOURCE_DIR}/test/CMakeLists.txt")
    enable_testing()
    add_subdirectory(test)
    message(STATUS "Test suite configured.")
else()
    message(STATUS "Test suite not found or not configured.")
endif()

# Ensure Lean is built before the main project
if(TARGET lean_build)
    add_dependencies(alaris_core lean_build)
endif()

# Configure Lean build with security warning suppression
set(LEAN_BUILD_ARGS 
    "build" 
    "${CMAKE_CURRENT_SOURCE_DIR}/external/lean/QuantConnect.Lean.sln" 
    "--configuration" "Release"
    "--verbosity" "minimal"
    "-p:WarningsNotAsErrors=NU1903"
    "-p:TreatWarningsAsErrors=false"
)

# Only create lean_build target if it doesn't already exist
if(NOT TARGET lean_build)
    add_custom_target(lean_build
        COMMAND ${CMAKE_COMMAND} -E echo "Building Lean with security warning suppressed..."
        COMMAND dotnet ${LEAN_BUILD_ARGS}
        WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
        COMMENT "Building QuantConnect Lean"
    )
endif()

# Installation configuration: Include the file to execute its commands
include(${CMAKE_SOURCE_DIR}/cmake/Install.cmake)
# Packaging configuration: Include the file to execute its CPack settings
include(${CMAKE_SOURCE_DIR}/cmake/Package.cmake)

# Store original compiler flags
set(CMAKE_CXX_FLAGS_ORIG "${CMAKE_CXX_FLAGS}")

# Add warning flags for our code
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    add_compile_options(-Wall -Wextra -Wpedantic -Werror)
endif()

# Function to set compiler flags for external dependencies
function(set_external_compiler_flags target)
    if(NOT TARGET ${target})
        return()
    endif()
    
    # Check if target is a library or executable
    get_target_property(target_type ${target} TYPE)
    if(NOT target_type STREQUAL "INTERFACE_LIBRARY")
        if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
            target_compile_options(${target} PRIVATE
                -w  # Disable all warnings
            )
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

# Note: Lean is handled differently as it's a .NET project
# We don't need to set C++ compiler flags for it

message(STATUS "Alaris project configuration finished.")
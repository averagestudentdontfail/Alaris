cmake_minimum_required(VERSION 3.20)

# Set policies
if(POLICY CMP0135)
    cmake_policy(SET CMP0135 NEW)
endif()

project(Alaris 
    VERSION 1.0.0 
    LANGUAGES CXX
    DESCRIPTION "High-Performance Derivatives Trading System"
)

# Set C++ standard
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Build type configuration
if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "Build type" FORCE)
    set_property(CACHE CMAKE_BUILD_TYPE PROPERTY STRINGS "Debug" "Release" "RelWithDebInfo" "MinSizeRel")
endif()

message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")

# Add custom CMake modules
list(APPEND CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/cmake/modules")

# Include helper functions
include(cmake/utils/CompilerFlags.cmake)
include(cmake/utils/Dependencies.cmake)
include(cmake/utils/Submodules.cmake)
include(cmake/utils/Platform.cmake)

# Set global properties
set_property(GLOBAL PROPERTY USE_FOLDERS ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# Platform-specific configuration
configure_platform()

# Configure compiler flags
configure_compiler_flags()

# Check and initialize submodules
check_submodules()

# Find system dependencies
find_system_dependencies()

# Configure external dependencies
configure_external_dependencies()

# Global include directories
include_directories(
    ${CMAKE_SOURCE_DIR}/src
    ${CMAKE_SOURCE_DIR}/src/quantlib
)

# Add subdirectories
add_subdirectory(external)
add_subdirectory(src/quantlib)

# Add tests if this is the main project
if(CMAKE_PROJECT_NAME STREQUAL PROJECT_NAME)
    enable_testing()
    add_subdirectory(test)
endif()

# Installation configuration
include(cmake/Install.cmake)

# Packaging configuration
include(cmake/Package.cmake)

# Print configuration summary
include(cmake/Summary.cmake)
print_configuration_summary()
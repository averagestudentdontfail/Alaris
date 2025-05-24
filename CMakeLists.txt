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
include(cmake/utils/ModernCMake.cmake)
include(cmake/utils/DependencyManager.cmake)
include(cmake/utils/BuildConfig.cmake)

# Enforce modern CMake practices
enforce_modern_cmake()

# Configure build options
configure_build_options()

# Configure sanitizers
configure_sanitizers()

# Configure code coverage
configure_coverage()

# Manage dependencies
manage_dependencies()

# Create dependency graph
create_dependency_graph()

# Add subdirectories
add_subdirectory(src)

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
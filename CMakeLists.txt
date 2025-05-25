cmake_minimum_required(VERSION 3.20)

# Set policies - CMP0135 allows for more intuitive handling of cmake_path
if(POLICY CMP0135)
    cmake_policy(SET CMP0135 NEW)
endif()

project(Alaris
    VERSION 1.0.0
    LANGUAGES CXX CSharp # Added CSharp as a language if .NET components are built
    DESCRIPTION "High-Performance Derivatives Trading System"
)

# Set C++ standard
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Build type configuration
if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "Build type (Debug, Release, RelWithDebInfo, MinSizeRel)" FORCE)
    set_property(CACHE CMAKE_BUILD_TYPE PROPERTY STRINGS "Debug" "Release" "RelWithDebInfo" "MinSizeRel")
endif()

message(STATUS "Project: ${PROJECT_NAME}")
message(STATUS "Version: ${PROJECT_VERSION}")
message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")

# Add custom CMake modules directory to search path
# This assumes your unified build system logic resides in 'cmake/modules' and 'cmake/utils'
list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake/modules")

# Include helper functions and build configurations
# These files should contain the core logic of your "unified build system"
include(cmake/utils/ModernCMake)
include(cmake/utils/DependencyManager)
include(cmake/utils/BuildConfig) # Renamed from BuildConfig.cmake for consistency if it's a .cmake file
include(cmake/utils/CompilerFlags) # Assuming you might have specific compiler flag settings here

# Enforce modern CMake practices (defined in ModernCMake.cmake)
enforce_modern_cmake()

# Configure global build options (defined in BuildConfig.cmake or similar)
configure_build_options()

# Configure project-specific compiler flags (defined in CompilerFlags.cmake or similar)
configure_compiler_flags()

# Configure sanitizers, if applicable (defined in BuildConfig.cmake or similar)
configure_sanitizers()

# Configure code coverage, if applicable (defined in BuildConfig.cmake or similar)
configure_coverage()

# Manage external dependencies (defined in DependencyManager.cmake)
# This will handle QuantLib, YAML-CPP, and check for .NET SDK for Lean
manage_dependencies()

# Create a dependency graph if your utility supports it
# create_dependency_graph() # Uncomment if available and desired

# Add source code subdirectories
# The CMakeLists.txt in these directories will define their specific targets
add_subdirectory(src)

# Add test subdirectory
# Tests are configured if the 'test' directory and its CMakeLists.txt exist
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/test/CMakeLists.txt")
    enable_testing()
    add_subdirectory(test)
    message(STATUS "Test suite configured.")
else()
    message(STATUS "Test suite not found or not configured.")
endif()

# Installation configuration (logic in Install.cmake)
include(cmake/Install)
configure_installation()

# Packaging configuration (logic in Package.cmake)
include(cmake/Package)
configure_packaging()

# Print configuration summary (logic in Summary.cmake)
include(cmake/Summary)
print_configuration_summary()

message(STATUS "Alaris project configuration finished.")
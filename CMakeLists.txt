cmake_minimum_required(VERSION 3.20)
project(Alaris VERSION 1.0.0 LANGUAGES CXX)

# Set C++ standard
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Build type specific flags
if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release)
endif()

# Compiler optimizations for production
set(CMAKE_CXX_FLAGS_RELEASE "-O3 -DNDEBUG -march=native -mtune=native -ffast-math")
set(CMAKE_CXX_FLAGS_DEBUG "-O0 -g -DDEBUG -fno-omit-frame-pointer")
set(CMAKE_CXX_FLAGS_RELWITHDEBINFO "-O2 -g -DNDEBUG -fno-omit-frame-pointer")

# Enable position independent code
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Find required packages
find_package(PkgConfig REQUIRED)
find_package(Threads REQUIRED)

# System libraries for shared memory and real-time
find_library(RT_LIB rt)
find_library(PTHREAD_LIB pthread REQUIRED)
find_library(MATH_LIB m)

# Set up library variables
set(SYSTEM_LIBS)
if(RT_LIB)
    list(APPEND SYSTEM_LIBS ${RT_LIB})
endif()
if(MATH_LIB)
    list(APPEND SYSTEM_LIBS ${MATH_LIB})
endif()
list(APPEND SYSTEM_LIBS ${PTHREAD_LIB})

# Check if submodules are initialized
if(NOT EXISTS "${CMAKE_SOURCE_DIR}/external/quant/CMakeLists.txt" OR
   NOT EXISTS "${CMAKE_SOURCE_DIR}/external/lean/CMakeLists.txt" OR
   NOT EXISTS "${CMAKE_SOURCE_DIR}/external/yaml-cpp/CMakeLists.txt")
    message(FATAL_ERROR 
        "Submodules not initialized. Please run:\n"
        "  git submodule update --init --recursive")
endif()

# Build external dependencies in order
message(STATUS "Building external dependencies...")

# 1. Build yaml-cpp first (it's a dependency for others)
message(STATUS "Building yaml-cpp...")
set(YAML_CPP_BUILD_TESTS OFF CACHE BOOL "Build yaml-cpp tests")
set(YAML_CPP_BUILD_TOOLS OFF CACHE BOOL "Build yaml-cpp tools")
set(YAML_CPP_BUILD_CONTRIB OFF CACHE BOOL "Build yaml-cpp contrib")
set(YAML_CPP_INSTALL OFF CACHE BOOL "Install yaml-cpp")
add_subdirectory(external/yaml-cpp)
set(YAML_CPP_TARGET yaml-cpp)
set(YAML_CPP_TARGET_EXISTS TRUE)
set(yaml-cpp_INCLUDE_DIRS "${CMAKE_SOURCE_DIR}/external/yaml-cpp/include")

# 2. Build QuantLib
message(STATUS "Building QuantLib...")
set(QL_BUILD_BENCHMARK OFF CACHE BOOL "Build QuantLib benchmark")
set(QL_BUILD_EXAMPLES OFF CACHE BOOL "Build QuantLib examples")
set(QL_BUILD_TEST_SUITE OFF CACHE BOOL "Build QuantLib test suite")
set(QL_ENABLE_SESSIONS OFF CACHE BOOL "Enable QuantLib sessions")
set(QL_ENABLE_THREAD_SAFE_OBSERVER_PATTERN OFF CACHE BOOL "Thread-safe observer")
set(QL_ENABLE_PARALLEL_UNIT_TEST_RUNNER OFF CACHE BOOL "Parallel unit tests")
set(QL_HIGH_RESOLUTION_DATE ON CACHE BOOL "High resolution dates")
set(QL_USE_STD_SHARED_PTR ON CACHE BOOL "Use std::shared_ptr")
set(BUILD_SHARED_LIBS OFF CACHE BOOL "Build shared libraries")
add_subdirectory(external/quant)

# Identify QuantLib target
set(QUANTLIB_TARGET_EXISTS FALSE)
foreach(TARGET_NAME "QuantLib" "quantlib" "ql")
    if(TARGET ${TARGET_NAME})
        set(QUANTLIB_TARGET ${TARGET_NAME})
        set(QUANTLIB_TARGET_EXISTS TRUE)
        message(STATUS "Found QuantLib target: ${TARGET_NAME}")
        break()
    endif()
endforeach()

if(NOT QUANTLIB_TARGET_EXISTS)
    message(FATAL_ERROR "No valid QuantLib target found after building")
endif()

set(QuantLib_INCLUDE_DIRS 
    "${CMAKE_SOURCE_DIR}/external/quant"
    "${CMAKE_BINARY_DIR}/external/quant"
)

# 3. Build Lean
message(STATUS "Building Lean...")
# Add Lean-specific build options here
add_subdirectory(external/lean)

# Identify Lean target
set(LEAN_TARGET_EXISTS FALSE)
if(TARGET Lean)
    set(LEAN_TARGET Lean)
    set(LEAN_TARGET_EXISTS TRUE)
    message(STATUS "Found Lean target: Lean")
endif()

if(NOT LEAN_TARGET_EXISTS)
    message(FATAL_ERROR "No valid Lean target found after building")
endif()

set(Lean_INCLUDE_DIRS 
    "${CMAKE_SOURCE_DIR}/external/lean"
    "${CMAKE_BINARY_DIR}/external/lean"
)

# Boost (required by QuantLib)
find_package(Boost 1.75.0 REQUIRED)
if(Boost_FOUND)
    message(STATUS "Found Boost: ${Boost_VERSION}")
endif()

# Global include directories
include_directories(
    ${CMAKE_SOURCE_DIR}/src
    ${QuantLib_INCLUDE_DIRS}
    ${yaml-cpp_INCLUDE_DIRS}
    ${Lean_INCLUDE_DIRS}
)

# Global compiler definitions for deterministic execution
add_compile_definitions(
    QL_ENABLE_PARALLEL_UNIT_TEST_RUNNER=0
    QL_ENABLE_THREAD_SAFE_OBSERVER_PATTERN=0
    QL_HIGH_RESOLUTION_DATE
    QL_USE_STD_SHARED_PTR
)

# Platform-specific definitions
if(UNIX AND NOT APPLE)
    add_compile_definitions(_GNU_SOURCE)
endif()

# Add subdirectories - order matters for dependencies
add_subdirectory(src/quantlib)

# Only add tests if this is the main project
if(CMAKE_PROJECT_NAME STREQUAL PROJECT_NAME)
    enable_testing()
    add_subdirectory(test)
endif()

# Print configuration summary
message(STATUS "")
message(STATUS "=== Alaris Configuration Summary ===")
message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")
message(STATUS "C++ standard: ${CMAKE_CXX_STANDARD}")
message(STATUS "QuantLib target: ${QUANTLIB_TARGET}")
message(STATUS "yaml-cpp target: ${YAML_CPP_TARGET}")
message(STATUS "Lean target: ${LEAN_TARGET}")
message(STATUS "System libraries: ${SYSTEM_LIBS}")
message(STATUS "=====================================")
message(STATUS "")

# Installation targets
install(TARGETS quantlib_process 
        RUNTIME DESTINATION bin
        PERMISSIONS OWNER_READ OWNER_WRITE OWNER_EXECUTE GROUP_READ GROUP_EXECUTE WORLD_READ WORLD_EXECUTE)

# Install configuration files
install(DIRECTORY config/
        DESTINATION etc/alaris
        FILES_MATCHING PATTERN "*.yaml")

# Install scripts
install(DIRECTORY scripts/
        DESTINATION bin
        FILES_MATCHING PATTERN "*.sh"
        PERMISSIONS OWNER_READ OWNER_WRITE OWNER_EXECUTE GROUP_READ GROUP_EXECUTE WORLD_READ WORLD_EXECUTE)
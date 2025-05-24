cmake_minimum_required(VERSION 3.20)
project(Alaris VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Compiler optimizations for production
set(CMAKE_CXX_FLAGS_RELEASE "-O3 -DNDEBUG -march=native -mtune=native")
set(CMAKE_CXX_FLAGS_DEBUG "-O0 -g -DDEBUG")

# Find required packages
find_package(PkgConfig REQUIRED)
find_package(Threads REQUIRED)

# QuantLib - try system first, fallback to external
find_package(QuantLib QUIET)
if(NOT QuantLib_FOUND)
    message(STATUS "Using external QuantLib")
    set(QuantLib_INCLUDE_DIRS "${CMAKE_SOURCE_DIR}/external/quant")
    # We'll build QuantLib as part of our build
    add_subdirectory(external/quant EXCLUDE_FROM_ALL)
    set(QuantLib_LIBRARIES QuantLib)
endif()

# System libraries for shared memory and real-time
find_library(RT_LIB rt REQUIRED)
find_library(PTHREAD_LIB pthread REQUIRED)

# Include directories
include_directories(
    ${CMAKE_SOURCE_DIR}/src
    ${QuantLib_INCLUDE_DIRS}
)

# Compiler definitions for deterministic execution
add_compile_definitions(
    QL_ENABLE_PARALLEL_UNIT_TEST_RUNNER=0
    QL_ENABLE_THREAD_SAFE_OBSERVER_PATTERN=0
    QL_HIGH_RESOLUTION_DATE
)

# Add subdirectories
add_subdirectory(src/quantlib)
add_subdirectory(test)

# Installation
install(TARGETS quantlib_process 
        DESTINATION bin
        PERMISSIONS OWNER_READ OWNER_WRITE OWNER_EXECUTE)

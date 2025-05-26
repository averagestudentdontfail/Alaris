# External dependency management

# First, try to find dependencies as system packages
option(USE_SYSTEM_QUANTLIB "Use system-installed QuantLib" ON)
option(USE_SYSTEM_YAMLCPP "Use system-installed yaml-cpp" ON)

# Configure QuantLib
if(USE_SYSTEM_QUANTLIB)
    find_package(QuantLib QUIET)
    if(QuantLib_FOUND)
        set(QUANTLIB_TARGET QuantLib::QuantLib)
        message(STATUS "Using system QuantLib: ${QuantLib_VERSION}")
    else()
        message(STATUS "System QuantLib not found, will attempt to build from source")
        set(USE_SYSTEM_QUANTLIB OFF)
    endif()
endif()

if(NOT USE_SYSTEM_QUANTLIB)
    # Check if external/QuantLib exists
    if(EXISTS "${CMAKE_SOURCE_DIR}/external/QuantLib/CMakeLists.txt")
        message(STATUS "Building QuantLib from external/QuantLib")
        add_subdirectory(${CMAKE_SOURCE_DIR}/external/QuantLib)
        set(QUANTLIB_TARGET QuantLib)
    elseif(EXISTS "${CMAKE_SOURCE_DIR}/external/quant/CMakeLists.txt")
        message(STATUS "Building QuantLib from external/quant")
        add_subdirectory(${CMAKE_SOURCE_DIR}/external/quant)
        set(QUANTLIB_TARGET QuantLib)
    else()
        # Fallback: try to find system QuantLib anyway
        find_package(QuantLib REQUIRED)
        set(QUANTLIB_TARGET QuantLib::QuantLib)
        message(STATUS "Using system QuantLib as fallback: ${QuantLib_VERSION}")
    endif()
endif()

# Configure yaml-cpp
if(USE_SYSTEM_YAMLCPP)
    find_package(yaml-cpp QUIET)
    if(yaml-cpp_FOUND)
        set(YAML_CPP_TARGET yaml-cpp::yaml-cpp)
        get_target_property(yaml-cpp_INCLUDE_DIRS yaml-cpp::yaml-cpp INTERFACE_INCLUDE_DIRECTORIES)
        message(STATUS "Using system yaml-cpp: ${yaml-cpp_VERSION}")
    else()
        message(STATUS "System yaml-cpp not found, will attempt to build from source")
        set(USE_SYSTEM_YAMLCPP OFF)
    endif()
endif()

if(NOT USE_SYSTEM_YAMLCPP)
    # Check if external/yaml-cpp exists
    if(EXISTS "${CMAKE_SOURCE_DIR}/external/yaml-cpp/CMakeLists.txt")
        message(STATUS "Building yaml-cpp from external/yaml-cpp")
        
        # Configure yaml-cpp options
        set(YAML_CPP_BUILD_TESTS OFF CACHE BOOL "Disable yaml-cpp tests")
        set(YAML_CPP_BUILD_TOOLS OFF CACHE BOOL "Disable yaml-cpp tools")
        set(YAML_CPP_BUILD_CONTRIB OFF CACHE BOOL "Disable yaml-cpp contrib")
        
        add_subdirectory(${CMAKE_SOURCE_DIR}/external/yaml-cpp)
        set(YAML_CPP_TARGET yaml-cpp)
        set(yaml-cpp_INCLUDE_DIRS ${CMAKE_SOURCE_DIR}/external/yaml-cpp/include)
    else()
        # Fallback: try to find system yaml-cpp anyway
        find_package(yaml-cpp REQUIRED)
        set(YAML_CPP_TARGET yaml-cpp::yaml-cpp)
        get_target_property(yaml-cpp_INCLUDE_DIRS yaml-cpp::yaml-cpp INTERFACE_INCLUDE_DIRECTORIES)
        message(STATUS "Using system yaml-cpp as fallback: ${yaml-cpp_VERSION}")
    endif()
endif()

# Find GTest
find_package(GTest QUIET)
if(NOT GTest_FOUND)
    # Try to find it with different names
    find_package(googletest QUIET)
    if(googletest_FOUND)
        set(GTest_FOUND TRUE)
    else()
        message(WARNING "GTest not found. Tests will be disabled.")
        set(BUILD_TESTS OFF CACHE BOOL "Disable tests since GTest not found" FORCE)
    endif()
endif()

if(GTest_FOUND)
    message(STATUS "GTest found: ${GTEST_VERSION}")
endif()

# Detect .NET SDK for C# components  
find_program(DOTNET_EXECUTABLE dotnet QUIET)
if(DOTNET_EXECUTABLE)
    execute_process(
        COMMAND ${DOTNET_EXECUTABLE} --version
        OUTPUT_VARIABLE DOTNET_VERSION
        OUTPUT_STRIP_TRAILING_WHITESPACE
        ERROR_QUIET
    )
    set(CMAKE_DOTNET_VERSION ${DOTNET_VERSION} CACHE STRING ".NET SDK version")
    message(STATUS ".NET SDK found: ${DOTNET_VERSION}")
else()
    message(WARNING ".NET SDK not found - C# components will not be built")
endif()

# Validate that required targets are set
if(NOT DEFINED QUANTLIB_TARGET OR NOT TARGET ${QUANTLIB_TARGET})
    message(FATAL_ERROR "QuantLib target not properly configured. QUANTLIB_TARGET='${QUANTLIB_TARGET}'")
endif()

if(NOT DEFINED YAML_CPP_TARGET OR NOT TARGET ${YAML_CPP_TARGET})
    message(FATAL_ERROR "yaml-cpp target not properly configured. YAML_CPP_TARGET='${YAML_CPP_TARGET}'")
endif()

# Print dependency information
message(STATUS "External Dependencies Summary:")
message(STATUS "  QuantLib Target: ${QUANTLIB_TARGET}")
message(STATUS "  yaml-cpp Target: ${YAML_CPP_TARGET}")
if(GTest_FOUND)
    message(STATUS "  GTest: Available")
else()
    message(STATUS "  GTest: Not found")
endif()
if(DOTNET_EXECUTABLE)
    message(STATUS "  .NET SDK: ${CMAKE_DOTNET_VERSION}")
else()
    message(STATUS "  .NET SDK: Not found")
endif()
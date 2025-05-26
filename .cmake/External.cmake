# External dependency management

# Define required external dependencies
set(EXTERNAL_DEPS
    QuantLib
    yaml-cpp
    gtest
)

# Configure QuantLib from submodule
add_subdirectory(${CMAKE_SOURCE_DIR}/external/quant ${CMAKE_BINARY_DIR}/quantlib)
set(QUANTLIB_TARGET "QuantLib" CACHE STRING "QuantLib target name")
set(QuantLib_INCLUDE_DIRS ${CMAKE_SOURCE_DIR}/external/quant CACHE PATH "QuantLib include directories")

# Configure yaml-cpp from submodule
add_subdirectory(${CMAKE_SOURCE_DIR}/external/yaml-cpp ${CMAKE_BINARY_DIR}/yaml-cpp)
set(YAML_CPP_TARGET "yaml-cpp" CACHE STRING "yaml-cpp target name")
set(yaml-cpp_INCLUDE_DIRS ${CMAKE_SOURCE_DIR}/external/yaml-cpp/include CACHE PATH "yaml-cpp include directories")

# Find GTest
find_package(GTest REQUIRED)
if(NOT GTest_FOUND)
    message(FATAL_ERROR "GTest not found. Please install GTest development package.")
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

# Print dependency information
message(STATUS "External Dependencies:")
message(STATUS "  QuantLib: ${QuantLib_VERSION}")
message(STATUS "  yaml-cpp: ${yaml-cpp_VERSION}")
message(STATUS "  GTest: ${GTEST_VERSION}")
message(STATUS "  .NET SDK: ${CMAKE_DOTNET_VERSION}") 
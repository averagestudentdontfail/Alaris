cmake_minimum_required(VERSION 3.20)

if(POLICY CMP0135)
    cmake_policy(SET CMP0135 NEW)
endif()

project(Alaris
    VERSION 1.0.0
    LANGUAGES CXX
    DESCRIPTION "High-Performance Derivatives Trading System"
)

list(APPEND CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/.cmake")

# Include build system and git info first (these don't depend on external deps)
include(BuildSystem)   # Handles global build settings
include(GitInfo)       # Extracts Git information

# Set up external dependencies BEFORE Config.cmake
include(External)      # Handles external dependencies

# Now include Config.cmake (which depends on variables set by External.cmake)
include(Config)        # Handles dependency configuration

# Include deployment after everything else is configured
include(Deployment)    # Handles installation rules

# Detect .NET SDK
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

include(GNUInstallDirs)
include(CTest)
find_package(Threads REQUIRED)

if(BUILD_TESTS)
    enable_testing()
    message(STATUS "Test suite configured")
else()
    message(STATUS "Test suite disabled (BUILD_TESTS is OFF)")
endif()

# Add subdirectories for source code
add_subdirectory(src/quantlib)

# Add test directory if tests are enabled
if(BUILD_TESTS)
    add_subdirectory(test)
endif()

# Print configuration summary
message(STATUS "")
message(STATUS "=== Configuration Summary ===")
message(STATUS "Project: ${PROJECT_NAME} v${PROJECT_VERSION}")
message(STATUS "Build Type: ${CMAKE_BUILD_TYPE}")
message(STATUS "Install Prefix: ${CMAKE_INSTALL_PREFIX}")
message(STATUS "C++ Standard: ${CMAKE_CXX_STANDARD}")
message(STATUS "Compiler: ${CMAKE_CXX_COMPILER_ID} ${CMAKE_CXX_COMPILER_VERSION}")
message(STATUS "")
message(STATUS "Options:")
message(STATUS "  BUILD_TESTS: ${BUILD_TESTS}")
message(STATUS "  BUILD_DOCS: ${BUILD_DOCS}")
message(STATUS "  ENABLE_SANITIZERS: ${ENABLE_SANITIZERS}")
message(STATUS "  ENABLE_COVERAGE: ${ENABLE_COVERAGE}")
message(STATUS "")
message(STATUS "Dependencies:")
if(DEFINED QUANTLIB_TARGET)
    message(STATUS "  QuantLib: ${QUANTLIB_TARGET}")
endif()
if(DEFINED YAML_CPP_TARGET)
    message(STATUS "  yaml-cpp: ${YAML_CPP_TARGET}")
endif()
if(Boost_FOUND)
    message(STATUS "  Boost: ${Boost_VERSION}")
endif()
if(DOTNET_EXECUTABLE)
    message(STATUS "  .NET SDK: ${CMAKE_DOTNET_VERSION}")
endif()
message(STATUS "=============================")
message(STATUS "")
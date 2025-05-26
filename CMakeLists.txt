cmake_minimum_required(VERSION 3.20)

if(POLICY CMP0135)
    cmake_policy(SET CMP0135 NEW)
endif()

project(Alaris
    VERSION 1.0.0
    LANGUAGES CXX
    DESCRIPTION "High-Performance Derivatives Trading System"
)

# First, add external dependencies to ensure targets are available
add_subdirectory(external)

# Add custom CMake module path
list(APPEND CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/.cmake")

# Include helper functions first
include(Helpers)

# Include build system configurations
include(Config)        # Handles dependency configuration
include(BuildSystem)   # Handles global build settings
include(Deployment)    # Handles installation rules
include(GitInfo)       # Extracts Git information

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

# Standard CMake includes
include(GNUInstallDirs)
include(CTest)

# Find Threads (required by many components)
find_package(Threads REQUIRED)

# Add source code
add_subdirectory(src)

# Add tests if enabled
if(BUILD_TESTS)
    enable_testing()
    add_subdirectory(test)
    message(STATUS "Test suite configured")
else()
    message(STATUS "Test suite disabled (BUILD_TESTS is OFF)")
endif()

# Create uninstall target
if(NOT TARGET uninstall)
    configure_file(
        "${CMAKE_SOURCE_DIR}/.cmake/templates/cmake_uninstall.cmake.in"
        "${CMAKE_CURRENT_BINARY_DIR}/cmake_uninstall.cmake"
        IMMEDIATE @ONLY
    )
    add_custom_target(uninstall
        COMMAND ${CMAKE_COMMAND} -P "${CMAKE_CURRENT_BINARY_DIR}/cmake_uninstall.cmake"
    )
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
message(STATUS "  QuantLib: ${QUANTLIB_TARGET}")
message(STATUS "  yaml-cpp: ${YAML_CPP_TARGET}")
message(STATUS "  Boost: ${Boost_VERSION}")
message(STATUS "  .NET SDK: ${CMAKE_DOTNET_VERSION}")
message(STATUS "=============================")
message(STATUS "")

# Export compilation database for tools
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# Include build system components
include(.cmake/BuildSystem.cmake)
include(.cmake/Components.cmake)
include(.cmake/External.cmake)

# Create QuantLib component library
create_component_library(quantlib)

# Create main executable
add_executable(alaris src/quantlib/main.cpp)
target_link_libraries(alaris PRIVATE quantlib)

# Create test executable if tests are enabled
if(BUILD_TESTS)
    create_test_executable(alaris_tests)
endif()

# Install targets
install(TARGETS alaris
    RUNTIME DESTINATION bin
)

if(BUILD_TESTS)
    install(TARGETS alaris_tests
        RUNTIME DESTINATION bin
    )
endif()

# Install configuration files
install(FILES
    config/default_config.yaml
    DESTINATION etc/alaris
)

# Print build configuration summary
message(STATUS "Build Configuration:")
message(STATUS "  Build Type: ${CMAKE_BUILD_TYPE}")
message(STATUS "  C++ Standard: ${CMAKE_CXX_STANDARD}")
message(STATUS "  Build Tests: ${BUILD_TESTS}")
message(STATUS "  Build Documentation: ${BUILD_DOCS}")
message(STATUS "  Enable Sanitizers: ${ENABLE_SANITIZERS}")
message(STATUS "  Enable Coverage: ${ENABLE_COVERAGE}")
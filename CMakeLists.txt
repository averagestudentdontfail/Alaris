# CMakeLists.txt - Main build configuration for Alaris
cmake_minimum_required(VERSION 3.20)
project(Alaris VERSION 1.0.0 LANGUAGES CXX)

# Include all build system modules
include(.cmake/BuildSystem.cmake)
include(.cmake/External.cmake)
include(.cmake/Config.cmake)
include(.cmake/Data.cmake)
include(.cmake/Components.cmake)
include(.cmake/GitInfo.cmake)
include(.cmake/Deployment.cmake)

# Check git submodules
check_submodules()

# Add build info header generation
create_build_info_file("${CMAKE_BINARY_DIR}/alaris_build_info.txt")

# Configure git hooks if in development
if(EXISTS "${CMAKE_SOURCE_DIR}/.git")
    setup_git_hooks()
endif()

# Print configuration summary
message(STATUS "")
message(STATUS "=== Alaris Build Configuration ===")
message(STATUS "  Version: ${PROJECT_VERSION}")
message(STATUS "  Build Type: ${CMAKE_BUILD_TYPE}")
message(STATUS "  Platform: ${ALARIS_PLATFORM}")
message(STATUS "  C++ Standard: ${CMAKE_CXX_STANDARD}")
message(STATUS "  QuantLib: ${QUANTLIB_TARGET}")
message(STATUS "  yaml-cpp: ${YAML_CPP_TARGET}")
if(DOTNET_EXECUTABLE)
    message(STATUS "  .NET SDK: ${CMAKE_DOTNET_VERSION}")
endif()
if(ALARIS_MINIMAL_DATA)
    message(STATUS "  Data Mode: Minimal (5 symbols)")
else()
    message(STATUS "  Data Mode: Full (23 symbols)")
endif()
message(STATUS "===================================")
message(STATUS "")

# Configure all components
configure_all_components()

# Add C# project if .NET SDK is available
if(DOTNET_EXECUTABLE AND EXISTS "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.Lean.csproj")
    add_dotnet_project(lean-process "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.Lean.csproj")
elseif(DOTNET_EXECUTABLE AND EXISTS "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.sln")
    add_dotnet_project(lean-process "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.sln")
endif()

# Print build targets summary
message(STATUS "")
message(STATUS "=== Build Targets ===")
message(STATUS "  alaris           - Main trading process")
message(STATUS "  quantlib-process - QuantLib pricing process")
message(STATUS "  alaris-config    - Configuration tool")
message(STATUS "  alaris-system    - System information tool")
message(STATUS "  quantlib         - Core QuantLib library")
if(DOTNET_EXECUTABLE)
    message(STATUS "  lean-process     - C# Lean components")
endif()
message(STATUS "")
message(STATUS "=== Data Management Targets ===")
message(STATUS "  setup-data       - Set up essential market data")
message(STATUS "  clean-data       - Clean all data directories")
message(STATUS "  validate-data    - Validate data setup")
message(STATUS "===================")
message(STATUS "")

# Print useful build commands
message(STATUS "=== Build Commands ===")
message(STATUS "  Full build:             cmake --build .")
message(STATUS "  Build main only:        cmake --build . --target alaris")
message(STATUS "  Build QuantLib process: cmake --build . --target quantlib-process")
message(STATUS "  Build tools:            cmake --build . --target alaris-config alaris-system")
message(STATUS "  Set up data:            cmake --build . --target setup-data")
message(STATUS "  Validate data:          cmake --build . --target validate-data")
message(STATUS "  Clean data:             cmake --build . --target clean-data")
message(STATUS "  Install:                cmake --install .")
message(STATUS "  Clean build:            cmake --build . --target clean")
message(STATUS "")
message(STATUS "=== Quick Start ===")
message(STATUS "  mkdir build && cd build")
message(STATUS "  cmake ..")
message(STATUS "  cmake --build .")
message(STATUS "======================")
message(STATUS "")
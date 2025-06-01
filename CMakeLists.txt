# CMakeLists.txt - Main build configuration for Alaris
cmake_minimum_required(VERSION 3.20)
project(Alaris VERSION 1.0.0 LANGUAGES CXX)

# Include all build system modules
include(.cmake/BuildSystem.cmake)
include(.cmake/External.cmake)
include(.cmake/Config.cmake)
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
message(STATUS "===================================")
message(STATUS "")

# Configure all components using the new multi-target approach
configure_all_components()

# Add C# project if .NET SDK is available
if(DOTNET_EXECUTABLE AND EXISTS "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.sln")
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
message(STATUS "===================")
message(STATUS "")

# Print useful build commands
message(STATUS "=== Build Commands ===")
message(STATUS "  Build all:              cmake --build build")
message(STATUS "  Build main only:        cmake --build build --target alaris")
message(STATUS "  Build QuantLib process: cmake --build build --target quantlib-process")
message(STATUS "  Build tools:            cmake --build build --target alaris-config alaris-system")
message(STATUS "  Install:                cmake --install build")
message(STATUS "  Clean:                  cmake --build build --target clean")
message(STATUS "======================")
message(STATUS "")
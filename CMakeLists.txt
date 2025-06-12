# CMakeLists.txt - Alaris Trading System
cmake_minimum_required(VERSION 3.20)
project(Alaris VERSION 1.0.0 LANGUAGES CXX)

# --- 1. Include Configuration Definitions ---
include(.cmake/BuildSystem.cmake)
include(.cmake/External.cmake)
include(.cmake/Config.cmake)
include(.cmake/Components.cmake)
include(.cmake/GitInfo.cmake)

# --- 2. Perform Initial Setup ---
create_build_info_file("${CMAKE_BINARY_DIR}/alaris_build_info.txt")

# --- 3. Define All C++ and C# Targets ---
configure_all_components()

if(DOTNET_EXECUTABLE)
    add_dotnet_project(lean-process "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.Lean.csproj")
endif()

# --- 4. Configure Deployment (after targets are defined) ---
include(.cmake/Deployment.cmake)

# --- 5. Build Summary ---
message(STATUS "")
message(STATUS "=== Alaris Build Configuration ===")
message(STATUS "  Version:      ${PROJECT_VERSION}")
if(GIT_COMMIT_HASH AND NOT GIT_COMMIT_HASH STREQUAL "unknown")
    message(STATUS "  Git Commit:   ${GIT_COMMIT_HASH}")
endif()
message(STATUS "  Build Type:   ${CMAKE_BUILD_TYPE}")
message(STATUS "  Platform:     ${ALARIS_PLATFORM}")
if(DOTNET_EXECUTABLE)
    message(STATUS "  .NET SDK:     ${CMAKE_DOTNET_VERSION}")
endif()
message(STATUS "==================================")
message(STATUS "Ready to build: cmake --build .")
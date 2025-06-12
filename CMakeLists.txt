# Root CMakeLists.txt for the Alaris Trading System
cmake_minimum_required(VERSION 3.20)
project(Alaris VERSION 1.0.0 LANGUAGES CXX)

# 1. Include all build system modules.
# These files define how the project is configured and built.
include(.cmake/BuildSystem.cmake)
include(.cmake/External.cmake)
include(.cmake/Config.cmake)
include(.cmake/Data.cmake)
include(.cmake/Components.cmake)
include(.cmake/GitInfo.cmake)
include(.cmake/Deployment.cmake)

# 2. Check for Git submodules (e.g., QuantLib, yaml-cpp)
# This ensures that external dependencies are available if not installed on the system.
check_submodules()

# 3. Generate a build information file.
# This captures version, git commit, and other metadata for logging and debugging.
create_build_info_file("${CMAKE_BINARY_DIR}/alaris_build_info.txt")

# 4. Configure all C++ components (executables and libraries).
# This function is defined in .cmake/Components.cmake and sets up all C++ targets.
configure_all_components()

# 5. Configure the C# Lean process project.
# This creates a build target for the C# application.
if(DOTNET_EXECUTABLE AND EXISTS "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.Lean.csproj")
    add_dotnet_project(lean-process "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.Lean.csproj")
elseif(DOTNET_EXECUTABLE AND EXISTS "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.sln")
    add_dotnet_project(lean-process "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.sln")
endif()

# 6. Print a summary of the build configuration.
# This provides clear feedback to the user about what is being built.
message(STATUS "")
message(STATUS "=== Alaris Build Configuration Summary ===")
message(STATUS "  Version:      ${PROJECT_VERSION}")
if(HAS_GIT)
    message(STATUS "  Git Commit:   ${GIT_COMMIT_HASH_SHORT}")
endif()
message(STATUS "  Build Type:   ${CMAKE_BUILD_TYPE}")
message(STATUS "  Platform:     ${ALARIS_PLATFORM}")
message(STATUS "  QuantLib:     ${QUANTLIB_TARGET}")
if(DOTNET_EXECUTABLE)
    message(STATUS "  .NET SDK:     ${CMAKE_DOTNET_VERSION}")
endif()
message(STATUS "  Build system configured for production use with Interactive Brokers.")
message(STATUS "==========================================")
message(STATUS "")
message(STATUS "Ready to build. Run: cmake --build .")
message(STATUS "After building, run './start-alaris.sh' to start the system.")
message(STATUS "")

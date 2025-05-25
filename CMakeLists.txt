cmake_minimum_required(VERSION 3.20)

if(POLICY CMP0135)
    cmake_policy(SET CMP0135 NEW) # Required for project() to not set CMAKE_PROJECT_VERSION
endif()

project(Alaris
    VERSION 1.0.0
    LANGUAGES CXX
    DESCRIPTION "High-Performance Derivatives Trading System"
)

# Add external dependencies first.
# This ensures targets like YAML_CPP_TARGET and QUANTLIB_TARGET are defined
# before other CMake modules or source directories try to use them.
add_subdirectory(external)

# Add custom CMake module path
list(APPEND CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/.cmake")

# Include build system configurations
include(Config)        # Handles dependency configuration (Boost, and uses external for QL, yaml-cpp)
include(BuildSystem)   # Handles global build settings, CXX flags, .NET config
include(Deployment)    # Handles installation rules
include(GitInfo)       # Extracts Git information (call get_git_info() where needed)

# Detect .NET SDK (moved after project() and GitInfo potentially, though BuildSystem now handles it)
# BuildSystem.cmake now has configure_dotnet_build(), which relies on DOTNET_EXECUTABLE
find_program(DOTNET_EXECUTABLE dotnet QUIET) # QUIET as BuildSystem.cmake will message
if(DOTNET_EXECUTABLE)
    execute_process(
        COMMAND ${DOTNET_EXECUTABLE} --version
        OUTPUT_VARIABLE DOTNET_VERSION
        OUTPUT_STRIP_TRAILING_WHITESPACE
    )
    set(CMAKE_DOTNET_VERSION ${DOTNET_VERSION} CACHE STRING ".NET SDK version")
endif()

# Call functions from included modules if they don't run automatically
get_git_info() # From GitInfo.cmake

# Add source code and test subdirectories
add_subdirectory(src)

if(BUILD_TESTS) # BUILD_TESTS is an option defined in BuildSystem.cmake
    enable_testing()
    add_subdirectory(test)
    message(STATUS "Test suite configured.")
else()
    message(STATUS "Test suite not configured (BUILD_TESTS is OFF).")
endif()

# The C# project handling seems to be in external/CMakeLists.txt and BuildSystem.cmake (add_dotnet_project)
# No need to add Alaris.Lean.csproj separately here if it's part of the solution built by external/lean_build
# or managed via add_dotnet_project from BuildSystem.cmake.

message(STATUS "Alaris project root configuration finished.")
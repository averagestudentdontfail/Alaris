# .cmake/BuildSystem.cmake
# Build system configuration for Alaris
include(CMakeDependentOption)

# Platform detection
if(WIN32)
    set(ALARIS_PLATFORM "Windows")
elseif(APPLE)
    set(ALARIS_PLATFORM "Darwin")
elseif(UNIX)
    set(ALARIS_PLATFORM "Linux")
else()
    set(ALARIS_PLATFORM "Unknown")
endif()

# Build type configuration
if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE "Release" CACHE STRING "Build type (Debug, Release, RelWithDebInfo, MinSizeRel)" FORCE)
endif()
set_property(CACHE CMAKE_BUILD_TYPE PROPERTY STRINGS Debug Release RelWithDebInfo MinSizeRel)

# Standard CMake settings
set(CMAKE_CXX_STANDARD 20 CACHE STRING "C++ standard to use")
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON) # Useful for C++ tooling
set(CMAKE_POSITION_INDEPENDENT_CODE ON) # Good practice for shared libraries

# Set output directories
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# Enable testing
include(CTest)
enable_testing()

# Find required packages
find_package(Threads REQUIRED)

# Build options
option(BUILD_TESTS "Build test suite" ON)
option(BUILD_DOCS "Build documentation" OFF)
option(ENABLE_SANITIZERS "Enable sanitizers (for Debug builds)" OFF)
option(ENABLE_COVERAGE "Enable code coverage (for Debug builds)" OFF)
option(ALARIS_INSTALL_DEVELOPMENT "Install development files (headers, etc.)" ON)

# Git submodule management (optional check during configure time)
function(check_submodules)
    if(EXISTS "${CMAKE_SOURCE_DIR}/.git")
        find_package(Git QUIET)
        if(GIT_FOUND)
            execute_process(
                COMMAND ${GIT_EXECUTABLE} submodule status --recursive
                WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
                OUTPUT_VARIABLE SUBMODULE_STATUS
                RESULT_VARIABLE SUBMODULE_RESULT
                OUTPUT_STRIP_TRAILING_WHITESPACE
            )
            if(NOT SUBMODULE_RESULT EQUAL 0 OR SUBMODULE_STATUS MATCHES "^-")
                message(WARNING "Git submodules might not be initialized or updated. Consider running 'git submodule update --init --recursive'.\n${SUBMODULE_STATUS}")
            endif()
        endif()
    endif()
endfunction()

# .NET configuration
function(add_dotnet_project TARGET_NAME PROJECT_FILE)
    if(DOTNET_EXECUTABLE)
        add_custom_target(${TARGET_NAME} ALL # Build with all C++ targets
            COMMAND ${DOTNET_EXECUTABLE} build "${PROJECT_FILE}" -c ${CMAKE_BUILD_TYPE}
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR} # Or specific C# project dir
            COMMENT "Building .NET project: ${PROJECT_FILE}"
            VERBATIM
        )
    else()
        message(WARNING ".NET SDK not found. Cannot create build target for ${PROJECT_FILE}")
    endif()
endfunction()

message(STATUS "BuildSystem.cmake: Global build settings applied.")
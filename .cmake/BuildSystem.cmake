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
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Set output directories for C++ executables
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# Find required system packages
find_package(Threads REQUIRED)

# Build options
option(BUILD_DOCS "Build documentation" OFF)
option(ENABLE_SANITIZERS "Enable sanitizers (for Debug builds)" OFF)
option(ENABLE_COVERAGE "Enable code coverage (for Debug builds)" OFF)
option(ALARIS_INSTALL_DEVELOPMENT "Install development files (headers, etc.)" ON)

# Git submodule management
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
                message(WARNING "Git submodules might not be initialized or updated. Run 'git submodule update --init --recursive'.")
            endif()
        endif()
    endif()
endfunction()

# --- CORRECTED .NET CONFIGURATION ---
# This function now correctly builds the C# project and copies the output
# to the main /bin directory in the build folder.
function(add_dotnet_project TARGET_NAME PROJECT_FILE)
    if(NOT DOTNET_EXECUTABLE)
        message(WARNING ".NET SDK not found. Cannot create build target for ${PROJECT_FILE}")
        return()
    endif()

    # Define paths
    get_filename_component(PROJECT_DIR ${PROJECT_FILE} DIRECTORY)
    set(DOTNET_OUTPUT_DIR "${PROJECT_DIR}/bin")
    set(FINAL_DESTINATION_DIR "${CMAKE_BINARY_DIR}/bin")

    # This target performs two steps:
    # 1. Builds the .NET project.
    # 2. Copies the build output to the main bin directory.
    add_custom_target(${TARGET_NAME} ALL
        # Step 1: Build the project
        COMMAND ${DOTNET_EXECUTABLE} build "${PROJECT_FILE}" -c ${CMAKE_BUILD_TYPE}
        
        # Step 2: Copy the output files to the main bin directory
        COMMAND ${CMAKE_COMMAND} -E copy_directory "${DOTNET_OUTPUT_DIR}" "${FINAL_DESTINATION_DIR}"
        
        WORKING_DIRECTORY ${PROJECT_DIR}
        COMMENT "Building and deploying .NET project: ${PROJECT_FILE}"
        VERBATIM
    )
endfunction()

message(STATUS "BuildSystem.cmake: Global build settings applied.")

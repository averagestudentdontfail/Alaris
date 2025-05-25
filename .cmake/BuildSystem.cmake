# Build system configuration for Alaris
include(CMakeDependentOption)

# Platform detection and configuration
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
    set(CMAKE_BUILD_TYPE "Release" CACHE STRING "Build type" FORCE)
endif()

# Modern CMake configuration
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Compiler configuration
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    # Common compiler flags
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wall -Wextra -Wpedantic")
    
    # Debug flags
    set(CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG} -g -O0")
    
    # Release flags
    set(CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE} -O3 -DNDEBUG")
endif()

# Build options
option(BUILD_TESTS "Build test suite" ON)
option(BUILD_DOCS "Build documentation" OFF)
option(ENABLE_SANITIZERS "Enable sanitizers" OFF)
option(ENABLE_COVERAGE "Enable code coverage" OFF)
option(ALARIS_INSTALL_DEVELOPMENT "Install development files" OFF)

# Sanitizer configuration
if(ENABLE_SANITIZERS AND CMAKE_BUILD_TYPE STREQUAL "Debug")
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        add_compile_options(-fsanitize=address,undefined)
        add_link_options(-fsanitize=address,undefined)
    endif()
endif()

# Coverage configuration
if(ENABLE_COVERAGE AND CMAKE_BUILD_TYPE STREQUAL "Debug")
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        add_compile_options(--coverage)
        add_link_options(--coverage)
    endif()
endif()

# Dependency management
function(find_required_package PACKAGE_NAME)
    find_package(${PACKAGE_NAME} REQUIRED)
    if(NOT ${PACKAGE_NAME}_FOUND)
        message(FATAL_ERROR "${PACKAGE_NAME} not found. Please install it.")
    endif()
endfunction()

# Git submodule management
function(check_submodules)
    if(EXISTS "${CMAKE_SOURCE_DIR}/.git")
        find_package(Git REQUIRED)
        execute_process(
            COMMAND ${GIT_EXECUTABLE} submodule status --recursive
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
            OUTPUT_VARIABLE SUBMODULE_STATUS
            RESULT_VARIABLE SUBMODULE_RESULT
        )
        
        if(NOT SUBMODULE_RESULT EQUAL 0)
            message(WARNING "Git submodule check failed. Some dependencies might be missing.")
        endif()
    endif()
endfunction()

# .NET configuration
function(configure_dotnet_build)
    if(DOTNET_EXECUTABLE)
        # Set .NET SDK version
        set(CMAKE_DOTNET_VERSION ${DOTNET_VERSION} CACHE STRING ".NET SDK version")
        
        # Configure .NET build properties
        set(DOTNET_BUILD_PROPERTIES
            "TreatWarningsAsErrors=false"
            "WarningsAsErrors=false"
            "GenerateDocumentationFile=false"
        )
        
        # Add .NET project function
        function(add_dotnet_project TARGET_NAME PROJECT_FILE)
            add_custom_target(${TARGET_NAME}
                COMMAND ${DOTNET_EXECUTABLE} build ${PROJECT_FILE} -c ${CMAKE_BUILD_TYPE}
                WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
                COMMENT "Building .NET project ${TARGET_NAME}"
            )
        endfunction()
    endif()
endfunction()

# Execute configuration steps
check_submodules()
configure_dotnet_build()

# Export configuration
set(ALARIS_BUILD_SYSTEM_CONFIGURED TRUE CACHE INTERNAL "Build system configured" FORCE) 
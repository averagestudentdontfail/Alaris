# cmake/utils/ModernCMake.cmake
# Modern CMake practices enforcement

# Enforce minimum CMake version
cmake_minimum_required(VERSION 3.20)

# Set policies
if(POLICY CMP0135)
    cmake_policy(SET CMP0135 NEW)
endif()

# Function to enforce modern CMake practices
function(enforce_modern_cmake)
    # Disable global commands
    set(CMAKE_CXX_STANDARD 20 PARENT_SCOPE)
    set(CMAKE_CXX_STANDARD_REQUIRED ON PARENT_SCOPE)
    set(CMAKE_CXX_EXTENSIONS OFF PARENT_SCOPE)
    
    # Enable position independent code
    set(CMAKE_POSITION_INDEPENDENT_CODE ON PARENT_SCOPE)
    
    # Enable export compile commands
    set(CMAKE_EXPORT_COMPILE_COMMANDS ON PARENT_SCOPE)
    
    # Enable folders
    set_property(GLOBAL PROPERTY USE_FOLDERS ON)
    
    # Set default build type
    if(NOT CMAKE_BUILD_TYPE)
        set(CMAKE_BUILD_TYPE Release CACHE STRING "Build type" FORCE)
        set_property(CACHE CMAKE_BUILD_TYPE PROPERTY STRINGS 
            "Debug" "Release" "RelWithDebInfo" "MinSizeRel" "DebugOpt" "Profile")
    endif()
    
    # Enable testing
    enable_testing()
    
    # Set output directories
    set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin PARENT_SCOPE)
    set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib PARENT_SCOPE)
    set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib PARENT_SCOPE)
    
    # Set test output directory
    set(CMAKE_TEST_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/test PARENT_SCOPE)
    
    # Set documentation output directory
    set(CMAKE_DOC_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/doc PARENT_SCOPE)
endfunction()

# Function to create a modern library target
function(create_modern_library)
    set(options SHARED STATIC INTERFACE)
    set(oneValueArgs NAME)
    set(multiValueArgs SOURCES HEADERS PUBLIC_DEPS PRIVATE_DEPS)
    
    cmake_parse_arguments(ARG "${options}" "${oneValueArgs}" "${multiValueArgs}" ${ARGN})
    
    if(ARG_INTERFACE)
        add_library(${ARG_NAME} INTERFACE)
    elseif(ARG_SHARED)
        add_library(${ARG_NAME} SHARED ${ARG_SOURCES} ${ARG_HEADERS})
    else()
        add_library(${ARG_NAME} STATIC ${ARG_SOURCES} ${ARG_HEADERS})
    endif()
    
    if(ARG_PUBLIC_DEPS)
        target_link_libraries(${ARG_NAME} PUBLIC ${ARG_PUBLIC_DEPS})
    endif()
    
    if(ARG_PRIVATE_DEPS)
        target_link_libraries(${ARG_NAME} PRIVATE ${ARG_PRIVATE_DEPS})
    endif()
    
    target_include_directories(${ARG_NAME}
        PUBLIC
            $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
            $<INSTALL_INTERFACE:include>
        PRIVATE
            ${CMAKE_CURRENT_SOURCE_DIR}/src
    )
    
    set_target_properties(${ARG_NAME} PROPERTIES
        CXX_STANDARD 20
        CXX_STANDARD_REQUIRED ON
        CXX_EXTENSIONS OFF
        POSITION_INDEPENDENT_CODE ON
    )
endfunction()

# Function to create a modern executable target
function(create_modern_executable)
    set(oneValueArgs NAME)
    set(multiValueArgs SOURCES HEADERS DEPS)
    
    cmake_parse_arguments(ARG "" "${oneValueArgs}" "${multiValueArgs}" ${ARGN})
    
    add_executable(${ARG_NAME} ${ARG_SOURCES} ${ARG_HEADERS})
    
    if(ARG_DEPS)
        target_link_libraries(${ARG_NAME} PRIVATE ${ARG_DEPS})
    endif()
    
    set_target_properties(${ARG_NAME} PROPERTIES
        CXX_STANDARD 20
        CXX_STANDARD_REQUIRED ON
        CXX_EXTENSIONS OFF
        RUNTIME_OUTPUT_DIRECTORY ${CMAKE_RUNTIME_OUTPUT_DIRECTORY}
    )
endfunction()

# Function to create a modern test target
function(create_modern_test)
    set(oneValueArgs NAME)
    set(multiValueArgs SOURCES HEADERS DEPS)
    
    cmake_parse_arguments(ARG "" "${oneValueArgs}" "${multiValueArgs}" ${ARGN})
    
    add_executable(${ARG_NAME} ${ARG_SOURCES} ${ARG_HEADERS})
    
    if(ARG_DEPS)
        target_link_libraries(${ARG_NAME} PRIVATE ${ARG_DEPS})
    endif()
    
    set_target_properties(${ARG_NAME} PROPERTIES
        CXX_STANDARD 20
        CXX_STANDARD_REQUIRED ON
        CXX_EXTENSIONS OFF
        RUNTIME_OUTPUT_DIRECTORY ${CMAKE_TEST_OUTPUT_DIRECTORY}
    )
    
    add_test(NAME ${ARG_NAME} COMMAND ${ARG_NAME})
endfunction() 
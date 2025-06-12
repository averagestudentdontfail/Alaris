# .cmake/BuildSystem.cmake
# Streamlined build system for Alaris trading system

# Platform detection
if(WIN32)
    set(ALARIS_PLATFORM "Windows")
elseif(APPLE)
    set(ALARIS_PLATFORM "Darwin")
elseif(UNIX)
    set(ALARIS_PLATFORM "Linux")
endif()

# Build configuration
if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE "Release" CACHE STRING "Build type" FORCE)
endif()

set(CMAKE_CXX_STANDARD 20 CACHE STRING "C++ standard")
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Output directories
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

find_package(Threads REQUIRED)

# .NET project build function
function(add_dotnet_project TARGET_NAME PROJECT_FILE)
    if(NOT DOTNET_EXECUTABLE)
        return()
    endif()

    get_filename_component(PROJECT_DIR ${PROJECT_FILE} DIRECTORY)
    set(DOTNET_OUTPUT_DIR "${PROJECT_DIR}/bin")
    set(FINAL_DESTINATION_DIR "${CMAKE_BINARY_DIR}/bin")

    add_custom_target(${TARGET_NAME} ALL
        COMMAND ${DOTNET_EXECUTABLE} build "${PROJECT_FILE}" -c ${CMAKE_BUILD_TYPE}
        COMMAND ${CMAKE_COMMAND} -E copy_directory "${DOTNET_OUTPUT_DIR}" "${FINAL_DESTINATION_DIR}"
        WORKING_DIRECTORY ${PROJECT_DIR}
        VERBATIM
    )
    
    if(TARGET quantlib AND TARGET alaris)
        add_dependencies(${TARGET_NAME} quantlib alaris)
    endif()
endfunction()
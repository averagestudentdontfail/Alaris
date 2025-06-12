# .cmake/External.cmake

set(QUANTLIB_TARGET "")
set(YAML_CPP_TARGET "")

option(USE_SYSTEM_QUANTLIB "Use system QuantLib" ON)
option(USE_SYSTEM_YAMLCPP "Use system yaml-cpp" ON)

# QuantLib Configuration
if(USE_SYSTEM_QUANTLIB)
    find_package(QuantLib QUIET)
    
    if(NOT QuantLib_FOUND)
        find_package(PkgConfig QUIET)
        if(PKG_CONFIG_FOUND)
            pkg_check_modules(QUANTLIB QUIET quantlib)
            if(QUANTLIB_FOUND)
                add_library(QuantLib::QuantLib INTERFACE IMPORTED)
                target_include_directories(QuantLib::QuantLib SYSTEM INTERFACE ${QUANTLIB_INCLUDE_DIRS})
                target_link_libraries(QuantLib::QuantLib INTERFACE ${QUANTLIB_LIBRARIES})
                set(QuantLib_FOUND TRUE)
            endif()
        endif()
    endif()
    
    if(NOT QuantLib_FOUND)
        find_path(QUANTLIB_INCLUDE_DIR ql/quantlib.hpp)
        find_library(QUANTLIB_LIBRARY NAMES QuantLib quantlib)
        
        if(QUANTLIB_INCLUDE_DIR AND QUANTLIB_LIBRARY)
            add_library(QuantLib::QuantLib INTERFACE IMPORTED)
            target_include_directories(QuantLib::QuantLib SYSTEM INTERFACE ${QUANTLIB_INCLUDE_DIR})
            target_link_libraries(QuantLib::QuantLib INTERFACE ${QUANTLIB_LIBRARY})
            set(QuantLib_FOUND TRUE)
        endif()
    endif()
    
    if(QuantLib_FOUND)
        set(QUANTLIB_TARGET QuantLib::QuantLib)
    else()
        set(USE_SYSTEM_QUANTLIB OFF)
    endif()
endif()

if(NOT USE_SYSTEM_QUANTLIB)
    if(EXISTS "${CMAKE_SOURCE_DIR}/external/QuantLib/CMakeLists.txt")
        set(QL_BUILD_EXAMPLES OFF CACHE BOOL "")
        set(QL_BUILD_TEST_SUITE OFF CACHE BOOL "")
        set(QL_BUILD_BENCHMARK OFF CACHE BOOL "")
        add_subdirectory(${CMAKE_SOURCE_DIR}/external/QuantLib EXCLUDE_FROM_ALL)
        set(QUANTLIB_TARGET QuantLib)
    elseif(EXISTS "${CMAKE_SOURCE_DIR}/external/quant/CMakeLists.txt")
        add_subdirectory(${CMAKE_SOURCE_DIR}/external/quant EXCLUDE_FROM_ALL)
        set(QUANTLIB_TARGET ql_library)
    else()
        message(FATAL_ERROR "QuantLib not found. Install system package or add to external/")
    endif()
endif()

# yaml-cpp Configuration
if(USE_SYSTEM_YAMLCPP)
    find_package(yaml-cpp QUIET)
    if(yaml-cpp_FOUND)
        set(YAML_CPP_TARGET yaml-cpp::yaml-cpp)
    else()
        find_package(PkgConfig QUIET)
        if(PKG_CONFIG_FOUND)
            pkg_check_modules(YAML_CPP QUIET yaml-cpp)
            if(YAML_CPP_FOUND)
                add_library(yaml-cpp::yaml-cpp INTERFACE IMPORTED)
                target_include_directories(yaml-cpp::yaml-cpp SYSTEM INTERFACE ${YAML_CPP_INCLUDE_DIRS})
                target_link_libraries(yaml-cpp::yaml-cpp INTERFACE ${YAML_CPP_LIBRARIES})
                set(YAML_CPP_TARGET yaml-cpp::yaml-cpp)
            else()
                set(USE_SYSTEM_YAMLCPP OFF)
            endif()
        else()
            set(USE_SYSTEM_YAMLCPP OFF)
        endif()
    endif()
endif()

if(NOT USE_SYSTEM_YAMLCPP)
    if(EXISTS "${CMAKE_SOURCE_DIR}/external/yaml-cpp/CMakeLists.txt")
        set(YAML_CPP_BUILD_TESTS OFF CACHE BOOL "")
        set(YAML_CPP_BUILD_TOOLS OFF CACHE BOOL "")
        set(YAML_CPP_BUILD_CONTRIB OFF CACHE BOOL "")
        add_subdirectory(${CMAKE_SOURCE_DIR}/external/yaml-cpp EXCLUDE_FROM_ALL)
        set(YAML_CPP_TARGET yaml-cpp)
    else()
        message(FATAL_ERROR "yaml-cpp not found. Install system package or add to external/")
    endif()
endif()

# .NET SDK Detection
find_program(DOTNET_EXECUTABLE dotnet QUIET)
if(DOTNET_EXECUTABLE)
    execute_process(
        COMMAND ${DOTNET_EXECUTABLE} --version
        OUTPUT_VARIABLE DOTNET_VERSION
        OUTPUT_STRIP_TRAILING_WHITESPACE
        ERROR_QUIET
    )
    set(CMAKE_DOTNET_VERSION ${DOTNET_VERSION} CACHE STRING ".NET SDK version")
endif()

# Validation
if(NOT QUANTLIB_TARGET)
    message(FATAL_ERROR "QuantLib configuration failed")
endif()

if(NOT YAML_CPP_TARGET)
    message(FATAL_ERROR "yaml-cpp configuration failed")
endif()
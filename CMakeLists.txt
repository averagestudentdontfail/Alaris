cmake_minimum_required(VERSION 3.20)

if(POLICY CMP0135)
    cmake_policy(SET CMP0135 NEW)
endif()

project(Alaris
    VERSION 1.0.0
    LANGUAGES CXX
    DESCRIPTION "High-Performance Derivatives Trading System"
)

list(APPEND CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/.cmake")

# Include centralized build system modules in correct order
include(BuildSystem)   # Handles global build settings
include(GitInfo)       # Extracts Git information
include(External)      # Handles external dependencies (must come before Config)
include(Config)        # Handles dependency configuration (depends on External)
include(Deployment)    # Handles installation rules

# Standard CMake setup
include(GNUInstallDirs)
include(CTest)
find_package(Threads REQUIRED)

if(BUILD_TESTS)
    enable_testing()
    message(STATUS "Test suite configured")
else()
    message(STATUS "Test suite disabled (BUILD_TESTS is OFF)")
endif()

# ========================================
# Source Files Discovery (Centralized)
# ========================================

# Discover QuantLib process source files
file(GLOB_RECURSE QUANTLIB_SOURCES 
    src/quantlib/*.cpp
    src/quantlib/*.c
)

file(GLOB_RECURSE QUANTLIB_HEADERS
    src/quantlib/*.h
    src/quantlib/*.hpp
)

# Filter out any files we don't want
list(FILTER QUANTLIB_SOURCES EXCLUDE REGEX ".*\\.tmp$")
list(FILTER QUANTLIB_HEADERS EXCLUDE REGEX ".*\\.tmp$")

# Discover test source files
if(BUILD_TESTS)
    file(GLOB_RECURSE TEST_SOURCES
        test/*.cpp
        test/*.c
    )
    
    file(GLOB_RECURSE TEST_HEADERS
        test/*.h
        test/*.hpp
    )
    
    list(FILTER TEST_SOURCES EXCLUDE REGEX ".*\\.tmp$")
    list(FILTER TEST_HEADERS EXCLUDE REGEX ".*\\.tmp$")
endif()

# ========================================
# QuantLib Process Library & Executable
# ========================================

if(QUANTLIB_SOURCES)
    message(STATUS "Found QuantLib process sources: ${QUANTLIB_SOURCES}")
    
    # Create the QuantLib core library
    add_library(quantlib_core STATIC ${QUANTLIB_SOURCES} ${QUANTLIB_HEADERS})
    
    # FIXED: Remove duplicate compiler options - these are now handled in Config.cmake
    # target_compile_options(quantlib_core PRIVATE "-Wall -Wextra -Wpedantic")
    
    # Set target properties
    target_include_directories(quantlib_core PUBLIC
        ${CMAKE_SOURCE_DIR}/src
        ${CMAKE_SOURCE_DIR}/src/quantlib
    )
    
    # Link against external dependencies
    target_link_libraries(quantlib_core PUBLIC
        ${QUANTLIB_TARGET}
        ${YAML_CPP_TARGET}
        Threads::Threads
    )
    
    # Link against Boost if found
    if(TARGET Boost::boost)
        target_link_libraries(quantlib_core PUBLIC Boost::boost)
    elseif(Boost_FOUND)
        target_include_directories(quantlib_core PUBLIC ${Boost_INCLUDE_DIRS})
        target_link_libraries(quantlib_core PUBLIC ${Boost_LIBRARIES})
    endif()
    
    # Set compiler features
    target_compile_features(quantlib_core PUBLIC cxx_std_20)
    
    # Add performance optimizations for Release builds
    if(CMAKE_BUILD_TYPE STREQUAL "Release")
        if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
            target_compile_options(quantlib_core PRIVATE
                -march=native
                -mtune=native
            )
        endif()
    endif()
    
    # Create the main executable
    add_executable(alaris_quantlib ${QUANTLIB_SOURCES})
    
    # FIXED: Remove duplicate compiler options
    # target_compile_options(alaris_quantlib PRIVATE "-Wall -Wextra -Wpedantic")
    
    # Link the executable against the core library  
    target_link_libraries(alaris_quantlib PRIVATE quantlib_core)
    
    # Set executable properties
    set_target_properties(alaris_quantlib PROPERTIES
        OUTPUT_NAME alaris
        RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    )
    
    message(STATUS "Created QuantLib targets: quantlib_core (library), alaris_quantlib (executable)")
else()
    message(WARNING "No QuantLib process source files found in src/quantlib/")
endif()

# ========================================
# Test Suite
# ========================================

if(BUILD_TESTS AND GTest_FOUND AND TEST_SOURCES)
    message(STATUS "Found test sources: ${TEST_SOURCES}")
    
    # Create test executable
    add_executable(alaris_tests ${TEST_SOURCES} ${TEST_HEADERS})
    
    # FIXED: Remove duplicate compiler options
    # target_compile_options(alaris_tests PRIVATE "-Wall -Wextra -Wpedantic")
    
    # Link against required libraries
    target_link_libraries(alaris_tests PRIVATE
        GTest::GTest
        GTest::Main
        Threads::Threads
    )
    
    # Link against our core library if it exists
    if(TARGET quantlib_core)
        target_link_libraries(alaris_tests PRIVATE quantlib_core)
    endif()
    
    # Link against external dependencies
    target_link_libraries(alaris_tests PRIVATE
        ${QUANTLIB_TARGET}
        ${YAML_CPP_TARGET}
    )
    
    # Set include directories
    target_include_directories(alaris_tests PRIVATE
        ${CMAKE_SOURCE_DIR}/src
        ${CMAKE_SOURCE_DIR}/test
    )
    
    # Set compiler features
    target_compile_features(alaris_tests PRIVATE cxx_std_20)
    
    # Set test properties
    set_target_properties(alaris_tests PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    )
    
    # Register tests with CTest
    if(COMMAND gtest_discover_tests)
        include(GoogleTest)
        gtest_discover_tests(alaris_tests
            WORKING_DIRECTORY ${CMAKE_BINARY_DIR}
            PROPERTIES VS_DEBUGGER_WORKING_DIRECTORY "${CMAKE_BINARY_DIR}"
        )
    else()
        # Fallback: add single test
        add_test(NAME alaris_tests COMMAND alaris_tests)
    endif()
    
    # Add custom test target for easier running
    add_custom_target(run_tests
        COMMAND ${CMAKE_CTEST_COMMAND} --output-on-failure
        DEPENDS alaris_tests
        WORKING_DIRECTORY ${CMAKE_BINARY_DIR}
        COMMENT "Running all tests"
    )
    
    message(STATUS "Created test target: alaris_tests")
elseif(BUILD_TESTS AND NOT GTest_FOUND)
    message(WARNING "BUILD_TESTS enabled but GTest not found")
elseif(BUILD_TESTS AND NOT TEST_SOURCES)
    message(WARNING "BUILD_TESTS enabled but no test sources found in test/")
endif()

# ========================================
# .NET Components
# ========================================

if(DOTNET_EXECUTABLE AND EXISTS "${CMAKE_SOURCE_DIR}/src/csharp")
    message(STATUS "Configuring .NET components")
    
    # Find C# source files
    file(GLOB_RECURSE CSHARP_SOURCES
        src/csharp/*.cs
    )
    
    if(CSHARP_SOURCES)
        # Create custom target for C# build
        add_custom_target(alaris_csharp ALL
            COMMAND ${DOTNET_EXECUTABLE} build ${CMAKE_SOURCE_DIR}/src/csharp/Alaris.Lean.csproj -c ${CMAKE_BUILD_TYPE}
            WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}/src/csharp
            COMMENT "Building C# components"
            SOURCES ${CSHARP_SOURCES}
        )
        
        message(STATUS "Created C# target: alaris_csharp")
    else()
        message(STATUS "No C# source files found in src/csharp/")
    endif()
endif()

# ========================================
# Installation
# ========================================

# Install executables
if(TARGET alaris_quantlib)
    install(TARGETS alaris_quantlib
        RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
        COMPONENT Runtime
    )
endif()

if(TARGET quantlib_core)
    install(TARGETS quantlib_core
        LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
        ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
        COMPONENT Development
    )
endif()

if(TARGET alaris_tests)
    install(TARGETS alaris_tests
        RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
        COMPONENT Testing
    )
endif()

# Install configuration files
if(EXISTS "${CMAKE_SOURCE_DIR}/config/default_config.yaml")
    install(FILES config/default_config.yaml
        DESTINATION ${CMAKE_INSTALL_SYSCONFDIR}/alaris
        COMPONENT Configuration
    )
endif()

# Install headers if development installation is enabled
if(ALARIS_INSTALL_DEVELOPMENT AND QUANTLIB_HEADERS)
    foreach(HEADER_FILE ${QUANTLIB_HEADERS})
        file(RELATIVE_PATH REL_PATH ${CMAKE_SOURCE_DIR}/src/quantlib ${HEADER_FILE})
        get_filename_component(HEADER_DIR ${REL_PATH} DIRECTORY)
        install(FILES ${HEADER_FILE}
                DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}/alaris/${HEADER_DIR}
                COMPONENT Development)
    endforeach()
endif()

# ========================================
# Configuration Summary
# ========================================

message(STATUS "")
message(STATUS "=== Configuration Summary ===")
message(STATUS "Project: ${PROJECT_NAME} v${PROJECT_VERSION}")
message(STATUS "Build Type: ${CMAKE_BUILD_TYPE}")
message(STATUS "Install Prefix: ${CMAKE_INSTALL_PREFIX}")
message(STATUS "C++ Standard: ${CMAKE_CXX_STANDARD}")
message(STATUS "Compiler: ${CMAKE_CXX_COMPILER_ID} ${CMAKE_CXX_COMPILER_VERSION}")
message(STATUS "")
message(STATUS "Components:")
if(TARGET alaris_quantlib)
    message(STATUS "  QuantLib Process: alaris_quantlib -> alaris")
endif()
if(TARGET quantlib_core)
    message(STATUS "  Core Library: quantlib_core")
endif()
if(TARGET alaris_tests)
    message(STATUS "  Test Suite: alaris_tests")
endif()
if(TARGET alaris_csharp)
    message(STATUS "  C# Components: alaris_csharp")
endif()
message(STATUS "")
message(STATUS "Options:")
message(STATUS "  BUILD_TESTS: ${BUILD_TESTS}")
message(STATUS "  BUILD_DOCS: ${BUILD_DOCS}")
message(STATUS "  ENABLE_SANITIZERS: ${ENABLE_SANITIZERS}")
message(STATUS "  ENABLE_COVERAGE: ${ENABLE_COVERAGE}")
message(STATUS "")
message(STATUS "Dependencies:")
if(DEFINED QUANTLIB_TARGET)
    message(STATUS "  QuantLib: ${QUANTLIB_TARGET}")
endif()
if(DEFINED YAML_CPP_TARGET)
    message(STATUS "  yaml-cpp: ${YAML_CPP_TARGET}")
endif()
if(Boost_FOUND)
    message(STATUS "  Boost: ${Boost_VERSION}")
endif()
if(DOTNET_EXECUTABLE)
    message(STATUS "  .NET SDK: ${CMAKE_DOTNET_VERSION}")
endif()
message(STATUS "=============================")
message(STATUS "")
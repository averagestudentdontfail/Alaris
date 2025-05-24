# cmake/utils/Dependencies.cmake
# Dependency management for Alaris

function(find_system_dependencies)
    message(STATUS "Finding system dependencies...")

    # Required system packages
    find_package(PkgConfig REQUIRED)
    find_package(Threads REQUIRED)

    # Boost (required by QuantLib) - Try to find system installation first
    set(Boost_USE_STATIC_LIBS OFF)
    set(Boost_USE_MULTITHREADED ON)
    set(Boost_USE_STATIC_RUNTIME OFF)
    
    find_package(Boost 1.75.0 COMPONENTS
        system
        filesystem
        date_time
        thread
        program_options
        serialization
    )

    if(Boost_FOUND)
        message(STATUS "Found system Boost: ${Boost_VERSION}")
        set(BOOST_TARGET Boost::boost PARENT_SCOPE)
    else()
        message(STATUS "System Boost not found, will use built-in version")
        set(BOOST_TARGET "" PARENT_SCOPE)
    endif()

    # System libraries for shared memory and real-time
    if(UNIX)
        find_library(RT_LIB rt)
        find_library(PTHREAD_LIB pthread REQUIRED)
        find_library(MATH_LIB m)
        
        set(SYSTEM_LIBS)
        if(RT_LIB)
            list(APPEND SYSTEM_LIBS ${RT_LIB})
        endif()
        if(MATH_LIB)
            list(APPEND SYSTEM_LIBS ${MATH_LIB})
        endif()
        list(APPEND SYSTEM_LIBS ${PTHREAD_LIB})
        
        set(SYSTEM_LIBS ${SYSTEM_LIBS} PARENT_SCOPE)
        message(STATUS "Found system libraries: ${SYSTEM_LIBS}")
    else()
        set(SYSTEM_LIBS "" PARENT_SCOPE)
    endif()

    # Optional dependencies
    find_package(OpenMP)
    if(OpenMP_CXX_FOUND)
        message(STATUS "Found OpenMP: ${OpenMP_CXX_VERSION}")
        set(OPENMP_TARGET OpenMP::OpenMP_CXX PARENT_SCOPE)
    else()
        set(OPENMP_TARGET "" PARENT_SCOPE)
    endif()

    # Development tools
    find_program(CCACHE_PROGRAM ccache)
    if(CCACHE_PROGRAM)
        message(STATUS "Found ccache: ${CCACHE_PROGRAM}")
        set(CMAKE_CXX_COMPILER_LAUNCHER ${CCACHE_PROGRAM} PARENT_SCOPE)
    endif()

    find_program(NINJA_PROGRAM ninja)
    if(NINJA_PROGRAM)
        message(STATUS "Found ninja: ${NINJA_PROGRAM}")
    endif()
endfunction()

function(configure_external_dependencies)
    message(STATUS "Configuring external dependencies...")

    # Set external build options
    set(BUILD_SHARED_LIBS OFF CACHE BOOL "Build shared libraries")
    
    # QuantLib configuration
    configure_quantlib()
    
    # yaml-cpp configuration
    configure_yaml_cpp()
    
    # Lean configuration (if needed)
    configure_lean()
endfunction()

function(configure_quantlib)
    message(STATUS "Configuring QuantLib...")

    # QuantLib build options
    set(QL_BUILD_BENCHMARK OFF CACHE BOOL "Build QuantLib benchmark")
    set(QL_BUILD_EXAMPLES OFF CACHE BOOL "Build QuantLib examples")
    set(QL_BUILD_TEST_SUITE OFF CACHE BOOL "Build QuantLib test suite")
    set(QL_ENABLE_SESSIONS OFF CACHE BOOL "Enable QuantLib sessions")
    set(QL_ENABLE_THREAD_SAFE_OBSERVER_PATTERN OFF CACHE BOOL "Thread-safe observer")
    set(QL_ENABLE_PARALLEL_UNIT_TEST_RUNNER OFF CACHE BOOL "Parallel unit tests")
    set(QL_HIGH_RESOLUTION_DATE ON CACHE BOOL "High resolution dates")
    set(QL_USE_STD_SHARED_PTR ON CACHE BOOL "Use std::shared_ptr")
    
    # Performance optimizations for deterministic trading
    set(QL_ERROR_FUNCTIONS OFF CACHE BOOL "Disable error functions for performance")
    set(QL_ERROR_LINES OFF CACHE BOOL "Disable error line info for performance")

    # Add QuantLib subdirectory if it exists
    if(EXISTS "${CMAKE_SOURCE_DIR}/external/quant/CMakeLists.txt")
        add_subdirectory(external/quant EXCLUDE_FROM_ALL)
        
        # Find the QuantLib target
        set(QUANTLIB_TARGET_CANDIDATES "QuantLib" "quantlib" "ql")
        set(QUANTLIB_TARGET "")
        
        foreach(target ${QUANTLIB_TARGET_CANDIDATES})
            if(TARGET ${target})
                set(QUANTLIB_TARGET ${target} PARENT_SCOPE)
                message(STATUS "Found QuantLib target: ${target}")
                break()
            endif()
        endforeach()
        
        if(NOT QUANTLIB_TARGET)
            message(FATAL_ERROR "QuantLib target not found after building")
        endif()

        # Set QuantLib include directories
        set(QuantLib_INCLUDE_DIRS 
            "${CMAKE_SOURCE_DIR}/external/quant"
            "${CMAKE_BINARY_DIR}/external/quant"
            PARENT_SCOPE
        )

        # Global QuantLib compile definitions for deterministic execution
        add_compile_definitions(
            QL_ENABLE_PARALLEL_UNIT_TEST_RUNNER=0
            QL_ENABLE_THREAD_SAFE_OBSERVER_PATTERN=0
            QL_HIGH_RESOLUTION_DATE
            QL_USE_STD_SHARED_PTR
            $<$<CONFIG:Release>:QL_ERROR_FUNCTIONS=0>
            $<$<CONFIG:Release>:QL_ERROR_LINES=0>
        )
    else()
        message(FATAL_ERROR "QuantLib submodule not found. Run: git submodule update --init")
    endif()
endfunction()

function(configure_yaml_cpp)
    message(STATUS "Configuring yaml-cpp...")

    # yaml-cpp build options
    set(YAML_CPP_BUILD_TESTS OFF CACHE BOOL "Build yaml-cpp tests")
    set(YAML_CPP_BUILD_TOOLS OFF CACHE BOOL "Build yaml-cpp tools")
    set(YAML_CPP_BUILD_CONTRIB OFF CACHE BOOL "Build yaml-cpp contrib")
    set(YAML_CPP_INSTALL OFF CACHE BOOL "Install yaml-cpp")

    if(EXISTS "${CMAKE_SOURCE_DIR}/external/yaml-cpp/CMakeLists.txt")
        add_subdirectory(external/yaml-cpp EXCLUDE_FROM_ALL)
        
        set(YAML_CPP_TARGET yaml-cpp PARENT_SCOPE)
        set(yaml-cpp_INCLUDE_DIRS 
            "${CMAKE_SOURCE_DIR}/external/yaml-cpp/include"
            PARENT_SCOPE
        )
        message(STATUS "Configured yaml-cpp")
    else()
        message(FATAL_ERROR "yaml-cpp submodule not found. Run: git submodule update --init")
    endif()
endfunction()

function(configure_lean)
    # Lean is a .NET project, so we don't build it with CMake
    # Just verify it exists for documentation purposes
    if(EXISTS "${CMAKE_SOURCE_DIR}/external/lean")
        message(STATUS "Lean submodule found (managed separately via .NET)")
        set(LEAN_AVAILABLE TRUE PARENT_SCOPE)
    else()
        message(STATUS "Lean submodule not found (optional for C++ components)")
        set(LEAN_AVAILABLE FALSE PARENT_SCOPE)
    endif()
endfunction()

# Function to create imported targets for system libraries
function(create_system_targets)
    if(SYSTEM_LIBS)
        add_library(System::Libraries INTERFACE IMPORTED)
        target_link_libraries(System::Libraries INTERFACE ${SYSTEM_LIBS})
    endif()
endfunction()
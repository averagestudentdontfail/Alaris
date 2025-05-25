# cmake/utils/DependencyManager.cmake
# Modern dependency management for Alaris

# Function to manage external dependencies
function(manage_dependencies)
    # Check for required submodules
    if(NOT EXISTS "${CMAKE_SOURCE_DIR}/external/quant/CMakeLists.txt")
        message(FATAL_ERROR "QuantLib submodule not found. Please run: git submodule update --init --recursive")
    endif()
    
    if(NOT EXISTS "${CMAKE_SOURCE_DIR}/external/yaml-cpp/CMakeLists.txt")
        message(FATAL_ERROR "yaml-cpp submodule not found. Please run: git submodule update --init --recursive")
    endif()
    
    # Configure QuantLib options
    set(QL_BUILD_BENCHMARK OFF CACHE BOOL "Build QuantLib benchmark")
    set(QL_BUILD_EXAMPLES OFF CACHE BOOL "Build QuantLib examples")
    set(QL_BUILD_TEST_SUITE OFF CACHE BOOL "Build QuantLib test suite")
    set(QL_ENABLE_SESSIONS OFF CACHE BOOL "Enable QuantLib sessions")
    set(QL_ENABLE_THREAD_SAFE_OBSERVER_PATTERN OFF CACHE BOOL "Thread-safe observer")
    set(QL_ENABLE_PARALLEL_UNIT_TEST_RUNNER OFF CACHE BOOL "Parallel unit tests")
    set(QL_HIGH_RESOLUTION_DATE ON CACHE BOOL "High resolution dates")
    set(QL_USE_STD_SHARED_PTR ON CACHE BOOL "Use std::shared_ptr")
    
    # Configure yaml-cpp options
    set(YAML_CPP_BUILD_TESTS OFF CACHE BOOL "Build yaml-cpp tests")
    set(YAML_CPP_BUILD_TOOLS OFF CACHE BOOL "Build yaml-cpp tools")
    set(YAML_CPP_BUILD_CONTRIB OFF CACHE BOOL "Build yaml-cpp contrib")
    set(YAML_CPP_INSTALL OFF CACHE BOOL "Install yaml-cpp")
    
    # Add QuantLib submodule
    add_subdirectory(${CMAKE_SOURCE_DIR}/external/quant)
    set(QUANTLIB_TARGET QuantLib PARENT_SCOPE)
    set(QuantLib_INCLUDE_DIRS 
        "${CMAKE_SOURCE_DIR}/external/quant"
        "${CMAKE_BINARY_DIR}/external/quant"
        PARENT_SCOPE
    )
    
    # Add yaml-cpp submodule
    add_subdirectory(${CMAKE_SOURCE_DIR}/external/yaml-cpp)
    set(YAML_CPP_TARGET yaml-cpp PARENT_SCOPE)
    set(yaml-cpp_INCLUDE_DIRS 
        "${CMAKE_SOURCE_DIR}/external/yaml-cpp/include"
        PARENT_SCOPE
    )
    
    # Create imported targets for system libraries
    if(UNIX)
        find_library(RT_LIB rt)
        find_library(PTHREAD_LIB pthread REQUIRED)
        find_library(MATH_LIB m)
        
        if(RT_LIB)
            add_library(System::RT UNKNOWN IMPORTED)
            set_target_properties(System::RT PROPERTIES IMPORTED_LOCATION ${RT_LIB})
        endif()
        
        if(MATH_LIB)
            add_library(System::Math UNKNOWN IMPORTED)
            set_target_properties(System::Math PROPERTIES IMPORTED_LOCATION ${MATH_LIB})
        endif()
        
        add_library(System::PThread UNKNOWN IMPORTED)
        set_target_properties(System::PThread PROPERTIES IMPORTED_LOCATION ${PTHREAD_LIB})
    endif()
    
    # Find OpenMP
    find_package(OpenMP)
    if(OpenMP_CXX_FOUND)
        if(NOT TARGET OpenMP::OpenMP_CXX)
            add_library(OpenMP::OpenMP_CXX INTERFACE IMPORTED)
            set_target_properties(OpenMP::OpenMP_CXX PROPERTIES
                INTERFACE_COMPILE_OPTIONS "${OpenMP_CXX_FLAGS}"
                INTERFACE_LINK_LIBRARIES "${OpenMP_CXX_LIBRARIES}"
            )
        endif()
    endif()
    
    # Create imported target for Boost
    if(Boost_FOUND)
        if(NOT TARGET Boost::boost)
            add_library(Boost::boost INTERFACE IMPORTED)
            set_target_properties(Boost::boost PROPERTIES
                INTERFACE_INCLUDE_DIRECTORIES "${Boost_INCLUDE_DIRS}"
                INTERFACE_LINK_LIBRARIES "${Boost_LIBRARIES}"
            )
        endif()
    endif()
endfunction()

# Function to create a dependency graph
function(create_dependency_graph)
    if(NOT CMAKE_DOT)
        find_program(CMAKE_DOT dot)
    endif()
    
    if(CMAKE_DOT)
        file(WRITE ${CMAKE_BINARY_DIR}/dependencies.dot
            "digraph dependencies {\n"
            "  node [shape=box];\n"
        )
        
        get_property(targets DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR} PROPERTY BUILDSYSTEM_TARGETS)
        foreach(target ${targets})
            get_target_property(type ${target} TYPE)
            if(type STREQUAL "INTERFACE_LIBRARY")
                continue()
            endif()
            
            get_target_property(deps ${target} LINK_LIBRARIES)
            if(deps)
                foreach(dep ${deps})
                    if(TARGET ${dep})
                        file(APPEND ${CMAKE_BINARY_DIR}/dependencies.dot
                            "  \"${target}\" -> \"${dep}\";\n"
                        )
                    endif()
                endforeach()
            endif()
        endforeach()
        
        file(APPEND ${CMAKE_BINARY_DIR}/dependencies.dot "}\n")
        
        execute_process(
            COMMAND ${CMAKE_DOT} -Tpng ${CMAKE_BINARY_DIR}/dependencies.dot -o ${CMAKE_BINARY_DIR}/dependencies.png
            RESULT_VARIABLE result
        )
        
        if(result EQUAL 0)
            message(STATUS "Dependency graph generated: ${CMAKE_BINARY_DIR}/dependencies.png")
        else()
            message(WARNING "Failed to generate dependency graph")
        endif()
    else()
        message(WARNING "Graphviz not found, skipping dependency graph generation")
    endif()
endfunction() 
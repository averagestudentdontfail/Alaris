# cmake/utils/DependencyManager.cmake
# Modern dependency management for Alaris

# Function to manage external dependencies
function(manage_dependencies)
    message(STATUS "Managing external dependencies via external/CMakeLists.txt...")

    # Check for required submodule directories first at the 'external' level
    if(NOT EXISTS "${CMAKE_SOURCE_DIR}/external/quant")
        message(FATAL_ERROR "QuantLib submodule directory 'external/quant' not found. Please run: git submodule update --init --recursive")
    endif()
    if(NOT EXISTS "${CMAKE_SOURCE_DIR}/external/yaml-cpp")
        message(FATAL_ERROR "yaml-cpp submodule directory 'external/yaml-cpp' not found. Please run: git submodule update --init --recursive")
    endif()
    
    # Add the main 'external' directory, which should contain its own CMakeLists.txt
    # This external/CMakeLists.txt (File 190 from your uploads) will then handle
    # adding 'external/quant' and 'external/yaml-cpp' as subdirectories,
    # building them, and setting QUANTLIB_TARGET and YAML_CPP_TARGET in PARENT_SCOPE.
    if(EXISTS "${CMAKE_SOURCE_DIR}/external/CMakeLists.txt")
        add_subdirectory(${CMAKE_SOURCE_DIR}/external)
    else()
        message(FATAL_ERROR "Main CMakeLists.txt for external dependencies (external/CMakeLists.txt) not found!")
    endif()

    # After add_subdirectory(${CMAKE_SOURCE_DIR}/external) returns,
    # QUANTLIB_TARGET and YAML_CPP_TARGET should be set in this scope (due to PARENT_SCOPE in external/CMakeLists.txt).
    # We should verify they are set.
    if(NOT DEFINED QUANTLIB_TARGET OR NOT TARGET ${QUANTLIB_TARGET})
        message(FATAL_ERROR "QUANTLIB_TARGET was not correctly set by external/CMakeLists.txt. Expected target name for QuantLib.")
    else()
        message(STATUS "QUANTLIB_TARGET is set to: ${QUANTLIB_TARGET}")
    endif()

    if(NOT DEFINED YAML_CPP_TARGET OR NOT TARGET ${YAML_CPP_TARGET})
        message(FATAL_ERROR "YAML_CPP_TARGET was not correctly set by external/CMakeLists.txt. Expected target name for yaml-cpp.")
    else()
        message(STATUS "YAML_CPP_TARGET is set to: ${YAML_CPP_TARGET}")
    endif()

    # The rest of your original manage_dependencies function (system libs, OpenMP, etc.) can follow.
    # This part is for system-level dependencies, not those built from external/.
    # This seems to be from your other `Dependencies.txt` (File 83) under `find_system_dependencies`.
    # Let's integrate that logic here.

    message(STATUS "Finding system-level dependencies...")

    find_package(PkgConfig QUIET) # PkgConfig is often helpful but not always critical
    find_package(Threads REQUIRED)

    # Boost (primarily a QuantLib dependency, QL should handle finding it or using its internal one)
    # QL_EXTERNAL_BOOST in external/CMakeLists.txt controls this.
    # We can check if system Boost was found for information.
    find_package(Boost 1.75.0 QUIET COMPONENTS system filesystem date_time thread program_options serialization)
    if(Boost_FOUND)
        message(STATUS "System Boost found: ${Boost_VERSION_STRING}. QuantLib will be configured to use it if QL_EXTERNAL_BOOST is ON.")
        # No need to set BOOST_TARGET here if QuantLib handles it.
    else()
        message(STATUS "System Boost not found. QuantLib will likely use its bundled/internal Boost.")
    endif()

    # System libraries for shared memory and real-time
    if(UNIX)
        find_library(RT_LIB rt)
        find_library(PTHREAD_LIB pthread) # REQUIRED is handled by Threads::Threads
        find_library(MATH_LIB m)
        
        set(LOCAL_SYSTEM_LIBS "") # Use a local variable
        if(RT_LIB)
            list(APPEND LOCAL_SYSTEM_LIBS ${RT_LIB})
        endif()
        if(MATH_LIB)
            list(APPEND LOCAL_SYSTEM_LIBS ${MATH_LIB})
        endif()
        # Threads::Threads interface target should be used for pthreads.
        # list(APPEND LOCAL_SYSTEM_LIBS ${PTHREAD_LIB})
        
        set(SYSTEM_LIBS ${LOCAL_SYSTEM_LIBS} PARENT_SCOPE) # Propagate if needed by other modules
        if(LOCAL_SYSTEM_LIBS)
            message(STATUS "Found other system libraries: ${LOCAL_SYSTEM_LIBS}")
        endif()
    endif()

    # Optional OpenMP
    find_package(OpenMP)
    if(OpenMP_CXX_FOUND)
        message(STATUS "Found OpenMP: ${OpenMP_CXX_VERSION}. Target: OpenMP::OpenMP_CXX")
        set(OPENMP_TARGET OpenMP::OpenMP_CXX PARENT_SCOPE) # Propagate if needed
    else()
        message(STATUS "OpenMP not found.")
    endif()

    # The following ALARIS_COMMON_LINK_LIBRARIES should be constructed carefully based on what's found
    # and what targets need. It's often better for individual Alaris library targets
    # in src/quantlib/CMakeLists.txt to link directly against ${QUANTLIB_TARGET}, ${YAML_CPP_TARGET}, Threads::Threads, etc.
    # For now, let's define it based on what we expect to be available.
    set(COMMON_LINK_DEPS "")
    list(APPEND COMMON_LINK_DEPS ${QUANTLIB_TARGET} ${YAML_CPP_TARGET} Threads::Threads)
    if(SYSTEM_LIBS) # If SYSTEM_LIBS was set and contains relevant libraries like rt, m
        list(APPEND COMMON_LINK_DEPS ${SYSTEM_LIBS})
    endif()
    if(OpenMP_CXX_FOUND AND TARGET OpenMP::OpenMP_CXX)
         list(APPEND COMMON_LINK_DEPS OpenMP::OpenMP_CXX) # If you intend to use OpenMP in Alaris code
    endif()
    set(ALARIS_COMMON_LINK_LIBRARIES ${COMMON_LINK_DEPS} PARENT_SCOPE)
    message(STATUS "ALARIS_COMMON_LINK_LIBRARIES set to: ${ALARIS_COMMON_LINK_LIBRARIES}")

endfunction()

# Function to create a dependency graph (from your DependencyManager.txt)
function(create_dependency_graph)
    # This function seems fine as provided by you previously.
    # Ensure CMAKE_DOT is found or Graphviz is installed.
    if(NOT CMAKE_DOT)
        find_program(CMAKE_DOT dot)
    endif()
    
    if(CMAKE_DOT)
        file(WRITE ${CMAKE_BINARY_DIR}/dependencies.dot
            "digraph dependencies {\n"
            "  node [shape=box];\n"
        )
        
        get_property(targets DIRECTORY ${CMAKE_SOURCE_DIR} PROPERTY BUILDSYSTEM_TARGETS)
        foreach(target ${targets})
            get_target_property(type ${target} TYPE)
            if(type STREQUAL "INTERFACE_LIBRARY" OR type STREQUAL "UTILITY") # Skip some types
                continue()
            endif()
            
            get_target_property(deps ${target} LINK_LIBRARIES)
            if(deps)
                foreach(dep ${deps})
                    if(TARGET ${dep})
                        get_target_property(deptype ${dep} TYPE)
                        if(NOT (deptype STREQUAL "INTERFACE_LIBRARY" OR deptype STREQUAL "UTILITY")) # Skip some types for deps too
                            file(APPEND ${CMAKE_BINARY_DIR}/dependencies.dot
                                "  \"${target}\" -> \"${dep}\";\n"
                            )
                        endif()
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
            message(WARNING "Failed to generate dependency graph. Dot command may have failed.")
        endif()
    else()
        message(WARNING "Graphviz (dot executable) not found. Skipping dependency graph generation.")
    endif()
endfunction()
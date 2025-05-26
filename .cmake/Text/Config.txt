# Alaris CMake Configuration
include(CMakeDependentOption)

# Platform-specific configuration
if(WIN32)
    set(ALARIS_PLATFORM "Windows")
    set(ALARIS_SHARED_LIB_EXT ".dll")
    set(ALARIS_STATIC_LIB_EXT ".lib")
    set(ALARIS_EXECUTABLE_EXT ".exe")
elseif(APPLE)
    set(ALARIS_PLATFORM "Darwin")
    set(ALARIS_SHARED_LIB_EXT ".dylib")
    set(ALARIS_STATIC_LIB_EXT ".a")
    set(ALARIS_EXECUTABLE_EXT "")
elseif(UNIX)
    set(ALARIS_PLATFORM "Linux")
    set(ALARIS_SHARED_LIB_EXT ".so")
    set(ALARIS_STATIC_LIB_EXT ".a")
    set(ALARIS_EXECUTABLE_EXT "")
else()
    set(ALARIS_PLATFORM "Unknown")
endif()

# Dependency configuration
set(ALARIS_REQUIRED_PACKAGES
    Boost
    # yaml-cpp # Removed: built from external/
    # QuantLib # Removed: built from external/
)

# Find and configure dependencies that are expected to be found as packages
# This loop will now only process packages remaining in ALARIS_REQUIRED_PACKAGES (e.g., Boost)
foreach(PACKAGE ${ALARIS_REQUIRED_PACKAGES})
    find_package(${PACKAGE} REQUIRED)
    if(NOT ${PACKAGE}_FOUND) # Should be redundant due to REQUIRED, but good for clarity
        message(FATAL_ERROR "${PACKAGE} not found. Please install it or ensure it's correctly pathed.")
    endif()
endforeach()

# Configure QuantLib (expected to be provided by External.cmake)
if(DEFINED QUANTLIB_TARGET AND TARGET ${QUANTLIB_TARGET})
    set(ALARIS_QUANTLIB_INCLUDE_DIRS ${QuantLib_INCLUDE_DIRS})
    set(ALARIS_QUANTLIB_LIBRARIES ${QUANTLIB_TARGET}) # Use the target name for linking
    message(STATUS "Configured QuantLib from external build target: ${QUANTLIB_TARGET}")
else()
    message(FATAL_ERROR "QuantLib was NOT configured. "
                        "Expected QUANTLIB_TARGET (cmake variable value: '${QUANTLIB_TARGET}') "
                        "to be set by External.cmake processing. "
                        "Ensure External.cmake is included and correctly configured.")
endif()

# Configure Boost (from find_package)
if(Boost_FOUND)
    set(ALARIS_BOOST_INCLUDE_DIRS ${Boost_INCLUDE_DIRS})
    set(ALARIS_BOOST_LIBRARIES ${Boost_LIBRARIES}) # This might be a list of component targets or an imported target
    set(ALARIS_BOOST_DEFINITIONS ${Boost_DEFINITIONS})
    message(STATUS "Configured Boost from find_package.")
endif()

# Configure yaml-cpp (expected to be provided by add_subdirectory(external) )
# external/CMakeLists.txt should have set YAML_CPP_TARGET and yaml-cpp_INCLUDE_DIRS
if(TARGET ${YAML_CPP_TARGET} AND DEFINED yaml-cpp_INCLUDE_DIRS)
    set(ALARIS_YAMLCPP_INCLUDE_DIRS ${yaml-cpp_INCLUDE_DIRS})
    set(ALARIS_YAMLCPP_LIBRARIES ${YAML_CPP_TARGET}) # Use the target name for linking
    message(STATUS "Configured yaml-cpp from external build target: ${YAML_CPP_TARGET}")
else()
    message(FATAL_ERROR "yaml-cpp was NOT configured. "
                        "Expected YAML_CPP_TARGET (cmake variable value: '${YAML_CPP_TARGET}') and "
                        "yaml-cpp_INCLUDE_DIRS (cmake variable value: '${yaml-cpp_INCLUDE_DIRS}') "
                        "to be set by 'external/CMakeLists.txt' processing. "
                        "Ensure 'add_subdirectory(external)' is called in the root CMakeLists.txt "
                        "BEFORE including this Config.cmake file, and that external/CMakeLists.txt is correct.")
endif()

# Build configuration
set(ALARIS_BUILD_OPTIONS
    BUILD_TESTS
    BUILD_DOCS
    ENABLE_SANITIZERS
    ENABLE_COVERAGE
    ALARIS_INSTALL_DEVELOPMENT
)

foreach(OPTION ${ALARIS_BUILD_OPTIONS})
    option(${OPTION} "Enable ${OPTION}" OFF)
endforeach()

# Compiler configuration
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    # Common flags
    set(ALARIS_COMMON_FLAGS
        -Wall
        -Wextra
        -Wpedantic
        -Werror=return-type
        -Werror=non-virtual-dtor
        -Werror=address
        -Werror=sequence-point
        -Werror=format-security
        -Werror=missing-braces
        -Werror=reorder
        # -Werror=return-type # Duplicate, removed
        -Werror=switch
        -Werror=uninitialized
        -Wno-unused-parameter
        -Wno-unused-variable
        -Wno-unused-function
    )

    # Debug flags
    set(ALARIS_DEBUG_FLAGS
        -g
        -O0
        -fno-omit-frame-pointer
        -fno-inline
        -fno-inline-functions
    )

    # Release flags
    set(ALARIS_RELEASE_FLAGS
        -O3
        -DNDEBUG
        -flto
        -fno-fat-lto-objects
    )

    # Set flags based on build type
    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        set(CMAKE_CXX_FLAGS_INIT "${ALARIS_COMMON_FLAGS} ${ALARIS_DEBUG_FLAGS}")
    else() # Release, RelWithDebInfo etc.
        set(CMAKE_CXX_FLAGS_INIT "${ALARIS_COMMON_FLAGS} ${ALARIS_RELEASE_FLAGS}")
    endif()
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS_INIT} ${CMAKE_CXX_FLAGS}" CACHE STRING "Flags used by the C++ compiler" FORCE)

endif()

# Sanitizer configuration
if(ENABLE_SANITIZERS AND CMAKE_BUILD_TYPE STREQUAL "Debug")
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        set(ALARIS_SANITIZER_FLAGS
            -fsanitize=address
            -fsanitize=undefined
            -fno-omit-frame-pointer # Already in debug, but explicit for sanitizers
        )
        # Append to existing flags rather than overwriting
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${ALARIS_SANITIZER_FLAGS}")
        set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} ${ALARIS_SANITIZER_FLAGS}")
        set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} ${ALARIS_SANITIZER_FLAGS}")
    endif()
endif()

# Coverage configuration
if(ENABLE_COVERAGE AND CMAKE_BUILD_TYPE STREQUAL "Debug")
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        # For GCC/gcov:
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fprofile-arcs -ftest-coverage")
        set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} -fprofile-arcs -ftest-coverage")
        set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} -fprofile-arcs -ftest-coverage")
        # For Clang/llvm-cov, you might use:
        # set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fprofile-instr-generate -fcoverage-mapping")
        # set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} -fprofile-instr-generate")
    endif()
endif()

# Export configuration
set(ALARIS_CONFIGURED TRUE CACHE INTERNAL "Alaris configuration complete" FORCE)
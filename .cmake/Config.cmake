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
    yaml-cpp
    QuantLib
)

# Find and configure dependencies
foreach(PACKAGE ${ALARIS_REQUIRED_PACKAGES})
    find_package(${PACKAGE} REQUIRED)
    if(NOT ${PACKAGE}_FOUND)
        message(FATAL_ERROR "${PACKAGE} not found. Please install it.")
    endif()
endforeach()

# Configure QuantLib
if(QuantLib_FOUND)
    set(ALARIS_QUANTLIB_INCLUDE_DIRS ${QuantLib_INCLUDE_DIRS})
    set(ALARIS_QUANTLIB_LIBRARIES ${QuantLib_LIBRARIES})
    set(ALARIS_QUANTLIB_DEFINITIONS ${QuantLib_DEFINITIONS})
endif()

# Configure Boost
if(Boost_FOUND)
    set(ALARIS_BOOST_INCLUDE_DIRS ${Boost_INCLUDE_DIRS})
    set(ALARIS_BOOST_LIBRARIES ${Boost_LIBRARIES})
    set(ALARIS_BOOST_DEFINITIONS ${Boost_DEFINITIONS})
endif()

# Configure yaml-cpp
if(yaml-cpp_FOUND)
    set(ALARIS_YAMLCPP_INCLUDE_DIRS ${YAML_CPP_INCLUDE_DIRS})
    set(ALARIS_YAMLCPP_LIBRARIES ${YAML_CPP_LIBRARIES})
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
        -Werror=return-type
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
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${ALARIS_COMMON_FLAGS} ${ALARIS_DEBUG_FLAGS}")
    else()
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${ALARIS_COMMON_FLAGS} ${ALARIS_RELEASE_FLAGS}")
    endif()
endif()

# Sanitizer configuration
if(ENABLE_SANITIZERS AND CMAKE_BUILD_TYPE STREQUAL "Debug")
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        set(ALARIS_SANITIZER_FLAGS
            -fsanitize=address
            -fsanitize=undefined
            -fno-omit-frame-pointer
        )
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${ALARIS_SANITIZER_FLAGS}")
        set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} ${ALARIS_SANITIZER_FLAGS}")
    endif()
endif()

# Coverage configuration
if(ENABLE_COVERAGE AND CMAKE_BUILD_TYPE STREQUAL "Debug")
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        set(ALARIS_COVERAGE_FLAGS
            --coverage
            -fprofile-arcs
            -ftest-coverage
        )
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${ALARIS_COVERAGE_FLAGS}")
        set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} ${ALARIS_COVERAGE_FLAGS}")
    endif()
endif()

# Export configuration
set(ALARIS_CONFIGURED TRUE CACHE INTERNAL "Alaris configuration complete" FORCE) 
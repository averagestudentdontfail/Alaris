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

# Dependency configuration - only check if variables are set, not if targets exist yet
# (targets will be created later during build process)

# Configure QuantLib (expected to be provided by External.cmake)
if(DEFINED QUANTLIB_TARGET AND QUANTLIB_TARGET)
    set(ALARIS_QUANTLIB_LIBRARIES ${QUANTLIB_TARGET}) # Use the target name for linking
    message(STATUS "Configured QuantLib target: ${QUANTLIB_TARGET}")
else()
    message(FATAL_ERROR "QuantLib was NOT configured. "
                        "Expected QUANTLIB_TARGET to be set by External.cmake processing. "
                        "Either install libquantlib0-dev or initialize git submodules.")
endif()

# Configure yaml-cpp (expected to be provided by External.cmake)
if(DEFINED YAML_CPP_TARGET AND YAML_CPP_TARGET)
    set(ALARIS_YAMLCPP_LIBRARIES ${YAML_CPP_TARGET}) # Use the target name for linking
    message(STATUS "Configured yaml-cpp target: ${YAML_CPP_TARGET}")
else()
    message(FATAL_ERROR "yaml-cpp was NOT configured. "
                        "Expected YAML_CPP_TARGET to be set by External.cmake processing. "
                        "Either install libyaml-cpp-dev or initialize git submodules.")
endif()

# Configure Boost (from find_package)
find_package(Boost QUIET)
if(Boost_FOUND)
    set(ALARIS_BOOST_INCLUDE_DIRS ${Boost_INCLUDE_DIRS})
    set(ALARIS_BOOST_LIBRARIES ${Boost_LIBRARIES})
    set(ALARIS_BOOST_DEFINITIONS ${Boost_DEFINITIONS})
    message(STATUS "Configured Boost from find_package: ${Boost_VERSION}")
endif()

# Build configuration options
set(ALARIS_BUILD_OPTIONS
    BUILD_DOCS
    ENABLE_SANITIZERS
    ENABLE_COVERAGE
    ALARIS_INSTALL_DEVELOPMENT
)

# Set default values for build options
option(BUILD_DOCS "Build documentation" OFF)
option(ENABLE_SANITIZERS "Enable sanitizers (for Debug builds)" OFF)
option(ENABLE_COVERAGE "Enable code coverage (for Debug builds)" OFF)
option(ALARIS_INSTALL_DEVELOPMENT "Install development files (headers, etc.)" ON)

# Compiler configuration - FIXED: Use string concatenation instead of list operations
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    # Common flags as a single string
    set(ALARIS_COMMON_FLAGS_STR 
        "-Wall -Wextra -Wpedantic -Werror=return-type -Werror=non-virtual-dtor -Werror=address -Werror=sequence-point -Werror=format-security -Werror=missing-braces -Werror=reorder -Werror=switch -Werror=uninitialized -Wno-unused-parameter -Wno-unused-variable -Wno-unused-function")

    # Debug flags
    set(ALARIS_DEBUG_FLAGS_STR "-g -O0 -fno-omit-frame-pointer -fno-inline -fno-inline-functions")

    # Release flags
    set(ALARIS_RELEASE_FLAGS_STR "-O3 -DNDEBUG -flto -fno-fat-lto-objects")

    # Apply flags based on build type - FIXED: Use string concatenation
    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${ALARIS_COMMON_FLAGS_STR} ${ALARIS_DEBUG_FLAGS_STR}")
    else() # Release, RelWithDebInfo etc.
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${ALARIS_COMMON_FLAGS_STR} ${ALARIS_RELEASE_FLAGS_STR}")
    endif()
endif()

# Sanitizer configuration
if(ENABLE_SANITIZERS AND CMAKE_BUILD_TYPE STREQUAL "Debug")
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        set(ALARIS_SANITIZER_FLAGS_STR "-fsanitize=address -fsanitize=undefined -fno-omit-frame-pointer")
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${ALARIS_SANITIZER_FLAGS_STR}")
        set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} ${ALARIS_SANITIZER_FLAGS_STR}")
        set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} ${ALARIS_SANITIZER_FLAGS_STR}")
    endif()
endif()

# Coverage configuration
if(ENABLE_COVERAGE AND CMAKE_BUILD_TYPE STREQUAL "Debug")
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        # For GCC/gcov:
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fprofile-arcs -ftest-coverage")
        set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} -fprofile-arcs -ftest-coverage")
        set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} -fprofile-arcs -ftest-coverage")
    endif()
endif()

# Export configuration
set(ALARIS_CONFIGURED TRUE CACHE INTERNAL "Alaris configuration complete" FORCE)

message(STATUS "Config.cmake: Configuration applied successfully")
# Initialize variables
set(QUANTLIB_TARGET "")
set(YAML_CPP_TARGET "")

# First, try to find dependencies as system packages
option(USE_SYSTEM_QUANTLIB "Use system-installed QuantLib" ON)
option(USE_SYSTEM_YAMLCPP "Use system-installed yaml-cpp" ON)

# Function to suppress warnings from external targets
function(suppress_external_warnings TARGET_NAME)
    if(TARGET ${TARGET_NAME})
        # Get the current interface include directories
        get_target_property(INCLUDE_DIRS ${TARGET_NAME} INTERFACE_INCLUDE_DIRECTORIES)
        if(INCLUDE_DIRS)
            # Clear the current directories and re-add them as SYSTEM
            set_target_properties(${TARGET_NAME} PROPERTIES INTERFACE_INCLUDE_DIRECTORIES "")
            target_include_directories(${TARGET_NAME} SYSTEM INTERFACE ${INCLUDE_DIRS})
        endif()
        
        # Suppress warnings for this target
        if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
            target_compile_options(${TARGET_NAME} INTERFACE 
                -Wno-unknown-pragmas 
                -Wno-extra 
                -Wno-pedantic
                -Wno-unused-parameter
                -Wno-unused-variable
                -Wno-unused-function
                -Wno-deprecated-declarations
            )
        endif()
    endif()
endfunction()

# ========================================
# QuantLib Configuration
# ========================================
if(USE_SYSTEM_QUANTLIB)
    # Try multiple approaches to find QuantLib
    find_package(QuantLib QUIET)
    
    if(NOT QuantLib_FOUND)
        # Try pkg-config approach
        find_package(PkgConfig QUIET)
        if(PKG_CONFIG_FOUND)
            pkg_check_modules(QUANTLIB QUIET quantlib)
            if(QUANTLIB_FOUND)
                # Create imported target from pkg-config
                add_library(QuantLib::QuantLib INTERFACE IMPORTED)
                target_include_directories(QuantLib::QuantLib SYSTEM INTERFACE ${QUANTLIB_INCLUDE_DIRS})
                target_link_libraries(QuantLib::QuantLib INTERFACE ${QUANTLIB_LIBRARIES})
                target_compile_options(QuantLib::QuantLib INTERFACE ${QUANTLIB_CFLAGS_OTHER})
                set(QuantLib_FOUND TRUE)
                set(QUANTLIB_TARGET QuantLib::QuantLib)
                suppress_external_warnings(QuantLib::QuantLib)
                message(STATUS "Using system QuantLib via pkg-config: ${QUANTLIB_VERSION}")
            endif()
        endif()
    endif()
    
    if(NOT QuantLib_FOUND)
        # Try direct library search
        find_path(QUANTLIB_INCLUDE_DIR 
            NAMES ql/quantlib.hpp
            PATHS /usr/include /usr/local/include /opt/local/include
            PATH_SUFFIXES quantlib QuantLib
        )
        
        find_library(QUANTLIB_LIBRARY
            NAMES QuantLib quantlib
            PATHS /usr/lib /usr/local/lib /opt/local/lib
            PATH_SUFFIXES x86_64-linux-gnu lib64
        )
        
        if(QUANTLIB_INCLUDE_DIR AND QUANTLIB_LIBRARY)
            add_library(QuantLib::QuantLib INTERFACE IMPORTED)
            target_include_directories(QuantLib::QuantLib SYSTEM INTERFACE ${QUANTLIB_INCLUDE_DIR})
            target_link_libraries(QuantLib::QuantLib INTERFACE ${QUANTLIB_LIBRARY})
            set(QuantLib_FOUND TRUE)
            set(QUANTLIB_TARGET QuantLib::QuantLib)
            suppress_external_warnings(QuantLib::QuantLib)
            message(STATUS "Using system QuantLib via direct search: ${QUANTLIB_INCLUDE_DIR}")
        endif()
    endif()
    
    if(QuantLib_FOUND)
        set(QUANTLIB_TARGET QuantLib::QuantLib)
        suppress_external_warnings(QuantLib::QuantLib)
        message(STATUS "Using system QuantLib")
    else()
        message(STATUS "System QuantLib not found, will attempt to build from source")
        set(USE_SYSTEM_QUANTLIB OFF)
    endif()
endif()

if(NOT USE_SYSTEM_QUANTLIB)
    # Check if external directories exist and add them directly
    if(EXISTS "${CMAKE_SOURCE_DIR}/external/QuantLib/CMakeLists.txt")
        message(STATUS "Building QuantLib from external/QuantLib")
        
        # Configure QuantLib build options
        set(QL_BUILD_EXAMPLES OFF CACHE BOOL "Don't build QuantLib examples")
        set(QL_BUILD_TEST_SUITE OFF CACHE BOOL "Don't build QuantLib tests")
        set(QL_BUILD_BENCHMARK OFF CACHE BOOL "Don't build QuantLib benchmarks")
        set(QL_USE_STD_SHARED_PTR ON CACHE BOOL "Use std::shared_ptr")
        
        add_subdirectory(${CMAKE_SOURCE_DIR}/external/QuantLib EXCLUDE_FROM_ALL)
        set(QUANTLIB_TARGET QuantLib)
        
        # Add QuantLib include directory as SYSTEM to suppress warnings
        target_include_directories(QuantLib SYSTEM PUBLIC
            ${CMAKE_SOURCE_DIR}/external/QuantLib
        )
        
        # Apply warning suppression
        suppress_external_warnings(QuantLib)
        
    elseif(EXISTS "${CMAKE_SOURCE_DIR}/external/quant/CMakeLists.txt")
        message(STATUS "Building QuantLib from external/quant")
        add_subdirectory(${CMAKE_SOURCE_DIR}/external/quant EXCLUDE_FROM_ALL)
        set(QUANTLIB_TARGET ql_library)
        
        # Apply warning suppression to the ql_library target
        suppress_external_warnings(ql_library)
        
    else()
        # Provide helpful installation instructions
        message(STATUS "")
        message(STATUS "=== QuantLib NOT FOUND ===")
        message(STATUS "QuantLib is required but not found. Please choose one of these options:")
        message(STATUS "")
        message(STATUS "Option 1 - Install system QuantLib (Ubuntu/Debian):")
        message(STATUS "  sudo apt update")
        message(STATUS "  sudo apt install libquantlib0-dev")
        message(STATUS "")
        message(STATUS "Option 2 - Install system QuantLib (Fedora/RHEL):")
        message(STATUS "  sudo dnf install QuantLib-devel")
        message(STATUS "")
        message(STATUS "Option 3 - Build from source via git submodules:")
        message(STATUS "  git submodule add https://github.com/lballabio/QuantLib.git external/QuantLib")
        message(STATUS "  git submodule update --init --recursive")
        message(STATUS "")
        message(STATUS "Option 4 - Manual build from source:")
        message(STATUS "  cd /tmp")
        message(STATUS "  git clone https://github.com/lballabio/QuantLib.git")
        message(STATUS "  cd QuantLib")
        message(STATUS "  cmake -S . -B build -DCMAKE_BUILD_TYPE=Release")
        message(STATUS "  cmake --build build --parallel $(nproc)")
        message(STATUS "  sudo cmake --install build")
        message(STATUS "")
        message(STATUS "Then reconfigure this project:")
        message(STATUS "  cmake -S . -B build")
        message(STATUS "")
        
        message(FATAL_ERROR "QuantLib configuration failed")
    endif()
endif()

# ========================================
# yaml-cpp Configuration  
# ========================================
if(USE_SYSTEM_YAMLCPP)
    find_package(yaml-cpp QUIET)
    if(yaml-cpp_FOUND)
        set(YAML_CPP_TARGET yaml-cpp::yaml-cpp)
        get_target_property(yaml-cpp_INCLUDE_DIRS yaml-cpp::yaml-cpp INTERFACE_INCLUDE_DIRECTORIES)
        suppress_external_warnings(yaml-cpp::yaml-cpp)
        message(STATUS "Using system yaml-cpp: ${yaml-cpp_VERSION}")
    else()
        # Try using PkgConfig as fallback (common on older Ubuntu versions)
        find_package(PkgConfig QUIET)
        if(PKG_CONFIG_FOUND)
            pkg_check_modules(YAML_CPP QUIET yaml-cpp)
            if(YAML_CPP_FOUND)
                add_library(yaml-cpp::yaml-cpp INTERFACE IMPORTED)
                target_include_directories(yaml-cpp::yaml-cpp SYSTEM INTERFACE ${YAML_CPP_INCLUDE_DIRS})
                target_link_libraries(yaml-cpp::yaml-cpp INTERFACE ${YAML_CPP_LIBRARIES})
                set(YAML_CPP_TARGET yaml-cpp::yaml-cpp)
                set(yaml-cpp_INCLUDE_DIRS ${YAML_CPP_INCLUDE_DIRS})
                suppress_external_warnings(yaml-cpp::yaml-cpp)
                message(STATUS "Using system yaml-cpp via PkgConfig: ${YAML_CPP_VERSION}")
            else()
                message(STATUS "System yaml-cpp not found, will attempt to build from source")
                set(USE_SYSTEM_YAMLCPP OFF)
            endif()
        else()
            message(STATUS "System yaml-cpp not found, will attempt to build from source")
            set(USE_SYSTEM_YAMLCPP OFF)
        endif()
    endif()
endif()

if(NOT USE_SYSTEM_YAMLCPP)
    # Check if external/yaml-cpp exists and add it directly
    if(EXISTS "${CMAKE_SOURCE_DIR}/external/yaml-cpp/CMakeLists.txt")
        message(STATUS "Building yaml-cpp from external/yaml-cpp")
        
        # Configure yaml-cpp options before adding subdirectory
        set(YAML_CPP_BUILD_TESTS OFF CACHE BOOL "Disable yaml-cpp tests")
        set(YAML_CPP_BUILD_TOOLS OFF CACHE BOOL "Disable yaml-cpp tools")
        set(YAML_CPP_BUILD_CONTRIB OFF CACHE BOOL "Disable yaml-cpp contrib")
        
        add_subdirectory(${CMAKE_SOURCE_DIR}/external/yaml-cpp EXCLUDE_FROM_ALL)
        set(YAML_CPP_TARGET yaml-cpp)
        set(yaml-cpp_INCLUDE_DIRS ${CMAKE_SOURCE_DIR}/external/yaml-cpp/include)
        
        # Apply warning suppression to yaml-cpp
        suppress_external_warnings(yaml-cpp)
    else()
        message(STATUS "")
        message(STATUS "=== yaml-cpp NOT FOUND ===")
        message(STATUS "yaml-cpp is required but not found. Please choose one of these options:")
        message(STATUS "")
        message(STATUS "Option 1 - Install system yaml-cpp (Ubuntu/Debian):")
        message(STATUS "  sudo apt update")
        message(STATUS "  sudo apt install libyaml-cpp-dev")
        message(STATUS "")
        message(STATUS "Option 2 - Build from source via git submodules:")
        message(STATUS "  git submodule add https://github.com/jbeder/yaml-cpp.git external/yaml-cpp")
        message(STATUS "  git submodule update --init --recursive")
        message(STATUS "")
        
        message(FATAL_ERROR "yaml-cpp configuration failed")
    endif()
endif()

# ========================================
# Lean (C#) Configuration
# ========================================
if(EXISTS "${CMAKE_SOURCE_DIR}/external/lean")
    message(STATUS "Found Lean submodule at external/lean")
    # Note: Lean is primarily C#, so we don't add_subdirectory here
    # This will be handled by the .NET build process
    set(LEAN_AVAILABLE TRUE)
else()
    message(STATUS "Lean submodule not found at external/lean")
    set(LEAN_AVAILABLE FALSE)
endif()

# ========================================
# .NET SDK Detection
# ========================================
find_program(DOTNET_EXECUTABLE dotnet QUIET)
if(DOTNET_EXECUTABLE)
    execute_process(
        COMMAND ${DOTNET_EXECUTABLE} --version
        OUTPUT_VARIABLE DOTNET_VERSION
        OUTPUT_STRIP_TRAILING_WHITESPACE
        ERROR_QUIET
    )
    set(CMAKE_DOTNET_VERSION ${DOTNET_VERSION} CACHE STRING ".NET SDK version")
    message(STATUS ".NET SDK found: ${DOTNET_VERSION}")
else()
    message(WARNING ".NET SDK not found - C# components will not be built")
endif()

# ========================================
# Validation
# ========================================
# Validate that required target variables are set
if(NOT QUANTLIB_TARGET)
    message(FATAL_ERROR "QuantLib configuration failed - see instructions above")
endif()

if(NOT YAML_CPP_TARGET)
    message(FATAL_ERROR "yaml-cpp configuration failed - see instructions above")
endif()

# ========================================
# Summary
# ========================================
message(STATUS "")
message(STATUS "=== External Dependencies Summary ===")
message(STATUS "  QuantLib Target: ${QUANTLIB_TARGET}")
message(STATUS "  yaml-cpp Target: ${YAML_CPP_TARGET}")
if(DOTNET_EXECUTABLE)
    message(STATUS "  .NET SDK: ${CMAKE_DOTNET_VERSION}")
    if(LEAN_AVAILABLE)
        message(STATUS "  Lean: Available at external/lean")
    else()
        message(STATUS "  Lean: Submodule not initialized")
    endif()
else()
    message(STATUS "  .NET SDK: Not found")
endif()
message(STATUS "=====================================")
message(STATUS "")
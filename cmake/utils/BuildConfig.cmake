# cmake/utils/BuildConfig.cmake
# Build configuration for Alaris

# Function to configure build options
function(configure_build_options)
    # Build type configuration
    if(NOT CMAKE_BUILD_TYPE)
        set(CMAKE_BUILD_TYPE Release CACHE STRING "Build type" FORCE)
        set_property(CACHE CMAKE_BUILD_TYPE PROPERTY STRINGS 
            "Debug" "Release" "RelWithDebInfo" "MinSizeRel" "DebugOpt" "Profile")
    endif()
    
    # Compiler flags configuration
    set(CMAKE_CXX_FLAGS_DEBUG "-O0 -g -DDEBUG -fsanitize=address -fsanitize=undefined" CACHE STRING "Debug flags" FORCE)
    set(CMAKE_CXX_FLAGS_RELEASE "-O3 -DNDEBUG -march=native -mtune=native -flto" CACHE STRING "Release flags" FORCE)
    set(CMAKE_CXX_FLAGS_RELWITHDEBINFO "-O2 -g -DNDEBUG" CACHE STRING "RelWithDebInfo flags" FORCE)
    set(CMAKE_CXX_FLAGS_MINSIZEREL "-Os -DNDEBUG" CACHE STRING "MinSizeRel flags" FORCE)
    set(CMAKE_CXX_FLAGS_DEBUGOPT "-O1 -g -DDEBUG" CACHE STRING "DebugOpt flags" FORCE)
    set(CMAKE_CXX_FLAGS_PROFILE "-O2 -g -DNDEBUG -pg" CACHE STRING "Profile flags" FORCE)
    
    # Common flags for all compilers
    set(COMMON_CXX_FLAGS
        -Wall
        -Wextra
        -Wpedantic
        -Wno-unused-parameter
        -fno-omit-frame-pointer
    )
    
    # Compiler-specific configurations
    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        list(APPEND COMMON_CXX_FLAGS
            -Wconversion
            -Wsign-conversion
            -Wformat=2
            -Wuninitialized
        )
        
        if(CMAKE_CXX_COMPILER_ID STREQUAL "GNU")
            # GCC-specific flags
            if(CMAKE_CXX_COMPILER_VERSION VERSION_GREATER_EQUAL 9.0)
                list(APPEND COMMON_CXX_FLAGS -Wduplicated-cond -Wlogical-op)
            endif()
        elseif(CMAKE_CXX_COMPILER_ID STREQUAL "Clang")
            # Clang-specific flags
            list(APPEND COMMON_CXX_FLAGS -Wthread-safety)
        endif()
    elseif(CMAKE_CXX_COMPILER_ID STREQUAL "MSVC")
        # MSVC-specific flags
        list(APPEND COMMON_CXX_FLAGS
            /W4
            /permissive-
            /Zc:__cplusplus
        )
    endif()
    
    # Apply common flags
    foreach(flag ${COMMON_CXX_FLAGS})
        if(CMAKE_CXX_COMPILER_ID STREQUAL "MSVC")
            if(NOT flag MATCHES "^/")
                continue() # Skip non-MSVC flags
            endif()
        else()
            if(flag MATCHES "^/")
                continue() # Skip MSVC flags
            endif()
        endif()
        
        string(APPEND CMAKE_CXX_FLAGS " ${flag}")
    endforeach()
    
    # Set cache variables
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS}" CACHE STRING "C++ flags" FORCE)
    
    # Global compile definitions
    add_compile_definitions(
        $<$<CONFIG:Debug>:DEBUG_BUILD>
        $<$<CONFIG:Release>:RELEASE_BUILD>
        $<$<CONFIG:RelWithDebInfo>:RELWITHDEBINFO_BUILD>
        $<$<CONFIG:DebugOpt>:DEBUGOPT_BUILD>
        $<$<CONFIG:Profile>:PROFILE_BUILD>
    )
    
    # Platform-specific definitions
    if(WIN32)
        add_compile_definitions(
            WIN32_LEAN_AND_MEAN
            NOMINMAX
            _CRT_SECURE_NO_WARNINGS
        )
    elseif(UNIX)
        add_compile_definitions(_GNU_SOURCE)
    endif()
    
    # Configure parallel build
    if(NOT CMAKE_BUILD_PARALLEL_LEVEL)
        include(ProcessorCount)
        ProcessorCount(N)
        if(NOT N EQUAL 0)
            set(CMAKE_BUILD_PARALLEL_LEVEL ${N} CACHE STRING "Number of parallel build jobs" FORCE)
        endif()
    endif()
    
    # Configure distributed build
    if(NOT CMAKE_DISTCC)
        find_program(CMAKE_DISTCC distcc)
        if(CMAKE_DISTCC)
            set(CMAKE_CXX_COMPILER_LAUNCHER ${CMAKE_DISTCC} PARENT_SCOPE)
            message(STATUS "Using distcc for distributed builds")
        endif()
    endif()
    
    # Configure ccache
    if(NOT CMAKE_CCACHE)
        find_program(CMAKE_CCACHE ccache)
        if(CMAKE_CCACHE)
            set(CMAKE_CXX_COMPILER_LAUNCHER ${CMAKE_CCACHE} PARENT_SCOPE)
            message(STATUS "Using ccache for faster builds")
        endif()
    endif()
    
    # Configure ninja
    if(NOT CMAKE_GENERATOR MATCHES "Ninja")
        find_program(CMAKE_NINJA ninja)
        if(CMAKE_NINJA)
            set(CMAKE_GENERATOR "Ninja" CACHE STRING "Build system generator" FORCE)
            message(STATUS "Using Ninja build system")
        endif()
    endif()
    
    message(STATUS "Configured build options for ${CMAKE_CXX_COMPILER_ID}")
    message(STATUS "  Build type: ${CMAKE_BUILD_TYPE}")
    message(STATUS "  Parallel jobs: ${CMAKE_BUILD_PARALLEL_LEVEL}")
endfunction()

# Function to configure sanitizers
function(configure_sanitizers)
    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
            set(SANITIZER_FLAGS
                -fsanitize=address
                -fsanitize=undefined
                -fsanitize=leak
                -fsanitize=thread
            )
            
            foreach(flag ${SANITIZER_FLAGS})
                string(APPEND CMAKE_CXX_FLAGS_DEBUG " ${flag}")
            endforeach()
            
            set(CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG}" CACHE STRING "Debug flags" FORCE)
            
            message(STATUS "Enabled sanitizers: Address, Undefined, Leak, Thread")
        endif()
    endif()
endfunction()

# Function to configure code coverage
function(configure_coverage)
    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
            set(COVERAGE_FLAGS
                --coverage
                -fprofile-arcs
                -ftest-coverage
            )
            
            foreach(flag ${COVERAGE_FLAGS})
                string(APPEND CMAKE_CXX_FLAGS_DEBUG " ${flag}")
            endforeach()
            
            set(CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG}" CACHE STRING "Debug flags" FORCE)
            
            message(STATUS "Enabled code coverage")
        endif()
    endif()
endfunction() 
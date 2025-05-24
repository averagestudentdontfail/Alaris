
# cmake/utils/CompilerFlags.cmake
# Compiler-specific flags and optimizations for Alaris

function(configure_compiler_flags)
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

    # Build type specific flags
    set(CMAKE_CXX_FLAGS_DEBUG "-O0 -g -DDEBUG -fsanitize=address -fsanitize=undefined" CACHE STRING "Debug flags" FORCE)
    set(CMAKE_CXX_FLAGS_RELEASE "-O3 -DNDEBUG -march=native -mtune=native -flto" CACHE STRING "Release flags" FORCE)
    set(CMAKE_CXX_FLAGS_RELWITHDEBINFO "-O2 -g -DNDEBUG" CACHE STRING "RelWithDebInfo flags" FORCE)
    set(CMAKE_CXX_FLAGS_MINSIZEREL "-Os -DNDEBUG" CACHE STRING "MinSizeRel flags" FORCE)

    # Platform-specific optimizations
    if(CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64|AMD64")
        if(NOT CMAKE_CXX_COMPILER_ID STREQUAL "MSVC")
            set(ARCH_FLAGS "-msse4.2 -mavx -mavx2")
            if(CMAKE_BUILD_TYPE STREQUAL "Release")
                string(APPEND CMAKE_CXX_FLAGS_RELEASE " ${ARCH_FLAGS}")
            endif()
        endif()
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

    message(STATUS "Configured compiler flags for ${CMAKE_CXX_COMPILER_ID}")
    message(STATUS "  Release flags: ${CMAKE_CXX_FLAGS_RELEASE}")
    message(STATUS "  Debug flags: ${CMAKE_CXX_FLAGS_DEBUG}")
endfunction()
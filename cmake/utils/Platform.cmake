# cmake/utils/Platform.cmake
# Platform-specific configuration for Alaris

function(configure_platform)
    message(STATUS "Configuring for platform: ${CMAKE_SYSTEM_NAME}")

    # Set platform variables
    if(WIN32)
        configure_windows()
    elseif(APPLE)
        configure_macos()
    elseif(UNIX)
        configure_linux()
    endif()

    # Configure CPU architecture
    configure_architecture()

    # Configure memory management
    configure_memory()

    # Configure real-time features
    configure_realtime()
endfunction()

function(configure_windows)
    message(STATUS "Configuring for Windows")

    # Windows-specific compiler definitions
    add_compile_definitions(
        WIN32_LEAN_AND_MEAN
        NOMINMAX
        _CRT_SECURE_NO_WARNINGS
        _WIN32_WINNT=0x0A00  # Windows 10
    )

    # Windows-specific linker flags
    if(CMAKE_BUILD_TYPE STREQUAL "Release")
        set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} /LTCG" PARENT_SCOPE)
        set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} /LTCG" PARENT_SCOPE)
    endif()

    # Windows libraries for shared memory and timing
    set(WINDOWS_LIBS
        kernel32
        user32
        advapi32
        winmm      # For high-resolution timers
        ws2_32     # For networking
    )
    
    add_library(Platform::Libraries INTERFACE IMPORTED)
    target_link_libraries(Platform::Libraries INTERFACE ${WINDOWS_LIBS})
    
    # Windows-specific features
    set(PLATFORM_HAS_SHARED_MEMORY TRUE PARENT_SCOPE)
    set(PLATFORM_HAS_REALTIME FALSE PARENT_SCOPE)  # Windows doesn't have POSIX RT
    set(PLATFORM_HAS_CPU_AFFINITY TRUE PARENT_SCOPE)
endfunction()

function(configure_macos)
    message(STATUS "Configuring for macOS")

    # macOS-specific compiler flags
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -stdlib=libc++" PARENT_SCOPE)

    # macOS-specific definitions
    add_compile_definitions(
        _DARWIN_C_SOURCE
    )

    # macOS frameworks
    find_library(CORE_FOUNDATION CoreFoundation REQUIRED)
    find_library(CORE_SERVICES CoreServices REQUIRED)
    
    set(MACOS_LIBS
        ${CORE_FOUNDATION}
        ${CORE_SERVICES}
        pthread
        m
    )
    
    add_library(Platform::Libraries INTERFACE IMPORTED)
    target_link_libraries(Platform::Libraries INTERFACE ${MACOS_LIBS})

    # macOS-specific features
    set(PLATFORM_HAS_SHARED_MEMORY TRUE PARENT_SCOPE)
    set(PLATFORM_HAS_REALTIME TRUE PARENT_SCOPE)
    set(PLATFORM_HAS_CPU_AFFINITY TRUE PARENT_SCOPE)

    # Check for macOS version
    execute_process(
        COMMAND sw_vers -productVersion
        OUTPUT_VARIABLE MACOS_VERSION
        OUTPUT_STRIP_TRAILING_WHITESPACE
    )
    message(STATUS "macOS version: ${MACOS_VERSION}")
endfunction()

function(configure_linux)
    message(STATUS "Configuring for Linux")

    # Linux-specific definitions
    add_compile_definitions(
        _GNU_SOURCE
        _POSIX_C_SOURCE=200809L
    )

    # Linux libraries
    set(LINUX_LIBS
        pthread
        rt
        m
        dl
    )
    
    add_library(Platform::Libraries INTERFACE IMPORTED)
    target_link_libraries(Platform::Libraries INTERFACE ${LINUX_LIBS})

    # Linux-specific features
    set(PLATFORM_HAS_SHARED_MEMORY TRUE PARENT_SCOPE)
    set(PLATFORM_HAS_REALTIME TRUE PARENT_SCOPE)
    set(PLATFORM_HAS_CPU_AFFINITY TRUE PARENT_SCOPE)

    # Check for specific Linux distributions
    if(EXISTS /etc/os-release)
        file(STRINGS /etc/os-release DISTRO_INFO)
        foreach(line ${DISTRO_INFO})
            if(line MATCHES "^ID=(.+)")
                set(LINUX_DISTRO ${CMAKE_MATCH_1})
                string(REPLACE "\"" "" LINUX_DISTRO ${LINUX_DISTRO})
                message(STATUS "Linux distribution: ${LINUX_DISTRO}")
                break()
            endif()
        endforeach()
    endif()

    # Distribution-specific optimizations
    if(LINUX_DISTRO STREQUAL "ubuntu" OR LINUX_DISTRO STREQUAL "debian")
        # Ubuntu/Debian specific settings
        message(STATUS "Applying Ubuntu/Debian optimizations")
    elseif(LINUX_DISTRO STREQUAL "centos" OR LINUX_DISTRO STREQUAL "rhel")
        # CentOS/RHEL specific settings
        message(STATUS "Applying CentOS/RHEL optimizations")
    endif()
endfunction()

function(configure_architecture)
    message(STATUS "Target architecture: ${CMAKE_SYSTEM_PROCESSOR}")

    if(CMAKE_SYSTEM_PROCESSOR MATCHES "x86_64|AMD64|amd64")
        set(ARCH_X86_64 TRUE PARENT_SCOPE)
        
        # Check for specific CPU features
        include(CheckCXXSourceRuns)
        
        # Check for AVX2 support
        check_cxx_source_runs("
            #include <immintrin.h>
            int main() {
                __m256i a = _mm256_set1_epi32(1);
                return 0;
            }
        " HAVE_AVX2)
        
        if(HAVE_AVX2)
            message(STATUS "AVX2 support detected")
            add_compile_definitions(HAVE_AVX2)
        endif()

        # Check for FMA support
        check_cxx_source_runs("
            #include <immintrin.h>
            int main() {
                __m256 a = _mm256_set1_ps(1.0f);
                __m256 b = _mm256_set1_ps(2.0f);
                __m256 c = _mm256_set1_ps(3.0f);
                __m256 result = _mm256_fmadd_ps(a, b, c);
                return 0;
            }
        " HAVE_FMA)
        
        if(HAVE_FMA)
            message(STATUS "FMA support detected")
            add_compile_definitions(HAVE_FMA)
        endif()

    elseif(CMAKE_SYSTEM_PROCESSOR MATCHES "aarch64|arm64")
        set(ARCH_ARM64 TRUE PARENT_SCOPE)
        message(STATUS "ARM64 architecture detected")
        
        # ARM64-specific optimizations
        if(CMAKE_BUILD_TYPE STREQUAL "Release")
            set(CMAKE_CXX_FLAGS_RELEASE 
                "${CMAKE_CXX_FLAGS_RELEASE} -mcpu=native"
                PARENT_SCOPE
            )
        endif()

    elseif(CMAKE_SYSTEM_PROCESSOR MATCHES "arm")
        set(ARCH_ARM32 TRUE PARENT_SCOPE)
        message(STATUS "ARM32 architecture detected")
        
    else()
        message(WARNING "Unknown architecture: ${CMAKE_SYSTEM_PROCESSOR}")
    endif()
endfunction()

function(configure_memory)
    message(STATUS "Configuring memory management")

    # Check for huge pages support
    if(UNIX AND NOT APPLE)
        if(EXISTS /proc/meminfo)
            file(STRINGS /proc/meminfo MEMINFO_LINES)
            foreach(line ${MEMINFO_LINES})
                if(line MATCHES "HugePages_Total:[ \t]+([0-9]+)")
                    set(HUGE_PAGES_TOTAL ${CMAKE_MATCH_1})
                    if(HUGE_PAGES_TOTAL GREATER 0)
                        message(STATUS "Huge pages available: ${HUGE_PAGES_TOTAL}")
                        add_compile_definitions(HAVE_HUGE_PAGES)
                        set(PLATFORM_HAS_HUGE_PAGES TRUE PARENT_SCOPE)
                    endif()
                    break()
                endif()
            endforeach()
        endif()
    endif()

    # Memory alignment preferences
    if(ARCH_X86_64)
        add_compile_definitions(PREFERRED_ALIGNMENT=64)  # Cache line size
    else()
        add_compile_definitions(PREFERRED_ALIGNMENT=32)
    endif()
endfunction()

function(configure_realtime)
    if(NOT PLATFORM_HAS_REALTIME)
        return()
    endif()

    message(STATUS "Configuring real-time features")

    # Check for real-time capabilities
    include(CheckIncludeFileCXX)
    
    check_include_file_cxx("sched.h" HAVE_SCHED_H)
    if(HAVE_SCHED_H)
        add_compile_definitions(HAVE_SCHED_H)
        
        # Check for specific scheduling policies
        include(CheckCXXSourceCompiles)
        
        check_cxx_source_compiles("
            #include <sched.h>
            int main() {
                struct sched_param param;
                param.sched_priority = 50;
                sched_setscheduler(0, SCHED_FIFO, &param);
                return 0;
            }
        " HAVE_SCHED_FIFO)
        
        if(HAVE_SCHED_FIFO)
            message(STATUS "SCHED_FIFO support available")
            add_compile_definitions(HAVE_SCHED_FIFO)
        endif()
    endif()

    # Check for memory locking
    check_include_file_cxx("sys/mman.h" HAVE_SYS_MMAN_H)
    if(HAVE_SYS_MMAN_H)
        add_compile_definitions(HAVE_SYS_MMAN_H)
        
        check_cxx_source_compiles("
            #include <sys/mman.h>
            int main() {
                mlockall(MCL_CURRENT | MCL_FUTURE);
                return 0;
            }
        " HAVE_MLOCKALL)
        
        if(HAVE_MLOCKALL)
            message(STATUS "Memory locking (mlockall) available")
            add_compile_definitions(HAVE_MLOCKALL)
        endif()
    endif()

    # Check for CPU affinity
    check_cxx_source_compiles("
        #include <sched.h>
        int main() {
            cpu_set_t set;
            CPU_ZERO(&set);
            CPU_SET(0, &set);
            sched_setaffinity(0, sizeof(set), &set);
            return 0;
        }
    " HAVE_CPU_AFFINITY)
    
    if(HAVE_CPU_AFFINITY)
        message(STATUS "CPU affinity control available")
        add_compile_definitions(HAVE_CPU_AFFINITY)
    endif()
endfunction()

# Function to detect available CPU cores
function(detect_cpu_cores)
    include(ProcessorCount)
    ProcessorCount(CPU_CORES)
    
    if(CPU_CORES EQUAL 0)
        set(CPU_CORES 1)
    endif()
    
    message(STATUS "Detected CPU cores: ${CPU_CORES}")
    set(SYSTEM_CPU_CORES ${CPU_CORES} PARENT_SCOPE)
    add_compile_definitions(SYSTEM_CPU_CORES=${CPU_CORES})
endfunction()

# Call CPU detection
detect_cpu_cores()
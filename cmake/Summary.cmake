# cmake/Summary.cmake
# Configuration summary for Alaris

function(print_configuration_summary)
    message(STATUS "")
    message(STATUS "╔════════════════════════════════════════════════════════════════════════════════╗")
    message(STATUS "║                        Alaris Configuration Summary                            ║")
    message(STATUS "╠════════════════════════════════════════════════════════════════════════════════╣")
    
    # Project information
    message(STATUS "║ Project: ${PROJECT_NAME} v${PROJECT_VERSION}")
    message(STATUS "║ Description: ${PROJECT_DESCRIPTION}")
    message(STATUS "║")
    
    # Build configuration
    message(STATUS "║ Build Configuration:")
    message(STATUS "║   Build Type:           ${CMAKE_BUILD_TYPE}")
    message(STATUS "║   Generator:            ${CMAKE_GENERATOR}")
    message(STATUS "║   Source Directory:     ${CMAKE_SOURCE_DIR}")
    message(STATUS "║   Build Directory:      ${CMAKE_BINARY_DIR}")
    message(STATUS "║   Install Prefix:       ${CMAKE_INSTALL_PREFIX}")
    message(STATUS "║")
    
    # Compiler information
    message(STATUS "║ Compiler Configuration:")
    message(STATUS "║   C++ Compiler:         ${CMAKE_CXX_COMPILER_ID} ${CMAKE_CXX_COMPILER_VERSION}")
    message(STATUS "║   C++ Standard:         C++${CMAKE_CXX_STANDARD}")
    message(STATUS "║   Compiler Path:        ${CMAKE_CXX_COMPILER}")
    
    # Compiler flags
    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        message(STATUS "║   Flags (Debug):        ${CMAKE_CXX_FLAGS} ${CMAKE_CXX_FLAGS_DEBUG}")
    elseif(CMAKE_BUILD_TYPE STREQUAL "Release")
        message(STATUS "║   Flags (Release):      ${CMAKE_CXX_FLAGS} ${CMAKE_CXX_FLAGS_RELEASE}")
    elseif(CMAKE_BUILD_TYPE STREQUAL "RelWithDebInfo")
        message(STATUS "║   Flags (RelWithDbg):   ${CMAKE_CXX_FLAGS} ${CMAKE_CXX_FLAGS_RELWITHDEBINFO}")
    elseif(CMAKE_BUILD_TYPE STREQUAL "MinSizeRel")
        message(STATUS "║   Flags (MinSize):      ${CMAKE_CXX_FLAGS} ${CMAKE_CXX_FLAGS_MINSIZEREL}")
    endif()
    message(STATUS "║")
    
    # Platform information
    message(STATUS "║ Platform Configuration:")
    message(STATUS "║   System:               ${CMAKE_SYSTEM_NAME} ${CMAKE_SYSTEM_VERSION}")
    message(STATUS "║   Processor:            ${CMAKE_SYSTEM_PROCESSOR}")
    if(DEFINED SYSTEM_CPU_CORES)
        message(STATUS "║   CPU Cores:            ${SYSTEM_CPU_CORES}")
    endif()
    
    # Platform features
    if(PLATFORM_HAS_SHARED_MEMORY)
        message(STATUS "║   Shared Memory:        ✓ Available")
    else()
        message(STATUS "║   Shared Memory:        ✗ Not available")
    endif()
    
    if(PLATFORM_HAS_REALTIME)
        message(STATUS "║   Real-time Support:    ✓ Available")
    else()
        message(STATUS "║   Real-time Support:    ✗ Not available")
    endif()
    
    if(PLATFORM_HAS_CPU_AFFINITY)
        message(STATUS "║   CPU Affinity:         ✓ Available")
    else()
        message(STATUS "║   CPU Affinity:         ✗ Not available")
    endif()
    
    if(PLATFORM_HAS_HUGE_PAGES)
        message(STATUS "║   Huge Pages:           ✓ Available")
    else()
        message(STATUS "║   Huge Pages:           ✗ Not available")
    endif()
    message(STATUS "║")
    
    # Dependencies
    message(STATUS "║ Dependencies:")
    
    # QuantLib
    if(TARGET ${QUANTLIB_TARGET})
        message(STATUS "║   QuantLib:             ✓ ${QUANTLIB_TARGET} (built from source)")
    else()
        message(STATUS "║   QuantLib:             ✗ Not found")
    endif()
    
    # yaml-cpp
    if(TARGET ${YAML_CPP_TARGET})
        message(STATUS "║   yaml-cpp:             ✓ ${YAML_CPP_TARGET} (built from source)")
    else()
        message(STATUS "║   yaml-cpp:             ✗ Not found")
    endif()
    
    # Boost
    if(Boost_FOUND)
        message(STATUS "║   Boost:                ✓ ${Boost_VERSION} (system)")
    else()
        message(STATUS "║   Boost:                ⚠ Using QuantLib's built-in version")
    endif()
    
    # Optional dependencies
    if(OpenMP_CXX_FOUND)
        message(STATUS "║   OpenMP:               ✓ ${OpenMP_CXX_VERSION}")
    else()
        message(STATUS "║   OpenMP:               ✗ Not found")
    endif()
    
    # System libraries
    if(SYSTEM_LIBS)
        message(STATUS "║   System Libraries:     ✓ Found: ${SYSTEM_LIBS}")
    else()
        message(STATUS "║   System Libraries:     ⚠ Platform-specific libraries may be missing")
    endif()
    
    # Development tools
    if(CCACHE_PROGRAM)
        message(STATUS "║   CCache:               ✓ ${CCACHE_PROGRAM}")
    else()
        message(STATUS "║   CCache:               ✗ Not found (builds will be slower)")
    endif()
    
    if(NINJA_PROGRAM)
        message(STATUS "║   Ninja:                ✓ ${NINJA_PROGRAM}")
    else()
        message(STATUS "║   Ninja:                ✗ Not found")
    endif()
    message(STATUS "║")
    
    # Targets
    message(STATUS "║ Build Targets:")
    
    # Main targets
    if(TARGET quantlib_process)
        message(STATUS "║   quantlib_process:     ✓ Main QuantLib trading process")
    else()
        message(STATUS "║   quantlib_process:     ✗ Not configured")
    endif()
    
    # Library targets
    if(TARGET alaris_core)
        message(STATUS "║   alaris_core:          ✓ Core utilities library")
    endif()
    if(TARGET alaris_pricing)
        message(STATUS "║   alaris_pricing:       ✓ Option pricing library")
    endif()
    if(TARGET alaris_volatility)
        message(STATUS "║   alaris_volatility:    ✓ Volatility modeling library")
    endif()
    if(TARGET alaris_strategy)
        message(STATUS "║   alaris_strategy:      ✓ Trading strategy library")
    endif()
    
    # Test targets
    if(TARGET alaris_core_test)
        message(STATUS "║   alaris_core_test:     ✓ Core component tests")
    endif()
    if(TARGET alaris_integration_test)
        message(STATUS "║   alaris_integration_test: ✓ Integration tests")
    endif()
    message(STATUS "║")
    
    # Features and optimizations
    message(STATUS "║ Features & Optimizations:")
    
    # Architecture optimizations
    if(ARCH_X86_64)
        message(STATUS "║   Architecture:         x86_64")
        if(HAVE_AVX2)
            message(STATUS "║   AVX2 Support:         ✓ Enabled")
        else()
            message(STATUS "║   AVX2 Support:         ✗ Not available")
        endif()
        if(HAVE_FMA)
            message(STATUS "║   FMA Support:          ✓ Enabled")
        else()
            message(STATUS "║   FMA Support:          ✗ Not available")
        endif()
    elseif(ARCH_ARM64)
        message(STATUS "║   Architecture:         ARM64")
    elseif(ARCH_ARM32)
        message(STATUS "║   Architecture:         ARM32")
    endif()
    
    # Build optimizations
    if(CMAKE_BUILD_TYPE STREQUAL "Release")
        message(STATUS "║   LTO:                  ✓ Enabled (Release)")
        message(STATUS "║   Native Optimization: ✓ Enabled (-march=native)")
    else()
        message(STATUS "║   LTO:                  ✗ Disabled (Debug build)")
        message(STATUS "║   Native Optimization: ✗ Disabled (Debug build)")
    endif()
    
    # Sanitizers (Debug builds)
    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        if(CMAKE_CXX_FLAGS_DEBUG MATCHES "sanitize=address")
            message(STATUS "║   Address Sanitizer:    ✓ Enabled (Debug)")
        else()
            message(STATUS "║   Address Sanitizer:    ✗ Disabled")
        endif()
        if(CMAKE_CXX_FLAGS_DEBUG MATCHES "sanitize=undefined")
            message(STATUS "║   UB Sanitizer:         ✓ Enabled (Debug)")
        else()
            message(STATUS "║   UB Sanitizer:         ✗ Disabled")
        endif()
    endif()
    message(STATUS "║")
    
    # Installation and packaging
    message(STATUS "║ Installation & Packaging:")
    message(STATUS "║   Install Prefix:       ${CMAKE_INSTALL_PREFIX}")
    if(DEFINED CPACK_GENERATOR)
        message(STATUS "║   Package Generators:   ${CPACK_GENERATOR}")
    endif()
    if(LEAN_AVAILABLE)
        message(STATUS "║   Lean Integration:     ✓ Available (.NET project)")
    else()
        message(STATUS "║   Lean Integration:     ⚠ Submodule not found")
    endif()
    message(STATUS "║")
    
    # Commands and next steps
    message(STATUS "║ Available Commands:")
    message(STATUS "║   Build:                make -j${SYSTEM_CPU_CORES} (or ninja)")
    message(STATUS "║   Test:                 ctest --output-on-failure")
    message(STATUS "║   Install:              make install")
    message(STATUS "║   Package:              make package")
    message(STATUS "║   Clean:                make clean")
    if(GIT_FOUND)
        message(STATUS "║   Update Submodules:    make submodules-update")
    endif()
    message(STATUS "║")
    
    # Warnings and recommendations
    print_warnings_and_recommendations()
    
    message(STATUS "╚════════════════════════════════════════════════════════════════════════════════╝")
    message(STATUS "")
endfunction()

function(print_warnings_and_recommendations)
    set(HAS_WARNINGS FALSE)
    
    # Check for common issues and recommendations
    if(NOT Boost_FOUND)
        if(NOT HAS_WARNINGS)
            message(STATUS "║ Warnings & Recommendations:")
            set(HAS_WARNINGS TRUE)
        endif()
        message(STATUS "║   ⚠ Consider installing system Boost for better performance")
    endif()
    
    if(NOT CCACHE_PROGRAM)
        if(NOT HAS_WARNINGS)
            message(STATUS "║ Warnings & Recommendations:")
            set(HAS_WARNINGS TRUE)
        endif()
        message(STATUS "║   ⚠ Install ccache to speed up rebuilds")
    endif()
    
    if(CMAKE_BUILD_TYPE STREQUAL "Debug" AND NOT CMAKE_CXX_FLAGS_DEBUG MATCHES "sanitize")
        if(NOT HAS_WARNINGS)
            message(STATUS "║ Warnings & Recommendations:")
            set(HAS_WARNINGS TRUE)
        endif()
        message(STATUS "║   ⚠ Consider enabling sanitizers for Debug builds")
    endif()
    
    if(NOT PLATFORM_HAS_REALTIME AND UNIX)
        if(NOT HAS_WARNINGS)
            message(STATUS "║ Warnings & Recommendations:")
            set(HAS_WARNINGS TRUE)
        endif()
        message(STATUS "║   ⚠ Real-time features not available on this platform")
    endif()
    
    if(NOT LEAN_AVAILABLE)
        if(NOT HAS_WARNINGS)
            message(STATUS "║ Warnings & Recommendations:")
            set(HAS_WARNINGS TRUE)
        endif()
        message(STATUS "║   ⚠ Lean submodule not found - run: git submodule update --init")
    endif()
    
    # Performance recommendations
    if(CMAKE_BUILD_TYPE STREQUAL "Release")
        if(NOT HAS_WARNINGS)
            message(STATUS "║ Performance Recommendations:")
            set(HAS_WARNINGS TRUE)
        endif()
        message(STATUS "║   • Ensure CPU performance governor is set to 'performance'")
        message(STATUS "║   • Consider disabling CPU frequency scaling for trading")
        message(STATUS "║   • Use CPU isolation for real-time cores")
        if(PLATFORM_HAS_HUGE_PAGES)
            message(STATUS "║   • Configure huge pages for better memory performance")
        endif()
    endif()
    
    if(HAS_WARNINGS)
        message(STATUS "║")
    endif()
endfunction()

# Function to save configuration to file
function(save_configuration_summary)
    set(SUMMARY_FILE "${CMAKE_BINARY_DIR}/configuration_summary.txt")
    
    file(WRITE ${SUMMARY_FILE} "Alaris Trading System - Configuration Summary\n")
    file(APPEND ${SUMMARY_FILE} "Generated: ${CMAKE_CURRENT_LIST_FILE}\n")
    file(APPEND ${SUMMARY_FILE} "Date: ")
    
    # Get current timestamp
    string(TIMESTAMP CURRENT_TIME "%Y-%m-%d %H:%M:%S")
    file(APPEND ${SUMMARY_FILE} "${CURRENT_TIME}\n\n")
    
    # Build configuration
    file(APPEND ${SUMMARY_FILE} "Build Configuration:\n")
    file(APPEND ${SUMMARY_FILE} "  Project: ${PROJECT_NAME} v${PROJECT_VERSION}\n")
    file(APPEND ${SUMMARY_FILE} "  Build Type: ${CMAKE_BUILD_TYPE}\n")
    file(APPEND ${SUMMARY_FILE} "  Generator: ${CMAKE_GENERATOR}\n")
    file(APPEND ${SUMMARY_FILE} "  Source Directory: ${CMAKE_SOURCE_DIR}\n")
    file(APPEND ${SUMMARY_FILE} "  Build Directory: ${CMAKE_BINARY_DIR}\n")
    file(APPEND ${SUMMARY_FILE} "  Install Prefix: ${CMAKE_INSTALL_PREFIX}\n\n")
    
    # Platform information
    file(APPEND ${SUMMARY_FILE} "Platform:\n")
    file(APPEND ${SUMMARY_FILE} "  System: ${CMAKE_SYSTEM_NAME} ${CMAKE_SYSTEM_VERSION}\n")
    file(APPEND ${SUMMARY_FILE} "  Processor: ${CMAKE_SYSTEM_PROCESSOR}\n")
    file(APPEND ${SUMMARY_FILE} "  Compiler: ${CMAKE_CXX_COMPILER_ID} ${CMAKE_CXX_COMPILER_VERSION}\n\n")
    
    # Dependencies
    file(APPEND ${SUMMARY_FILE} "Dependencies:\n")
    if(TARGET ${QUANTLIB_TARGET})
        file(APPEND ${SUMMARY_FILE} "  QuantLib: ${QUANTLIB_TARGET} (built from source)\n")
    endif()
    if(TARGET ${YAML_CPP_TARGET})
        file(APPEND ${SUMMARY_FILE} "  yaml-cpp: ${YAML_CPP_TARGET} (built from source)\n")
    endif()
    if(Boost_FOUND)
        file(APPEND ${SUMMARY_FILE} "  Boost: ${Boost_VERSION} (system)\n")
    endif()
    
    message(STATUS "Configuration summary saved to: ${SUMMARY_FILE}")
endfunction()

# Save summary to file
save_configuration_summary()
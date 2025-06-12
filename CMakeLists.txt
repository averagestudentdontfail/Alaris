# CMakeLists.txt - Alaris Trading System
cmake_minimum_required(VERSION 3.20)
project(Alaris VERSION 1.0.0 LANGUAGES CXX)

# Set policy for newer CMake versions
if(POLICY CMP0135)
    cmake_policy(SET CMP0135 NEW)
endif()

message(STATUS "")
message(STATUS "=== Configuring Alaris Trading System ===")
message(STATUS "  Version: ${PROJECT_VERSION}")
message(STATUS "  Source:  ${CMAKE_SOURCE_DIR}")
message(STATUS "  Binary:  ${CMAKE_BINARY_DIR}")

# --- 1. Include Configuration Definitions ---
message(STATUS "Loading build system configuration...")
include(.cmake/BuildSystem.cmake)
include(.cmake/External.cmake)
include(.cmake/Config.cmake)
include(.cmake/Components.cmake)
include(.cmake/GitInfo.cmake)

# --- 2. Perform Initial Setup ---
message(STATUS "Creating build metadata...")
create_build_info_file("${CMAKE_BINARY_DIR}/alaris_build_info.txt")

# --- 3. Define All C++ and C# Targets ---
message(STATUS "Configuring C++ components...")
configure_all_components()

# --- 4. Configure .NET Projects ---
if(DOTNET_EXECUTABLE)
    message(STATUS "Configuring .NET components...")
    add_dotnet_project(lean-process "${CMAKE_SOURCE_DIR}/src/csharp/Alaris.Lean.csproj")
    
    # Make sure C++ components build before .NET
    if(TARGET lean-process AND TARGET alaris)
        add_dependencies(lean-process alaris)
    endif()
else()
    message(WARNING ".NET SDK not found - Lean process will not be built")
    message(STATUS "To build .NET components, install .NET 6.0+ SDK")
endif()

# --- 5. Add final build coordination targets ---
# Create a meta-target that depends on everything
if(TARGET lean-process)
    add_custom_target(alaris-complete ALL
        DEPENDS alaris-all lean-process
        COMMENT "Building complete Alaris system"
    )
    
    # Add final setup step that runs after everything is built
    add_custom_command(TARGET alaris-complete POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E echo ""
        COMMAND ${CMAKE_COMMAND} -E echo "=== Alaris Build Complete ==="
        COMMAND ${CMAKE_COMMAND} -E echo "All components built successfully!"
        COMMAND ${CMAKE_COMMAND} -E echo ""
        COMMAND ${CMAKE_COMMAND} -E echo "Quick start:"
        COMMAND ${CMAKE_COMMAND} -E echo "  ./start-alaris.sh paper    # Paper trading"
        COMMAND ${CMAKE_COMMAND} -E echo "  ./start-alaris.sh live     # Live trading"
        COMMAND ${CMAKE_COMMAND} -E echo ""
        COMMAND ${CMAKE_COMMAND} -E echo "Troubleshooting:"
        COMMAND ${CMAKE_COMMAND} -E echo "  make help                  # Show help"
        COMMAND ${CMAKE_COMMAND} -E echo "  make verify-build          # Verify build"
        COMMAND ${CMAKE_COMMAND} -E echo "  make alaris-setup          # Manual setup"
        COMMAND ${CMAKE_COMMAND} -E echo "============================"
        VERBATIM
        COMMENT "Final build completion message"
    )
else()
    add_custom_target(alaris-complete ALL
        DEPENDS alaris-all
        COMMENT "Building Alaris system (C++ only)"
    )
    
    add_custom_command(TARGET alaris-complete POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E echo ""
        COMMAND ${CMAKE_COMMAND} -E echo "=== Alaris Build Complete (C++ Only) ==="
        COMMAND ${CMAKE_COMMAND} -E echo "C++ components built successfully!"
        COMMAND ${CMAKE_COMMAND} -E echo ""
        COMMAND ${CMAKE_COMMAND} -E echo "Note: .NET components not built (SDK not found)"
        COMMAND ${CMAKE_COMMAND} -E echo "Install .NET 6.0+ SDK to build Lean process"
        COMMAND ${CMAKE_COMMAND} -E echo ""
        COMMAND ${CMAKE_COMMAND} -E echo "Available executables:"
        COMMAND ${CMAKE_COMMAND} -E echo "  ./bin/quantlib-process     # QuantLib process"
        COMMAND ${CMAKE_COMMAND} -E echo "  ./bin/alaris               # Main executable"
        COMMAND ${CMAKE_COMMAND} -E echo "========================================="
        VERBATIM
        COMMENT "C++ only build completion message"
    )
endif()

# --- 6. Configure Deployment (after targets are defined) ---
message(STATUS "Configuring deployment...")
include(.cmake/Deployment.cmake)

# --- 7. Build Summary ---
show_build_summary()

# --- 8. Add helpful targets for development ---
# Development convenience target
add_custom_target(dev
    COMMAND ${CMAKE_COMMAND} --build . --target alaris-complete
    COMMAND ${CMAKE_COMMAND} -E echo ""
    COMMAND ${CMAKE_COMMAND} -E echo "Development build complete!"
    COMMAND ${CMAKE_COMMAND} -E echo "Run './start-alaris.sh paper' to test"
    VERBATIM
    COMMENT "Quick development build"
)

# Clean shared memory target
add_custom_target(clean-shm
    COMMAND sudo rm -f /dev/shm/alaris_* 2>/dev/null || rm -f /dev/shm/alaris_* 2>/dev/null || true
    COMMAND ${CMAKE_COMMAND} -E echo "Shared memory cleaned"
    VERBATIM
    COMMENT "Clean shared memory segments"
)

# Test connectivity target
add_custom_target(test-ibkr
    COMMAND ${CMAKE_COMMAND} -E echo "Testing IBKR connectivity..."
    COMMAND timeout 5 bash -c 'cat < /dev/null > /dev/tcp/172.31.16.1/4002' && echo "✓ IBKR Paper port reachable" || echo "✗ IBKR Paper port not reachable"
    COMMAND timeout 5 bash -c 'cat < /dev/null > /dev/tcp/172.31.16.1/4001' && echo "✓ IBKR Live port reachable" || echo "✗ IBKR Live port not reachable"
    VERBATIM
    COMMENT "Test IBKR connectivity"
)

message(STATUS "=== Configuration Complete ===")
message(STATUS "Ready to build: cmake --build .")
message(STATUS "Quick build:    make dev")
message(STATUS "Show help:      make help")
message(STATUS "===============================")
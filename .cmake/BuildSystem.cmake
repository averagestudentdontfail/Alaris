# .cmake/BuildSystem.cmake
# Streamlined build system for Alaris trading system

# Platform detection
if(WIN32)
    set(ALARIS_PLATFORM "Windows")
elseif(APPLE)
    set(ALARIS_PLATFORM "Darwin")
elseif(UNIX)
    set(ALARIS_PLATFORM "Linux")
endif()

# Build configuration
if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE "Release" CACHE STRING "Build type" FORCE)
endif()

set(CMAKE_CXX_STANDARD 20 CACHE STRING "C++ standard")
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Output directories with proper creation
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# Ensure output directories exist
file(MAKE_DIRECTORY ${CMAKE_RUNTIME_OUTPUT_DIRECTORY})
file(MAKE_DIRECTORY ${CMAKE_LIBRARY_OUTPUT_DIRECTORY})
file(MAKE_DIRECTORY ${CMAKE_ARCHIVE_OUTPUT_DIRECTORY})

find_package(Threads REQUIRED)

# Enhanced .NET project build function
function(add_dotnet_project TARGET_NAME PROJECT_FILE)
    if(NOT DOTNET_EXECUTABLE)
        message(WARNING ".NET SDK not found - ${TARGET_NAME} will not be built")
        return()
    endif()

    get_filename_component(PROJECT_DIR ${PROJECT_FILE} DIRECTORY)
    set(FINAL_DESTINATION_DIR "${CMAKE_BINARY_DIR}/bin")

    # Create a copy script to handle multiple possible output locations
    set(COPY_SCRIPT "${CMAKE_BINARY_DIR}/copy_dotnet_${TARGET_NAME}.sh")
    file(WRITE "${COPY_SCRIPT}" "#!/bin/bash
set -e
PROJECT_DIR=\"${PROJECT_DIR}\"
DEST_DIR=\"${FINAL_DESTINATION_DIR}\"
BUILD_TYPE=\"${CMAKE_BUILD_TYPE}\"

echo \"Searching for .NET build output in \$PROJECT_DIR/bin/...\"

# Function to copy files from source to destination
copy_dotnet_files() {
    local src_dir=\"\$1\"
    echo \"Copying files from \$src_dir to \$DEST_DIR\"
    
    # Create destination directory
    mkdir -p \"\$DEST_DIR\"
    
    # Copy all files from source to destination
    cp -v \"\$src_dir\"/* \"\$DEST_DIR/\" 2>/dev/null || true
    
    # Verify the main DLL was copied
    if [[ -f \"\$DEST_DIR/Alaris.Lean.dll\" ]]; then
        echo \"✓ Alaris.Lean.dll successfully copied\"
        echo \"Files in destination:\"
        ls -la \"\$DEST_DIR/\"*.dll \"\$DEST_DIR/\"*.exe 2>/dev/null || echo \"  No DLL/EXE files found\"
    else
        echo \"ERROR: Alaris.Lean.dll was not copied to \$DEST_DIR\"
        echo \"Source directory contents:\"
        ls -la \"\$src_dir/\"
        echo \"Destination directory contents:\"
        ls -la \"\$DEST_DIR/\"
        return 1
    fi
}

# Try different possible output locations
if [[ -f \"\$PROJECT_DIR/bin/Alaris.Lean.dll\" ]]; then
    echo \"Found output in bin/ - copying...\"
    copy_dotnet_files \"\$PROJECT_DIR/bin\"
elif [[ -f \"\$PROJECT_DIR/bin/\$BUILD_TYPE/Alaris.Lean.dll\" ]]; then
    echo \"Found output in bin/\$BUILD_TYPE/ - copying...\"
    copy_dotnet_files \"\$PROJECT_DIR/bin/\$BUILD_TYPE\"
elif [[ -f \"\$PROJECT_DIR/bin/Release/Alaris.Lean.dll\" ]]; then
    echo \"Found output in bin/Release/ - copying...\"
    copy_dotnet_files \"\$PROJECT_DIR/bin/Release\"
elif [[ -f \"\$PROJECT_DIR/bin/Debug/Alaris.Lean.dll\" ]]; then
    echo \"Found output in bin/Debug/ - copying...\"
    copy_dotnet_files \"\$PROJECT_DIR/bin/Debug\"
else
    echo \"ERROR: Could not find Alaris.Lean.dll in any expected location:\"
    echo \"  \$PROJECT_DIR/bin/\"
    echo \"  \$PROJECT_DIR/bin/\$BUILD_TYPE/\"
    echo \"  \$PROJECT_DIR/bin/Release/\"
    echo \"  \$PROJECT_DIR/bin/Debug/\"
    echo \"\"
    echo \"Available files in \$PROJECT_DIR/bin/:\"
    find \"\$PROJECT_DIR/bin\" -name \"*.dll\" -o -name \"*.exe\" 2>/dev/null || echo \"  No DLL/EXE files found\"
    echo \"\"
    echo \"Directory structure:\"
    ls -la \"\$PROJECT_DIR/bin/\" 2>/dev/null || echo \"  bin/ directory not found\"
    exit 1
fi

echo \"✓ Successfully copied .NET artifacts to \$DEST_DIR\"
")
    execute_process(COMMAND chmod +x "${COPY_SCRIPT}")

    # Add custom target with better dependency handling
    add_custom_target(${TARGET_NAME} ALL
        COMMAND ${CMAKE_COMMAND} -E echo "Building .NET project: ${TARGET_NAME}"
        COMMAND ${DOTNET_EXECUTABLE} build "${PROJECT_FILE}" -c ${CMAKE_BUILD_TYPE} --verbosity quiet
        COMMAND ${CMAKE_COMMAND} -E echo "Copying .NET artifacts to: ${FINAL_DESTINATION_DIR}"
        COMMAND ${CMAKE_COMMAND} -E make_directory "${FINAL_DESTINATION_DIR}"
        COMMAND bash "${COPY_SCRIPT}"
        COMMAND ${CMAKE_COMMAND} -E echo "✓ .NET project ${TARGET_NAME} built successfully"
        WORKING_DIRECTORY ${PROJECT_DIR}
        VERBATIM
        COMMENT "Building .NET project ${TARGET_NAME}"
    )
    
    # Add dependencies if C++ targets exist
    if(TARGET quantlib)
        add_dependencies(${TARGET_NAME} quantlib)
    endif()
    if(TARGET alaris)
        add_dependencies(${TARGET_NAME} alaris)
    endif()
endfunction()

# Function to create a build summary
function(show_build_summary)
    message(STATUS "")
    message(STATUS "=== Alaris Build System Summary ===")
    message(STATUS "  Platform:           ${ALARIS_PLATFORM}")
    message(STATUS "  Build Type:         ${CMAKE_BUILD_TYPE}")
    message(STATUS "  C++ Standard:       ${CMAKE_CXX_STANDARD}")
    message(STATUS "  Binary Directory:   ${CMAKE_RUNTIME_OUTPUT_DIRECTORY}")
    
    if(DOTNET_EXECUTABLE)
        message(STATUS "  .NET SDK:           ${CMAKE_DOTNET_VERSION}")
    else()
        message(STATUS "  .NET SDK:           Not found")
    endif()
    
    if(ALARIS_CAPABILITIES_AVAILABLE)
        message(STATUS "  Linux Capabilities: Available")
        if(ALARIS_CAN_SUDO)
            message(STATUS "  Sudo Access:        Available (auto-setup enabled)")
        else()
            message(STATUS "  Sudo Access:        Not available (manual setup required)")
        endif()
    else()
        message(STATUS "  Linux Capabilities: Not available")
    endif()
    
    message(STATUS "")
    message(STATUS "Available targets:")
    message(STATUS "  alaris               - Main executable")
    message(STATUS "  quantlib-process     - QuantLib process")
    message(STATUS "  alaris-config        - Configuration tool")
    message(STATUS "  alaris-system        - System tool")
    if(DOTNET_EXECUTABLE)
        message(STATUS "  lean-process         - .NET Lean process")
    endif()
    message(STATUS "  alaris-all           - Build all components")
    if(ALARIS_CAPABILITIES_AVAILABLE)
        message(STATUS "  set-capabilities     - Set Linux capabilities")
        message(STATUS "  alaris-setup         - Run setup manually")
    endif()
    message(STATUS "  verify-build         - Verify build completion")
    message(STATUS "=====================================")
endfunction()

# Function to add helpful build messages
function(add_build_messages)
    # Add a target that shows helpful information
    add_custom_target(alaris-help
        COMMAND ${CMAKE_COMMAND} -E echo ""
        COMMAND ${CMAKE_COMMAND} -E echo "=== Alaris Trading System Build Help ==="
        COMMAND ${CMAKE_COMMAND} -E echo ""
        COMMAND ${CMAKE_COMMAND} -E echo "Quick start:"
        COMMAND ${CMAKE_COMMAND} -E echo "  cmake --build .                    # Build all components"
        COMMAND ${CMAKE_COMMAND} -E echo "  ./start-alaris.sh paper            # Start in paper trading mode"
        COMMAND ${CMAKE_COMMAND} -E echo "  ./start-alaris.sh live             # Start in live trading mode"
        COMMAND ${CMAKE_COMMAND} -E echo ""
        COMMAND ${CMAKE_COMMAND} -E echo "Useful targets:"
        COMMAND ${CMAKE_COMMAND} -E echo "  make alaris-all                    # Build all components"
        COMMAND ${CMAKE_COMMAND} -E echo "  make verify-build                  # Verify build completion"
        COMMAND ${CMAKE_COMMAND} -E echo "  make alaris-setup                  # Run setup manually"
        COMMAND ${CMAKE_COMMAND} -E echo "  make set-capabilities              # Set Linux capabilities"
        COMMAND ${CMAKE_COMMAND} -E echo ""
        COMMAND ${CMAKE_COMMAND} -E echo "Troubleshooting:"
        COMMAND ${CMAKE_COMMAND} -E echo "  - If capabilities fail: run 'sudo make set-capabilities'"
        COMMAND ${CMAKE_COMMAND} -E echo "  - If .NET build fails: check 'dotnet --version'"
        COMMAND ${CMAKE_COMMAND} -E echo "  - For logs: check build/logs/ directory"
        COMMAND ${CMAKE_COMMAND} -E echo ""
        COMMAND ${CMAKE_COMMAND} -E echo "========================================"
        VERBATIM
        COMMENT "Showing Alaris build help"
    )
endfunction()

# Add helper functions
add_build_messages()

# Set up custom target for showing the summary at the end
add_custom_target(build-summary
    COMMAND ${CMAKE_COMMAND} -E echo ""
    COMMAND ${CMAKE_COMMAND} -E echo "=== Build Complete ==="
    COMMAND ${CMAKE_COMMAND} -E echo "Run './start-alaris.sh paper' to start in paper trading mode"
    COMMAND ${CMAKE_COMMAND} -E echo "Run './start-alaris.sh live' to start in live trading mode"
    COMMAND ${CMAKE_COMMAND} -E echo "Run 'make help' for more options"
    COMMAND ${CMAKE_COMMAND} -E echo "======================"
    VERBATIM
    COMMENT "Build completion summary"
)
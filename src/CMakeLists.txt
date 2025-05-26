# src/CMakeLists.txt
message(STATUS "Configuring Alaris source components...")

# Add the CMake helpers
include(${CMAKE_SOURCE_DIR}/.cmake/Helpers.cmake)

# Add subdirectories
add_subdirectory(quantlib)

# Handle C# components if .NET is available
if(DOTNET_EXECUTABLE)
    message(STATUS "Configuring C# components...")
    
    # Build the C# project
    add_custom_target(alaris_lean_build ALL
        COMMAND ${DOTNET_EXECUTABLE} build 
            "${CMAKE_CURRENT_SOURCE_DIR}/csharp/Alaris.Lean.csproj" 
            -c ${CMAKE_BUILD_TYPE}
            -o "${CMAKE_BINARY_DIR}/bin/lean"
        WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/csharp
        COMMENT "Building Alaris Lean C# components"
    )
    
    # Copy configuration files
    add_custom_command(TARGET alaris_lean_build POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${CMAKE_SOURCE_DIR}/config/lean_process.yaml"
            "${CMAKE_BINARY_DIR}/bin/lean/config/lean_process.yaml"
    )
else()
    message(WARNING ".NET SDK not found - C# components will not be built")
endif()

message(STATUS "Source components configuration complete")
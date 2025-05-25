cmake_minimum_required(VERSION 3.20)

# Set C# project properties
set(CSHARP_PROJECT_NAME "Alaris.Lean")
set(CSHARP_PROJECT_FILE "${CMAKE_CURRENT_SOURCE_DIR}/${CSHARP_PROJECT_NAME}.csproj")

# Configure build arguments
set(CSHARP_BUILD_ARGS
    "build"
    "${CSHARP_PROJECT_FILE}"
    "--configuration" "Release"
    "--verbosity" "minimal"
    "-p:GenerateFullPaths=true"
    "-p:TargetFramework=net${CMAKE_DOTNET_VERSION}"
)

# Create custom target for C# build
add_custom_target(${CSHARP_PROJECT_NAME}
    COMMAND ${DOTNET_EXECUTABLE} restore "${CSHARP_PROJECT_FILE}"
    COMMAND ${DOTNET_EXECUTABLE} ${CSHARP_BUILD_ARGS}
    WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
    COMMENT "Building ${CSHARP_PROJECT_NAME}"
    DEPENDS "${CSHARP_PROJECT_FILE}"
)

# Add dependencies
if(TARGET lean_build)
    add_dependencies(${CSHARP_PROJECT_NAME} lean_build)
endif()

# Add to the main project's dependencies
if(TARGET alaris_core)
    add_dependencies(alaris_core ${CSHARP_PROJECT_NAME})
endif() 
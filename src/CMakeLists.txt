# CMakeLists.txt located in the 'src' directory
# This file organizes the build for different source components.

# Add the C++ QuantLib components
# This requires 'src/quantlib/CMakeLists.txt' to exist.
if(EXISTS "${CMAKE_CURRENT_LIST_DIR}/quantlib/CMakeLists.txt")
    add_subdirectory(quantlib)
else()
    message(WARNING "CMakeLists.txt not found in src/quantlib. QuantLib components will not be built.")
endif()

# Add C# components if .NET SDK was detected by DependencyManager.cmake
# DOTNET_VERSION should be set in a parent scope (e.g., by your DependencyManager.cmake)
if(DEFINED DOTNET_VERSION AND DOTNET_VERSION)
    message(STATUS "Found .NET SDK Version: ${DOTNET_VERSION} - Configuring C# components.")
    # The src/csharp directory should ideally have its own CMakeLists.txt
    # if you want CMake to interact with it (e.g., create custom build targets for dotnet).
    # If src/csharp/CMakeLists.txt doesn't exist, this add_subdirectory will fail.
    # For C# projects, often they are built independently via 'dotnet build'.
    # If 'src/csharp/CMakeLists.txt' is not intended, you might remove this block
    # or use it to define custom targets for building the C# project.
    if(EXISTS "${CMAKE_CURRENT_LIST_DIR}/csharp/CMakeLists.txt")
        add_subdirectory(csharp)
    elseif(EXISTS "${CMAKE_CURRENT_LIST_DIR}/csharp" AND EXISTS "${CMAKE_CURRENT_LIST_DIR}/csharp/Alaris.Lean.csproj")
        # If no CMakeLists.txt in csharp, but the project file exists,
        # you could define a custom target here to build it.
        message(STATUS "C# component 'src/csharp/Alaris.Lean.csproj' found. No 'src/csharp/CMakeLists.txt', so not adding as CMake subdirectory.")
        # Example of adding a custom target (optional):
        # add_custom_target(Alaris.CSharp.Build ALL
        #     COMMAND ${DOTNET_EXECUTABLE} build "${CMAKE_CURRENT_LIST_DIR}/csharp/Alaris.Lean.csproj" --configuration ${CMAKE_BUILD_TYPE}
        #     WORKING_DIRECTORY "${CMAKE_CURRENT_LIST_DIR}/csharp"
        #     COMMENT "Building Alaris C# project"
        #     VERBATIM
        # )
        # message(STATUS "  -> Added custom target 'Alaris.CSharp.Build' for the C# project.")
    else()
        message(STATUS "C# component directory 'src/csharp' or its project file not found. Skipping C# configuration.")
    endif()
else()
    message(STATUS ".NET SDK not found (DOTNET_VERSION not set). Skipping C# components configuration.")
endif()
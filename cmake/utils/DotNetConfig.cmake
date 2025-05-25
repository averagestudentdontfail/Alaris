# Function to configure .NET build settings
function(configure_dotnet_build)
    # Detect .NET SDK
    find_program(DOTNET_EXECUTABLE dotnet)
    if(DOTNET_EXECUTABLE)
        execute_process(
            COMMAND ${DOTNET_EXECUTABLE} --version
            OUTPUT_VARIABLE DOTNET_VERSION
            OUTPUT_STRIP_TRAILING_WHITESPACE
        )
        message(STATUS "Found .NET SDK version: ${DOTNET_VERSION}")
        set(CMAKE_DOTNET_VERSION ${DOTNET_VERSION} CACHE STRING ".NET SDK version")
        
        # Set common .NET build arguments
        set(DOTNET_COMMON_ARGS
            "--configuration" "Release"
            "--verbosity" "minimal"
            "-p:GenerateFullPaths=true"
            "-p:TargetFramework=net${CMAKE_DOTNET_VERSION}"
            "-p:WarningsNotAsErrors=NU1903"
            "-p:TreatWarningsAsErrors=false"
            "-p:NoWarn=NU1903"
            "-p:SuppressWarnings=NU1903"
            PARENT_SCOPE
        )
        
        set(DOTNET_FOUND TRUE PARENT_SCOPE)
    else()
        message(WARNING ".NET SDK not found. C# components will be skipped.")
        set(DOTNET_FOUND FALSE PARENT_SCOPE)
    endif()
endfunction()

# Function to add a .NET project
function(add_dotnet_project PROJECT_NAME PROJECT_FILE)
    if(NOT DOTNET_FOUND)
        return()
    endif()
    
    add_custom_target(${PROJECT_NAME}
        COMMAND ${DOTNET_EXECUTABLE} restore "${PROJECT_FILE}"
        COMMAND ${DOTNET_EXECUTABLE} build "${PROJECT_FILE}" ${DOTNET_COMMON_ARGS}
        WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
        COMMENT "Building ${PROJECT_NAME}"
        DEPENDS "${PROJECT_FILE}"
    )
    
    # Add to main project dependencies if it exists
    if(TARGET alaris_core)
        add_dependencies(alaris_core ${PROJECT_NAME})
    endif()
endfunction() 
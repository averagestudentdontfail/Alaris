# src/CMakeLists.txt
# Source code organization for Alaris

# Add QuantLib components
add_subdirectory(quantlib)

# Add C# components if .NET is available
if(DOTNET_VERSION)
    add_subdirectory(csharp)
endif() 
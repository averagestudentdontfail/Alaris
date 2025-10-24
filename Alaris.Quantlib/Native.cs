using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Alaris.Quantlib;

/// <summary>
/// Initializes native library loading for QuantLib dependencies.
/// This ensures that libQuantLib.so.1 can be found when libNQuantLibc.so is loaded.
/// </summary>
internal static class Native
{
    private static bool _initialized = false;
    private static readonly object _lock = new object();

    /// <summary>
    /// Module initializer that runs before any other code in the assembly.
    /// Configures the native library resolver to search in the assembly directory.
    /// </summary>
    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries", Justification = "Required for native library resolution")]
    public static void Initialize()
    {
        lock (_lock)
        {
            if (_initialized)
                return;

            NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, ResolveDllImport);
            _initialized = true;
        }
    }

    /// <summary>
    /// Custom DLL import resolver that searches for native libraries in the assembly directory.
    /// </summary>
    private static IntPtr ResolveDllImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // Get the directory where the assembly is located
        string? assemblyDirectory = System.IO.Path.GetDirectoryName(assembly.Location);
        if (string.IsNullOrEmpty(assemblyDirectory))
            return IntPtr.Zero;

        // Map library names to actual file names on Linux
        string[] possibleNames = libraryName switch
        {
            "NQuantLibc" => new[] { "libNQuantLibc.so", "NQuantLibc.so", "libNQuantLibc" },
            "QuantLib" => new[] { "libQuantLib.so.1.39.0", "libQuantLib.so.1", "libQuantLib.so" },
            _ when libraryName.StartsWith("lib") => new[] { libraryName, $"{libraryName}.so" },
            _ => new[] { $"lib{libraryName}.so", $"{libraryName}.so", libraryName }
        };

        // Try each possible name in the assembly directory
        foreach (string name in possibleNames)
        {
            string fullPath = System.IO.Path.Combine(assemblyDirectory, name);
            if (System.IO.File.Exists(fullPath))
            {
                // Try to load the library
                if (NativeLibrary.TryLoad(fullPath, out IntPtr handle))
                {
                    return handle;
                }
            }
        }

        // If we couldn't find it in the assembly directory, let the default resolver try
        return IntPtr.Zero;
    }

    /// <summary>
    /// Preloads QuantLib dependencies to ensure they're available when NQuantLibc is loaded.
    /// Call this before any QuantLib functionality is used.
    /// </summary>
    public static void PreloadDependencies()
    {
        lock (_lock)
        {
            string? assemblyDirectory = System.IO.Path.GetDirectoryName(typeof(Native).Assembly.Location);
            if (string.IsNullOrEmpty(assemblyDirectory))
                return;

            // Preload QuantLib library before NQuantLibc tries to use it
            string[] quantLibPaths = new[]
            {
                System.IO.Path.Combine(assemblyDirectory, "libQuantLib.so.1.39.0"),
                System.IO.Path.Combine(assemblyDirectory, "libQuantLib.so.1"),
                System.IO.Path.Combine(assemblyDirectory, "libQuantLib.so")
            };

            foreach (string path in quantLibPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    try
                    {
                        NativeLibrary.Load(path);
                        break; // Successfully loaded, no need to try other paths
                    }
                    catch
                    {
                        // Continue to next path
                    }
                }
            }
        }
    }
}
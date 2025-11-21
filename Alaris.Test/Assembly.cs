using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Alaris.Test;

/// <summary>
/// Ensures native libraries are properly loaded before any tests execute.
/// This is critical for SWIG-generated bindings that depend on native libraries.
/// </summary>
internal static class Assembly
{
    private static bool _initialized;
    private static readonly object _lock = new();

    /// <summary>
    /// Module initializer that runs when the test assembly is loaded.
    /// Preloads QuantLib dependencies to prevent DllNotFoundException.
    /// </summary>
    [ModuleInitializer]
    public static void Initialize()
    {
        lock (_lock)
        {
            if (_initialized)
            {
                return;
            }

            PreloadNativeLibraries();
            _initialized = true;
        }
    }

    /// <summary>
    /// Preloads native libraries in the correct order to satisfy dependencies.
    /// </summary>
    private static void PreloadNativeLibraries()
    {
        // Get the directory where test assemblies are located
        string? testDirectory = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(testDirectory))
        {
            testDirectory = System.IO.Path.GetDirectoryName(typeof(Assembly).Assembly.Location);
        }

        if (string.IsNullOrEmpty(testDirectory))
        {
            Console.WriteLine("Warning: Could not determine test directory for native library loading");
            return;
        }

        // Load QuantLib first (the dependency)
        LoadLibrary(testDirectory, "libQuantLib.so.1.39.0", "libQuantLib.so.1", "libQuantLib.so");
        
        // Then load NQuantLibc (which depends on QuantLib)
        LoadLibrary(testDirectory, "libNQuantLibc.so", "NQuantLibc.so");

        Console.WriteLine("Native libraries preloaded successfully from: " + testDirectory);
    }

    /// <summary>
    /// Attempts to load a native library, trying multiple possible file names.
    /// </summary>
    private static void LoadLibrary(string directory, params string[] fileNames)
    {
        foreach (string fileName in fileNames)
        {
            string fullPath = System.IO.Path.Combine(directory, fileName);
            if (System.IO.File.Exists(fullPath))
            {
                try
                {
                    IntPtr handle = NativeLibrary.Load(fullPath);
                    if (handle != IntPtr.Zero)
                    {
                        Console.WriteLine($"Loaded: {fileName}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load {fileName}: {ex.Message}");
                }
            }
        }
        
        Console.WriteLine($"Warning: Could not find any of: {string.Join(", ", fileNames)}");
    }
}
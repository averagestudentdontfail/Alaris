using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Alaris.Double;

/// <summary>
/// Simplified native library loader for Alaris QuantLib bindings
/// Focuses on manual preloading to avoid SWIG binding complications
/// </summary>
public static class NativeLibraryLoader
{
    private static bool _isInitialized = false;
    private static readonly object _lock = new object();

    /// <summary>
    /// Initialize native library loading before using any QuantLib functionality
    /// </summary>
    public static void Initialize()
    {
        lock (_lock)
        {
            if (_isInitialized) return;

            Console.WriteLine("Initializing native library loader...");
            
            // Try manual preloading approach (more reliable than DllImportResolver)
            TryPreloadLibraries();
            
            _isInitialized = true;
            Console.WriteLine("Native library initialization completed.");
        }
    }

    /// <summary>
    /// Attempts to manually preload all required libraries
    /// </summary>
    private static void TryPreloadLibraries()
    {
        Console.WriteLine("Attempting manual library preloading...");

        // First load QuantLib core library (dependency)
        var quantlibPath = FindLibraryPath("libQuantLib.so.1");
        if (!string.IsNullOrEmpty(quantlibPath))
        {
            if (TryLoadLibrary(quantlibPath, out IntPtr quantlibHandle))
            {
                Console.WriteLine($"✅ Preloaded QuantLib from: {quantlibPath}");
            }
            else
            {
                Console.WriteLine($"⚠️ Failed to preload QuantLib from: {quantlibPath}");
            }
        }
        else
        {
            Console.WriteLine("❌ QuantLib core library not found");
        }

        // Then load the SWIG wrapper
        var wrapperPath = FindLibraryPath("libNQuantLibc.so");
        if (!string.IsNullOrEmpty(wrapperPath))
        {
            if (TryLoadLibrary(wrapperPath, out IntPtr wrapperHandle))
            {
                Console.WriteLine($"✅ Preloaded wrapper from: {wrapperPath}");
            }
            else
            {
                Console.WriteLine($"⚠️ Failed to preload wrapper from: {wrapperPath}");
            }
        }
        else
        {
            Console.WriteLine("❌ SWIG wrapper library not found");
        }
    }

    /// <summary>
    /// Searches for a library file in common locations
    /// </summary>
    private static string FindLibraryPath(string libraryFileName)
    {
        var searchPaths = new string[]
        {
            // Current working directory
            libraryFileName,
            
            // Relative paths (most common case)
            "../Alaris.Library/Native/" + libraryFileName,
            "../Alaris.Library/Runtime/" + libraryFileName,
            
            // More relative paths
            "../../Alaris.Library/Native/" + libraryFileName,
            "../../Alaris.Library/Runtime/" + libraryFileName,
            
            // Even more relative paths
            "../../../Alaris.Library/Native/" + libraryFileName,
            "../../../Alaris.Library/Runtime/" + libraryFileName,
            
            // Absolute paths (fallback)
            "/home/sunny/.project/Alaris/Alaris.Library/Native/" + libraryFileName,
            "/home/sunny/.project/Alaris/Alaris.Library/Runtime/" + libraryFileName,
            
            // Alternative library names
            libraryFileName.Replace("libQuantLib.so.1", "libQuantLib.so"),
            "../Alaris.Library/Runtime/libQuantLib.so",
            "../Alaris.Library/Runtime/libQuantLib.so.1.39.0"
        };

        foreach (var path in searchPaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    var fullPath = GetFullPath(path);
                    Console.WriteLine($"📁 Found library at: {fullPath}");
                    return fullPath;
                }
            }
            catch (Exception ex)
            {
                // Ignore path resolution errors
                Console.WriteLine($"⚠️ Error checking path '{path}': {ex.Message}");
            }
        }

        Console.WriteLine($"❌ Library {libraryFileName} not found in any search path");
        return "";
    }

    /// <summary>
    /// Safe wrapper for Path.GetFullPath
    /// </summary>
    private static string GetFullPath(string path)
    {
        try
        {
            return System.IO.Path.GetFullPath(path);
        }
        catch
        {
            return path; // Return original path if full path resolution fails
        }
    }

    /// <summary>
    /// Attempts to load a library from the specified path
    /// </summary>
    private static bool TryLoadLibrary(string path, out IntPtr handle)
    {
        handle = IntPtr.Zero;

        try
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"❌ Library file does not exist: {path}");
                return false;
            }

            Console.WriteLine($"🔄 Attempting to load: {path}");
            handle = NativeLibrary.Load(path);
            bool success = handle != IntPtr.Zero;
            
            if (success)
            {
                Console.WriteLine($"✅ Successfully loaded library");
            }
            else
            {
                Console.WriteLine($"❌ Failed to load library (handle is null)");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception loading library {path}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Verifies that QuantLib can be used
    /// </summary>
    public static bool VerifyLibraries()
    {
        Console.WriteLine("🔍 Verifying library functionality...");
        
        try
        {
            // Try to create a simple QuantLib object
            var today = new Date(1, Month.January, 2020);
            Console.WriteLine($"✅ Library verification successful - created date: {today}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Library verification failed: {ex.Message}");
            
            // Provide more detailed error information
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
            }
            
            return false;
        }
    }

    /// <summary>
    /// Displays diagnostic information about library loading
    /// </summary>
    public static void DisplayDiagnostics()
    {
        Console.WriteLine("=== 🔧 Native Library Diagnostics ===");
        
        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            Console.WriteLine($"📂 Current Directory: {currentDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"📂 Current Directory: Error getting directory - {ex.Message}");
        }
        
        try
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            Console.WriteLine($"📦 Assembly Location: {assemblyLocation}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"📦 Assembly Location: Error getting location - {ex.Message}");
        }
        
        Console.WriteLine($"💻 OS: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"🏗️ Architecture: {RuntimeInformation.OSArchitecture}");
        
        // Check environment variables
        var ldLibraryPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
        Console.WriteLine($"🔗 LD_LIBRARY_PATH: {ldLibraryPath ?? "(not set)"}");
        
        Console.WriteLine();
        Console.WriteLine("📋 Library Search Results:");
        
        // Check for library files
        var librariesToCheck = new[] { "libNQuantLibc.so", "libQuantLib.so.1", "libQuantLib.so" };
        
        foreach (var lib in librariesToCheck)
        {
            var path = FindLibraryPath(lib);
            var status = string.IsNullOrEmpty(path) ? "❌ NOT FOUND" : $"✅ Found at {path}";
            Console.WriteLine($"   {lib}: {status}");
        }
        
        Console.WriteLine("=== End Diagnostics ===");
        Console.WriteLine();
    }
}
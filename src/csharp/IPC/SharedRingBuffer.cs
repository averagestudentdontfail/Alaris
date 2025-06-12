// src/csharp/IPC/SharedRingBuffer.cs
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace Alaris.IPC
{
    #region Platform-Specific Abstractions

    /// <summary>
    /// Defines a common interface for accessing a region of memory, whether it's a
    /// .NET MemoryMappedViewAccessor on Windows or a raw pointer from mmap on Linux.
    /// </summary>
    internal interface IMemoryAccessor : IDisposable
    {
        void Write<T>(long position, ref T structure) where T : struct;
        void Read<T>(long position, out T structure) where T : struct;
        long ReadInt64(long position);
        void Write(long position, long value);
    }

    /// <summary>
    /// A wrapper around the standard MemoryMappedViewAccessor to conform to the IMemoryAccessor interface.
    /// Used on Windows.
    /// </summary>
    internal class WindowsMemoryAccessor : IMemoryAccessor
    {
        private readonly MemoryMappedViewAccessor _accessor;
        public WindowsMemoryAccessor(MemoryMappedViewAccessor accessor) { _accessor = accessor; }
        public void Dispose() => _accessor.Dispose();
        public void Read<T>(long position, out T structure) where T : struct => _accessor.Read(position, out structure);
        public long ReadInt64(long position) => _accessor.ReadInt64(position);
        public void Write<T>(long position, ref T structure) where T : struct => _accessor.Write(position, ref structure);
        public void Write(long position, long value) => _accessor.Write(position, value);
    }

    /// <summary>
    /// An accessor that operates on a raw memory pointer obtained from mmap on POSIX systems.
    /// </summary>
    internal unsafe class PosixMemoryAccessor : IMemoryAccessor
    {
        private readonly byte* _pointer;
        private readonly long _size;

        public PosixMemoryAccessor(IntPtr pointer, long size)
        {
            if (pointer == IntPtr.Zero) throw new ArgumentNullException(nameof(pointer));
            _pointer = (byte*)pointer.ToPointer();
            _size = size;
        }

        public void Read<T>(long position, out T structure) where T : struct
        {
            if (position < 0 || position + Marshal.SizeOf<T>() > _size) throw new ArgumentOutOfRangeException(nameof(position));
            structure = Marshal.PtrToStructure<T>(new IntPtr(_pointer + position));
        }

        public long ReadInt64(long position)
        {
            if (position < 0 || position + 8 > _size) throw new ArgumentOutOfRangeException(nameof(position));
            return *(long*)(_pointer + position);
        }

        public void Write<T>(long position, ref T structure) where T : struct
        {
            if (position < 0 || position + Marshal.SizeOf<T>() > _size) throw new ArgumentOutOfRangeException(nameof(position));
            Marshal.StructureToPtr(structure, new IntPtr(_pointer + position), false);
        }

        public void Write(long position, long value)
        {
            if (position < 0 || position + 8 > _size) throw new ArgumentOutOfRangeException(nameof(position));
            *(long*)(_pointer + position) = value;
        }

        // The owner (SharedRingBuffer) is responsible for calling munmap, so this is a no-op.
        public void Dispose() { }
    }

    /// <summary>
    /// P/Invoke wrapper for POSIX shared memory functions.
    /// </summary>
    internal static class PosixSharedMemory
    {
        [DllImport("libc", SetLastError = true)]
        internal static extern int shm_open(string name, int oflag, int mode);
        [DllImport("libc", SetLastError = true)]
        internal static extern int shm_unlink(string name);
        [DllImport("libc", SetLastError = true)]
        internal static extern IntPtr mmap(IntPtr addr, long length, int prot, int flags, int fd, long offset);
        [DllImport("libc", SetLastError = true)]
        internal static extern int munmap(IntPtr addr, long length);
        [DllImport("libc", SetLastError = true)]
        internal static extern int ftruncate(int fd, long length);
        [DllImport("libc", SetLastError = true)]
        internal static extern int close(int fd);

        internal const int O_CREAT = 64;
        internal const int O_RDWR = 2;
        internal const int PROT_READ = 1;
        internal const int PROT_WRITE = 2;
        internal const int MAP_SHARED = 1;
    }

    #endregion

    public class SharedRingBuffer<T> : IDisposable where T : struct
    {
        private readonly int _elementSize;
        private readonly int _bufferSize;
        private readonly long _totalSize;
        private readonly string _name;
        private bool _disposed = false;

        // Platform-specific handles
        private MemoryMappedFile? _mmf; // Windows only
        private IMemoryAccessor? _accessor; // Made nullable to fix CS8618
        
        // POSIX-specific handles
        private int _posixShmFd = -1;
        private IntPtr _posixMmapPtr = IntPtr.Zero;

        private const int HEADER_SIZE = 16; // Two 64-bit atomic counters (read/write indices)

        public SharedRingBuffer(string name, int bufferSize, bool create = false)
        {
            if (!name.StartsWith("/")) { _name = "/" + name; } else { _name = name; }

            _bufferSize = bufferSize;
            _elementSize = Marshal.SizeOf<T>();
            _totalSize = HEADER_SIZE + (_elementSize * bufferSize);

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    InitializeWindows(_totalSize, create);
                }
                else
                {
                    InitializePosix(_totalSize, create);
                }
            }
            catch (Exception ex)
            {
                // Cleanup any partially initialized resources
                Cleanup();
                throw new InvalidOperationException($"Failed to initialize shared ring buffer '{_name}': {ex.Message}", ex);
            }

            // Ensure _accessor is initialized after successful initialization
            if (_accessor == null)
            {
                Cleanup();
                throw new InvalidOperationException($"Failed to properly initialize memory accessor for '{_name}'");
            }
        }

        private void InitializeWindows(long totalSize, bool create)
        {
            if (create)
            {
                _mmf = MemoryMappedFile.CreateNew(_name, totalSize, MemoryMappedFileAccess.ReadWrite);
            }
            else
            {
                _mmf = MemoryMappedFile.OpenExisting(_name, MemoryMappedFileRights.ReadWrite);
            }
            _accessor = new WindowsMemoryAccessor(_mmf.CreateViewAccessor(0, totalSize, MemoryMappedFileAccess.ReadWrite));
        }

        private void InitializePosix(long totalSize, bool create)
        {
            const int mode = 0x1B0; // 0660 octal
            if (create)
            {
                _posixShmFd = PosixSharedMemory.shm_open(_name, PosixSharedMemory.O_CREAT | PosixSharedMemory.O_RDWR, mode);
                if (_posixShmFd == -1) throw new IOException($"shm_open create failed for '{_name}'. Errno: {Marshal.GetLastWin32Error()}");
                if (PosixSharedMemory.ftruncate(_posixShmFd, totalSize) == -1)
                {
                    PosixSharedMemory.close(_posixShmFd);
                    PosixSharedMemory.shm_unlink(_name);
                    throw new IOException($"ftruncate failed for '{_name}'. Errno: {Marshal.GetLastWin32Error()}");
                }
            }
            else
            {
                _posixShmFd = PosixSharedMemory.shm_open(_name, PosixSharedMemory.O_RDWR, mode);
                if (_posixShmFd == -1) throw new IOException($"shm_open existing failed for '{_name}'. Errno: {Marshal.GetLastWin32Error()}. Ensure producer is running.");
            }

            _posixMmapPtr = PosixSharedMemory.mmap(IntPtr.Zero, totalSize, PosixSharedMemory.PROT_READ | PosixSharedMemory.PROT_WRITE, PosixSharedMemory.MAP_SHARED, _posixShmFd, 0);
            if (_posixMmapPtr == new IntPtr(-1))
            {
                PosixSharedMemory.close(_posixShmFd);
                if (create) PosixSharedMemory.shm_unlink(_name);
                throw new IOException($"mmap failed for '{_name}'. Errno: {Marshal.GetLastWin32Error()}");
            }
            
            _accessor = new PosixMemoryAccessor(_posixMmapPtr, totalSize);
        }

        public bool TryWrite(T item)
        {
            if (_disposed || _accessor == null) return false;

            long currentWrite = _accessor.ReadInt64(0);
            long currentRead = _accessor.ReadInt64(8);

            if (currentWrite - currentRead >= _bufferSize) return false;

            var position = (int)(currentWrite % _bufferSize);
            var offset = HEADER_SIZE + (position * _elementSize);

            _accessor.Write(offset, ref item);
            
            // This is not a true atomic operation across processes without a lock/semaphore,
            // but it's the lock-free approach used in the original C++ code.
            // A memory barrier ensures writes are visible before the index is updated.
            Thread.MemoryBarrier(); 
            _accessor.Write(0, currentWrite + 1);

            return true;
        }

        public bool TryRead(out T item)
        {
            item = default;
            if (_disposed || _accessor == null) return false;

            long currentRead = _accessor.ReadInt64(8);
            long currentWrite = _accessor.ReadInt64(0);

            if (currentRead == currentWrite) return false;

            var position = (int)(currentRead % _bufferSize);
            var offset = HEADER_SIZE + (position * _elementSize);
            
            _accessor.Read(offset, out item);

            Thread.MemoryBarrier();
            _accessor.Write(8, currentRead + 1);

            return true;
        }

        public int Size => _accessor != null ? (int)(_accessor.ReadInt64(0) - _accessor.ReadInt64(8)) : 0;
        public bool IsEmpty => Size == 0;
        public bool IsFull => Size >= _bufferSize;
        public double Utilization => _bufferSize > 0 ? (double)Size / _bufferSize : 0.0;

        private void Cleanup()
        {
            _accessor?.Dispose();
            _mmf?.Dispose();

            if (!OperatingSystem.IsWindows())
            {
                if (_posixMmapPtr != IntPtr.Zero && _posixMmapPtr != new IntPtr(-1))
                {
                    PosixSharedMemory.munmap(_posixMmapPtr, _totalSize);
                    _posixMmapPtr = IntPtr.Zero;
                }
                if (_posixShmFd != -1)
                {
                    PosixSharedMemory.close(_posixShmFd);
                    _posixShmFd = -1;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Cleanup();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
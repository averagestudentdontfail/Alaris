// src/csharp/IPC/SharedRingBuffer.cs
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace Alaris.IPC
{
    /// <summary>
    /// Platform-agnostic P/Invoke wrapper for POSIX shared memory functions (shm_open, mmap, etc.).
    /// This is necessary because MemoryMappedFile on .NET for Unix-like systems maps regular files,
    /// not POSIX shared memory objects, which is what the C++ component creates.
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

        // Constants for shm_open flags
        internal const int O_CREAT = 0x40;  // 64 in decimal
        internal const int O_RDWR = 2;

        // Constants for mmap protection
        internal const int PROT_READ = 1;
        internal const int PROT_WRITE = 2;

        // Constants for mmap flags
        internal const int MAP_SHARED = 1;
    }

    public class SharedRingBuffer<T> : IDisposable where T : struct
    {
        private readonly int _elementSize;
        private readonly int _bufferSize;
        private readonly string _name;
        private bool _disposed = false;

        // Platform-specific handles
        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _accessor;
        private int _posixShmFd = -1;
        private IntPtr _posixMmapPtr = IntPtr.Zero;
        private long _totalSize;

        private const int HEADER_SIZE = 16; // Two 64-bit atomic counters

        public SharedRingBuffer(string name, int bufferSize, bool create = false)
        {
            // POSIX shared memory names must start with a slash
            if (!name.StartsWith("/"))
            {
                _name = "/" + name;
            }
            else
            {
                _name = name;
            }

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
                throw new InvalidOperationException($"Failed to initialize shared ring buffer '{_name}': {ex.Message}", ex);
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
            _accessor = _mmf.CreateViewAccessor(0, totalSize, MemoryMappedFileAccess.ReadWrite);
        }

        private unsafe void InitializePosix(long totalSize, bool create)
        {
            const int mode = 0x1B0; // 0660 in octal - Read/write permissions for owner and group
            
            if (create)
            {
                // Create the POSIX shared memory object
                _posixShmFd = PosixSharedMemory.shm_open(_name, PosixSharedMemory.O_CREAT | PosixSharedMemory.O_RDWR, mode);
                if (_posixShmFd == -1)
                {
                    var errno = Marshal.GetLastWin32Error();
                    throw new IOException($"shm_open failed for '{_name}'. Error code: {errno}");
                }
                
                // Set the size of the shared memory object
                if (PosixSharedMemory.ftruncate(_posixShmFd, totalSize) == -1)
                {
                    var errno = Marshal.GetLastWin32Error();
                    PosixSharedMemory.close(_posixShmFd);
                    PosixSharedMemory.shm_unlink(_name);
                    throw new IOException($"ftruncate failed for '{_name}'. Error code: {errno}");
                }
            }
            else
            {
                // Open an existing POSIX shared memory object
                _posixShmFd = PosixSharedMemory.shm_open(_name, PosixSharedMemory.O_RDWR, mode);
                if (_posixShmFd == -1)
                {
                    var errno = Marshal.GetLastWin32Error();
                    throw new IOException($"shm_open (existing) failed for '{_name}'. Error code: {errno}. Ensure the producer process is running.");
                }
            }

            // Map the shared memory object into the process's address space
            _posixMmapPtr = PosixSharedMemory.mmap(
                IntPtr.Zero, 
                totalSize, 
                PosixSharedMemory.PROT_READ | PosixSharedMemory.PROT_WRITE, 
                PosixSharedMemory.MAP_SHARED, 
                _posixShmFd, 
                0);
                
            if (_posixMmapPtr == new IntPtr(-1))
            {
                var errno = Marshal.GetLastWin32Error();
                PosixSharedMemory.close(_posixShmFd);
                if (create)
                {
                    PosixSharedMemory.shm_unlink(_name);
                }
                throw new IOException($"mmap failed for '{_name}'. Error code: {errno}");
            }

            // Create an unmanaged memory stream over the mapped memory
            var unmanagedStream = new UnmanagedMemoryAccessor(_posixMmapPtr, totalSize, MemoryMappedFileAccess.ReadWrite);
            _accessor = unmanagedStream;
        }

        public bool TryWrite(T item)
        {
            if (_disposed || _accessor == null) return false;

            long writeIndex = _accessor.ReadInt64(0);
            long readIndex = _accessor.ReadInt64(8);

            if (writeIndex - readIndex >= _bufferSize)
            {
                return false; // Buffer is full
            }

            int position = (int)(writeIndex % _bufferSize);
            int offset = HEADER_SIZE + (position * _elementSize);

            _accessor.Write(offset, ref item);
            
            // Atomic increment
            Interlocked.Increment(ref writeIndex);
            _accessor.Write(0, writeIndex);

            return true;
        }

        public bool TryRead(out T item)
        {
            item = default;
            if (_disposed || _accessor == null) return false;

            long readIndex = _accessor.ReadInt64(8);
            long writeIndex = _accessor.ReadInt64(0);

            if (readIndex == writeIndex)
            {
                return false; // Buffer is empty
            }

            int position = (int)(readIndex % _bufferSize);
            int offset = HEADER_SIZE + (position * _elementSize);

            _accessor.Read(offset, out item);
            
            // Atomic increment
            Interlocked.Increment(ref readIndex);
            _accessor.Write(8, readIndex);

            return true;
        }

        public int Size => _accessor != null ? (int)(_accessor.ReadInt64(0) - _accessor.ReadInt64(8)) : 0;
        public bool IsEmpty => Size == 0;
        public bool IsFull => Size >= _bufferSize;
        public double Utilization => _bufferSize > 0 ? (double)Size / _bufferSize : 0.0;

        public void Dispose()
        {
            if (!_disposed)
            {
                _accessor?.Dispose();
                _mmf?.Dispose();

                if (!OperatingSystem.IsWindows())
                {
                    if (_posixMmapPtr != IntPtr.Zero && _posixMmapPtr != new IntPtr(-1))
                    {
                        PosixSharedMemory.munmap(_posixMmapPtr, _totalSize);
                    }
                    if (_posixShmFd != -1)
                    {
                        PosixSharedMemory.close(_posixShmFd);
                    }
                }
                
                _disposed = true;
            }
        }
    }

    // Helper class for unmanaged memory access
    internal unsafe class UnmanagedMemoryAccessor : MemoryMappedViewAccessor
    {
        private readonly byte* _pointer;
        private readonly long _size;

        public UnmanagedMemoryAccessor(IntPtr pointer, long size, MemoryMappedFileAccess access)
        {
            _pointer = (byte*)pointer.ToPointer();
            _size = size;
        }

        public override long Capacity => _size;

        public override void Write<T>(long position, ref T structure)
        {
            if (position < 0 || position + Marshal.SizeOf<T>() > _size)
                throw new ArgumentOutOfRangeException(nameof(position));

            Marshal.StructureToPtr(structure, new IntPtr(_pointer + position), false);
        }

        public override int ReadArray<T>(long position, T[] array, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Read<T>(long position, out T structure)
        {
            if (position < 0 || position + Marshal.SizeOf<T>() > _size)
                throw new ArgumentOutOfRangeException(nameof(position));

            structure = Marshal.PtrToStructure<T>(new IntPtr(_pointer + position));
        }

        public override long ReadInt64(long position)
        {
            if (position < 0 || position + 8 > _size)
                throw new ArgumentOutOfRangeException(nameof(position));

            return *(long*)(_pointer + position);
        }

        public override void Write(long position, long value)
        {
            if (position < 0 || position + 8 > _size)
                throw new ArgumentOutOfRangeException(nameof(position));

            *(long*)(_pointer + position) = value;
        }

        public override void WriteArray<T>(long position, T[] array, int offset, int count)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            // No disposal needed as we don't own the memory
        }
    }
}
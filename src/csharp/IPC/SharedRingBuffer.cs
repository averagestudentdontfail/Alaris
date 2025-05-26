// src/csharp/IPC/SharedRingBuffer.cs
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace Alaris.IPC
{
    public class SharedRingBuffer<T> : IDisposable where T : struct
    {
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly int _elementSize;
        private readonly int _bufferSize;
        private readonly string _name;
        private bool _disposed = false;

        private const int HEADER_SIZE = 16; // Two 64-bit atomic counters
        
        public SharedRingBuffer(string name, int bufferSize, bool create = false)
        {
            _name = name;
            _bufferSize = bufferSize;
            _elementSize = Marshal.SizeOf<T>();
            
            long totalSize = HEADER_SIZE + (_elementSize * bufferSize);

            try
            {
                if (create)
                {
                    _mmf = MemoryMappedFile.CreateNew(_name, totalSize, MemoryMappedFileAccess.ReadWrite);
                }
                else
                {
                    // Cross-platform compatible way to open existing memory mapped file
                    if (OperatingSystem.IsWindows())
                    {
                        _mmf = MemoryMappedFile.OpenExisting(_name, MemoryMappedFileRights.ReadWrite);
                    }
                    else
                    {
                        // On Linux/Unix, try to open as if it were created (fallback)
                        _mmf = MemoryMappedFile.CreateOrOpen(_name, totalSize, MemoryMappedFileAccess.ReadWrite);
                    }
                }

                _accessor = _mmf.CreateViewAccessor(0, totalSize, MemoryMappedFileAccess.ReadWrite);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize shared ring buffer '{name}': {ex.Message}", ex);
            }
        }

        public bool TryWrite(T item)
        {
            if (_disposed) return false;

            // Read current indices
            long writeIndex = _accessor.ReadInt64(0);
            long readIndex = _accessor.ReadInt64(8);

            // Check if buffer is full
            if (writeIndex - readIndex >= _bufferSize)
            {
                return false;
            }

            // Calculate position in buffer
            int position = (int)(writeIndex % _bufferSize);
            int offset = HEADER_SIZE + (position * _elementSize);

            // Write the item using byte array marshaling
            byte[] itemBytes = StructToByteArray(item);
            for (int i = 0; i < itemBytes.Length; i++)
            {
                _accessor.Write(offset + i, itemBytes[i]);
            }

            // Update write index atomically
            long newWriteIndex = Interlocked.Increment(ref writeIndex);
            _accessor.Write(0, newWriteIndex);

            return true;
        }

        public bool TryRead(out T item)
        {
            item = default(T);
            if (_disposed) return false;

            // Read current indices
            long readIndex = _accessor.ReadInt64(8);
            long writeIndex = _accessor.ReadInt64(0);

            // Check if buffer is empty
            if (readIndex == writeIndex)
            {
                return false;
            }

            // Calculate position in buffer
            int position = (int)(readIndex % _bufferSize);
            int offset = HEADER_SIZE + (position * _elementSize);

            // Read the item using byte array marshaling
            byte[] itemBytes = new byte[_elementSize];
            for (int i = 0; i < _elementSize; i++)
            {
                itemBytes[i] = _accessor.ReadByte(offset + i);
            }
            item = ByteArrayToStruct<T>(itemBytes);

            // Update read index atomically
            long newReadIndex = Interlocked.Increment(ref readIndex);
            _accessor.Write(8, newReadIndex);

            return true;
        }

        private static byte[] StructToByteArray<U>(U obj) where U : struct
        {
            int size = Marshal.SizeOf<U>();
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(obj, ptr, false);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return arr;
        }

        private static U ByteArrayToStruct<U>(byte[] bytes) where U : struct
        {
            int size = Marshal.SizeOf<U>();
            if (bytes.Length != size)
            {
                throw new ArgumentException($"Byte array length {bytes.Length} does not match struct size {size}");
            }

            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(bytes, 0, ptr, size);
                return Marshal.PtrToStructure<U>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public int Size
        {
            get
            {
                if (_disposed) return 0;
                long writeIndex = _accessor.ReadInt64(0);
                long readIndex = _accessor.ReadInt64(8);
                return (int)(writeIndex - readIndex);
            }
        }

        public bool IsEmpty => Size == 0;
        public bool IsFull => Size >= _bufferSize;
        public double Utilization => (double)Size / _bufferSize;

        public void Dispose()
        {
            if (!_disposed)
            {
                _accessor?.Dispose();
                _mmf?.Dispose();
                _disposed = true;
            }
        }
    }
}
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
                    _mmf = MemoryMappedFile.OpenExisting(_name, MemoryMappedFileRights.ReadWrite);
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

            long writeIndex = Interlocked.Read(ref _accessor.ReadInt64(0));
            long readIndex = Interlocked.Read(ref _accessor.ReadInt64(8));

            // Check if buffer is full
            if (writeIndex - readIndex >= _bufferSize)
            {
                return false;
            }

            // Calculate position in buffer
            int position = (int)(writeIndex % _bufferSize);
            int offset = HEADER_SIZE + (position * _elementSize);

            // Write the item
            _accessor.Write(offset, ref item);

            // Update write index atomically
            Interlocked.Increment(ref _accessor.ReadInt64(0));

            return true;
        }

        public bool TryRead(out T item)
        {
            item = default(T);
            if (_disposed) return false;

            long readIndex = Interlocked.Read(ref _accessor.ReadInt64(8));
            long writeIndex = Interlocked.Read(ref _accessor.ReadInt64(0));

            // Check if buffer is empty
            if (readIndex == writeIndex)
            {
                return false;
            }

            // Calculate position in buffer
            int position = (int)(readIndex % _bufferSize);
            int offset = HEADER_SIZE + (position * _elementSize);

            // Read the item
            item = _accessor.ReadStruct<T>(offset);

            // Update read index atomically
            Interlocked.Increment(ref _accessor.ReadInt64(8));

            return true;
        }

        public int Size
        {
            get
            {
                if (_disposed) return 0;
                long writeIndex = Interlocked.Read(ref _accessor.ReadInt64(0));
                long readIndex = Interlocked.Read(ref _accessor.ReadInt64(8));
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
using System.IO.MemoryMappedFiles;

namespace BetterHexViewer.Uno;

/// <summary>Read-only 64-bit file access with a bounded fallback for runtimes without MMF.</summary>
internal sealed class HexFileAccessor : IDisposable
{
    private readonly MemoryMappedFile? _mapping;
    private readonly MemoryMappedViewAccessor? _view;
    private readonly FileStream? _stream;
    private readonly object _gate = new();
    private readonly byte[] _cache = new byte[64 * 1024];
    private long _cacheStart = -1;
    private int _cacheLength;

    private HexFileAccessor(MemoryMappedFile mapping, MemoryMappedViewAccessor view)
        => (_mapping, _view) = (mapping, view);
    private HexFileAccessor(FileStream stream) => _stream = stream;

    internal static HexFileAccessor Open(string path, bool useMemoryMapping = true)
    {
        if (useMemoryMapping)
        {
            try
            {
                var mapping = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                try { return new HexFileAccessor(mapping, mapping.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read)); }
                catch { mapping.Dispose(); throw; }
            }
            catch (PlatformNotSupportedException) { }
        }
        return new HexFileAccessor(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    internal byte ReadByte(long offset)
    {
        if (_view != null) return _view.ReadByte(offset);
        lock (_gate)
        {
            if (offset < _cacheStart || offset >= _cacheStart + _cacheLength)
            {
                _cacheStart = offset / _cache.Length * _cache.Length;
                _stream!.Position = _cacheStart;
                _cacheLength = (int)Math.Min(_cache.Length, _stream.Length - _cacheStart);
                _stream.ReadExactly(_cache.AsSpan(0, _cacheLength));
            }
            return _cache[checked((int)(offset - _cacheStart))];
        }
    }

    internal void ReadArray(long position, byte[] buffer, int offset, int count)
    {
        if (_view != null) { _view.ReadArray(position, buffer, offset, count); return; }
        lock (_gate)
        {
            _stream!.Position = position;
            _stream.ReadExactly(buffer.AsSpan(offset, count));
        }
    }

    public void Dispose()
    {
        _view?.Dispose();
        _mapping?.Dispose();
        _stream?.Dispose();
    }
}

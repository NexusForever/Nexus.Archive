using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace Nexus.Archive;

public class MemoryMappedViewableData : IViewableData
{
    private FileAccess _fileAccessMode;
    private MemoryMappedFileAccess _memoryMappedFileAccessMode;
    private MemoryMappedFile _file;
    private Stream _fileStream;

    public MemoryMappedViewableData(string fileName, FileAccess fileAccess)
    {
        FileName = fileName;
        _fileAccessMode = fileAccess;
        switch (fileAccess)
        {
            case FileAccess.Read:
                _memoryMappedFileAccessMode = MemoryMappedFileAccess.Read;
                break;
            case FileAccess.ReadWrite:
                _memoryMappedFileAccessMode = MemoryMappedFileAccess.ReadWrite;
                break;
            default:
                throw new NotSupportedException("Only read, or Read/Write is supported");
        }

        var fileStream = System.IO.File.Open(fileName, FileMode.Open, _fileAccessMode, FileShare.ReadWrite);
        _fileStream = fileStream;
        _file = MemoryMappedFile.CreateFromFile(fileStream, null, 0, _memoryMappedFileAccessMode, HandleInheritability.Inheritable, false);
    }

    public string FileName { get; }
    public Stream CreateView(long offset, long length)
    {
        return _file.CreateViewStream(offset, length, _memoryMappedFileAccessMode);
    }

    public long Length => _fileStream.Length;

    public void Dispose()
    {
        _file?.Dispose();
        _fileStream?.Dispose();
    }
}
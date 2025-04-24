using System;
using System.IO;

namespace Nexus.Archive
{
    public interface IArchiveFileEntry : IArchiveFilesystemEntry
    {
        ArchiveFileFlags Flags { get; }
        DateTimeOffset WriteTime { get; }
        long UncompressedSize { get; }
        long CompressedSize { get; }
        byte[] Hash { get; }
        uint Reserved { get; }
        Stream OpenRead(bool decompress = true);
    }
}
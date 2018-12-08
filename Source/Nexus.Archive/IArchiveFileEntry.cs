using System;

namespace Nexus.Archive
{
    public interface IArchiveFileEntry : IArchiveFilesystemEntry
    {
        ArchiveFileFlags Flags { get; }
        DateTimeOffset WriteTime { get; }
        long UncompressedSize { get; }
        long CompressedSize { get; }
        byte[] Hash { get; }
        uint UnknownData { get; }
    }
}
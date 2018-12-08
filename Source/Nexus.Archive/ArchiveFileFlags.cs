using System;

namespace Nexus.Archive
{
    [Flags]
    public enum ArchiveFileFlags : uint
    {
        File = 1,
        CompressedDeflate = 2,
        CompressedLzma = 4
    }
}
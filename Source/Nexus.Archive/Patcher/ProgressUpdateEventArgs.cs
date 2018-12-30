using System;

namespace Nexus.Archive.Patcher
{
    public class ProgressUpdateEventArgs : EventArgs
    {
        public ProgressUpdateEventArgs(IArchiveFileEntry fileEntry, long bytesWritten, long length)
        {
            FileEntry = fileEntry;
            BytesWritten = bytesWritten;
            Length = length;
        }
        public IArchiveFileEntry FileEntry { get; }
        public long BytesWritten { get; }
        public long Length { get; }
    }
}
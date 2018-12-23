
using System.Collections.Generic;
using System.IO;
using Nexus.Archive;

namespace Nexus.Patch.Server.Services
{
    public interface IGameDataFiles
    {
        IEnumerable<IndexFile> IndexFiles { get; }
        IEnumerable<ArchiveFile> ArchiveFiles { get; }

        int Build { get; }
        IEnumerable<FileEntry> OtherFiles { get; }

        Stream OpenHash(byte[] hash);
        byte[] GetHash(string fileName);
    }

    public class FileEntry
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public byte[] Hash { get; set; }
    }
}

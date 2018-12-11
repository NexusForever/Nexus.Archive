
using System.Collections.Generic;
using Nexus.Archive;

namespace Nexus.Patch.Server.Services
{
    public interface IGameDataFiles
    {
        IEnumerable<IndexFile> IndexFiles { get; }
        IEnumerable<ArchiveFile> ArchiveFiles { get; }

        int Build { get; }
        IEnumerable<(string filePath, string alias, byte[] hash)> OtherFiles { get; }
    }
}

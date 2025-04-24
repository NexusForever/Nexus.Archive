using System.Collections.Generic;

namespace Nexus.Archive;

public interface IArchiveFolderEntry : IArchiveFilesystemEntry
{
    IEnumerable<IArchiveFilesystemEntry> EnumerateChildren(bool recurse = false);
    IEnumerable<IArchiveFolderEntry> EnumerateFolders(bool recurse = false);
    IEnumerable<IArchiveFileEntry> EnumerateFiles(bool recurse = false);
}
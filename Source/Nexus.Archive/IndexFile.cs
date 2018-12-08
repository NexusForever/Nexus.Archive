using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;

namespace Nexus.Archive
{
    [ArchiveFileType(ArchiveType.Index)]
    public sealed class IndexFile : ArchiveFileBase
    {
        public IndexFile(string fileName, MemoryMappedFile file, ArchiveHeader header,
            BlockInfoHeader[] blockInfoHeaders, RootIndexBlock rootIndex)
            : base(fileName, file, header, blockInfoHeaders, rootIndex)
        {
            RootFolder = new FolderEntry("", this,
                new BinaryReader(GetBlockView(rootIndex.BlockIndex), Encoding.UTF8));
        }

        public IArchiveFolderEntry RootFolder { get; }

        public IArchiveFilesystemEntry FindEntry(string archivePath)
        {
            var parts = archivePath.TrimStart(Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
                .Split(Path.DirectorySeparatorChar);
            if (parts.Length == 0) return RootFolder;
            string currentPath = null;
            var currentFolder = RootFolder;
            for (var x = 0; x < parts.Length; x++)
            {
                currentPath = currentPath == null ? parts[x] : Path.Combine(currentPath, parts[x]);
                var match = currentFolder.EnumerateChildren().FirstOrDefault(i =>
                    string.Equals(i.Path, currentPath, StringComparison.OrdinalIgnoreCase));
                if (x == parts.Length - 1 && match != null) return match;
                if (match is IArchiveFolderEntry entry)
                {
                    currentFolder = entry;
                    continue;
                }

                return null;
            }

            // shouldn't really make it here... but whatever.
            return currentFolder;
        }
    }
}
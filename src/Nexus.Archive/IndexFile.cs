using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DotNet.Globbing;

namespace Nexus.Archive;

[ArchiveFileType(ArchiveType.Index)]
public sealed class IndexFile : ArchiveFileBase
{
    public IndexFile(IViewableData file, ArchiveHeader header,
        BlockInfoHeader[] blockInfoHeaders, RootIndexBlock rootIndex)
        : base(file, header, blockInfoHeaders, rootIndex)
    {
        RootFolder = new FolderEntry("", this,
            new BinaryReader(GetBlockView(rootIndex.BlockIndex), Encoding.UTF8));

        using (var fileStream = System.IO.File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var sha = SHA1.Create())
        {
            _fileHash = sha.ComputeHash(fileStream);
        }
    }

    private readonly byte[] _fileHash;
    private Archive _archive;

    public byte[] FileHash => _fileHash.ToArray();

    public IEnumerable<IArchiveFilesystemEntry> GetFilesystemEntries()
    {
        return RootFolder.EnumerateChildren(true);
    }
    public IEnumerable<IArchiveFilesystemEntry> GetFilesystemEntries(string searchPattern)
    {
        return SearchWithGlob(GetFilesystemEntries(), searchPattern);
    }

    public IEnumerable<IArchiveFolderEntry> GetFolders()
    {
        return RootFolder.EnumerateFolders(true);
    }

    public IEnumerable<IArchiveFolderEntry> GetFolders(string searchPattern)
    {
        return SearchWithGlob(GetFolders(), searchPattern);
    }

    public IEnumerable<IArchiveFileEntry> GetFiles()
    {
        return RootFolder.EnumerateFiles(true);
    }

    public IEnumerable<IArchiveFileEntry> GetFiles(string searchPattern)
    {
        return SearchWithGlob(GetFiles(), searchPattern);
    }

    private static IEnumerable<T> SearchWithGlob<T>(IEnumerable<T> items, string searchPattern)
        where T : IArchiveFilesystemEntry
    {
        var glob = ParseGlob(searchPattern);
        foreach (var item in items.Where(i => glob.IsMatch(i.Path)))
            yield return item;
    }

    private static Glob ParseGlob(string glob)
    {
        var options = new GlobOptions()
        {
            Evaluation =
            {
                CaseInsensitive = true
            }
        };
        return Glob.Parse(glob, options);
    }

    public IArchiveFolderEntry RootFolder { get; }

    public Archive Archive
    {
        get => _archive;
        internal set
        {
            _archive = value;
            RootFolder.Archive = value;
        }
    }

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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Nexus.Archive;

public class FolderEntry : FilesystemEntry, IArchiveFolderEntry
{
    private const int FolderEntrySize = 8;
    private readonly FileEntry[] _files;
    private readonly FolderPointer[] _folderPointers;
    private readonly IndexFile _index;
    private readonly Lazy<List<IArchiveFilesystemEntry>> _lazyChildrenReader;

    public FolderEntry(string name, IndexFile indexFile, BinaryReader reader)
    {
        _index = indexFile;
        Path = name;
        _lazyChildrenReader = new Lazy<List<IArchiveFilesystemEntry>>(ReadChildren, true);
        Subdirectories = reader.ReadInt32();
        Files = reader.ReadInt32();
        var dataSize = FolderPointer.Size * Subdirectories + FileEntry.Size * Files + 8;
        var stringLength = reader.BaseStream.Length - dataSize;
        _folderPointers = new FolderPointer[Subdirectories];
        _files = new FileEntry[Files];
        for (var x = 0; x < Subdirectories; x++) _folderPointers[x] = FolderPointer.FromReader(reader);

        for (var x = 0; x < Files; x++)
        {
            _files[x] = FileEntry.FromReader(reader);
            if (((int)_files[x].Flags & 1) != 1)
                Debugger.Break();
        }

        foreach (var folder in _folderPointers)
            folder.Name = GetChildPath(ReadName((int)folder.NameOffset));

        foreach (var file in _files)
            file.Path = GetChildPath(ReadName(file.NameOffset));
        return;

        string ReadName(int itemNameOffset)
        {
            reader.BaseStream.Seek(dataSize + itemNameOffset, SeekOrigin.Begin);
            var nameBuilder = new StringBuilder();
            char next;
            while ((next = reader.ReadChar()) != '\0') nameBuilder.Append(next);

            return nameBuilder.ToString();
        }
    }

    public override Archive Archive
    {
        get => base.Archive;
        set
        {
            base.Archive = value;
            if (!_lazyChildrenReader.IsValueCreated) return;
            foreach (var entry in Children)
                entry.Archive = value;
        }
    }

    public int Subdirectories { get; }
    public int Files { get; }
    private object _lock = new object();
    public IEnumerable<IArchiveFilesystemEntry> Children => _lazyChildrenReader.Value;

    public IEnumerable<IArchiveFilesystemEntry> EnumerateChildren(bool recurse = false)
    {
        foreach (var child in Children)
        {
            yield return child;
            if (!recurse || !(child is IArchiveFolderEntry folder))
                continue;

            foreach (var innerChild in folder.EnumerateChildren(true)) yield return innerChild;
        }
    }

    public IEnumerable<IArchiveFolderEntry> EnumerateFolders(bool recurse = false)
    {
        return EnumerateChildren(recurse).OfType<IArchiveFolderEntry>();
    }

    public IEnumerable<IArchiveFileEntry> EnumerateFiles(bool recurse = false)
    {
        return EnumerateChildren(recurse).OfType<IArchiveFileEntry>();
    }

    private string GetChildPath(string name)
    {
        if (string.IsNullOrWhiteSpace(Path)) return name;
        return System.IO.Path.Combine(Path, name);
    }

    public override string ToString()
    {
        return Path;
    }

    private List<IArchiveFilesystemEntry> ReadChildren()
    {
        lock (_lock)
        {
            var allFiles = new List<IArchiveFilesystemEntry>();
            foreach (var folderPointer in _folderPointers)
            {
                var file = new FolderEntry(folderPointer.Name, _index,
                    new BinaryReader(_index.GetBlockView(folderPointer.FolderBlock), Encoding.UTF8));
                allFiles.Add(file);
                file.Archive = Archive;
            }

            foreach (var file in _files)
            {
                file.Archive = Archive;
            }

            allFiles.AddRange(_files);
            return allFiles;
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;

namespace Nexus.Archive
{
    [ArchiveFileType(ArchiveType.Archive)]
    public sealed class ArchiveFile : ArchiveFileBase
    {
        private readonly List<FileDataEntry> _dataEntries;

        public ArchiveFile(IViewableData file, ArchiveHeader header,
            BlockInfoHeader[] blockInfoHeaders, RootIndexBlock rootIndex)
            : base(file, header, blockInfoHeaders, rootIndex)
        {
            _dataEntries = ReadDataHeaders(GetBlockReader(rootIndex.BlockIndex));
        }

        private List<FileDataEntry> ReadDataHeaders(BinaryReader getBlockView)
        {
            var dataEntries = new List<FileDataEntry>();
            for (var x = 0; x < RootIndex.BlockCount; x++)
            {
                var thisDataEntry = FileDataEntry.FromReader(getBlockView);
                var insertIndex = dataEntries.BinarySearch(thisDataEntry, BlockHashComparer.Instance);
                if (insertIndex < 0)
                    insertIndex = ~insertIndex;
                dataEntries.Insert(insertIndex, thisDataEntry);
            }

            return dataEntries;
        }

        public IEnumerable<FileDataEntry> FileData => _dataEntries.AsReadOnly();

        public FileDataEntry GetFileDataEntryByHash(byte[] hash)
        {
            if (hash.Length != 20) throw new ArgumentException("Hash must be exactly 20 bytes.", nameof(hash));
            var index = _dataEntries.BinarySearch(FileDataEntry.ForSearch(hash), BlockHashComparer.Instance);
            if (index < 0) return null;
            return _dataEntries[index];
        }


        public Stream Open(IArchiveFileEntry fileEntry)
        {
            var dataEntry = GetFileDataEntryByHash(fileEntry.Hash);
            return Open(dataEntry);
        }

        public Stream Open(FileDataEntry dataEntry)
        {
            if(dataEntry == null) return null;
            return GetBlockView(dataEntry.BlockIndex);
        }

        public Stream OpenFileByHash(byte[] hash)
        {
            return Open(GetFileDataEntryByHash(hash));
        }

        private class BlockHashComparer : IComparer<FileDataEntry>
        {
            private BlockHashComparer()
            {
            }

            public static BlockHashComparer Instance { get; } = new BlockHashComparer();

            public int Compare(FileDataEntry x, FileDataEntry y)
            {
                if (x == null) throw new ArgumentNullException(nameof(x));
                if (y == null) throw new ArgumentNullException(nameof(y));

                for (var i = 0; i < x.Hash.Length; i++)
                {
                    if (x.Hash[i] < y.Hash[i]) return -1;
                    if (x.Hash[i] > y.Hash[i]) return 1;
                }

                return 0;
            }
        }
    }
}
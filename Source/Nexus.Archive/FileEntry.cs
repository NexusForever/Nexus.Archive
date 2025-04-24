using System;
using System.IO;

namespace Nexus.Archive
{
    public class FileEntry : FilesystemEntry, IArchiveFileEntry
    {
        public const int Size = 56;
        internal int NameOffset { get; private set; }
        public ArchiveFileFlags Flags { get; private set; }
        public DateTimeOffset WriteTime { get; private set; }
        public long UncompressedSize { get; private set; }
        public long CompressedSize { get; private set; }
        public byte[] Hash { get; private set; }
        public uint Reserved { get; private set; } // Available for use.
        public Stream OpenRead(bool decompress = true)
        {
            return Archive.OpenFileStream(this, decompress);
        }

        public static FileEntry FromReader(BinaryReader reader)
        {
            var nameOffset = reader.ReadInt32();
            var archiveFileFlags = reader.ReadUInt32();
            var fileTime = reader.ReadInt64();
            var writeTime = DateTimeOffset.FromFileTime(fileTime);
            var uncompressedSize = reader.ReadInt64();
            var compressedSize = reader.ReadInt64();
            var hash = reader.ReadBytes(20);
            var reserved = reader.ReadUInt32();

            return new FileEntry()
            {
                NameOffset = nameOffset,
                Flags = (ArchiveFileFlags)archiveFileFlags,
                WriteTime = writeTime,
                UncompressedSize = uncompressedSize,
                CompressedSize = compressedSize,
                Hash = hash,
                Reserved = reserved
            };
        }

        public override string ToString()
        {
            return ToString(false);
        }

        public string ToString(bool detailed)
        {
            if (!detailed) return Path;
            return
                $"{WriteTime.ToUnixTimeSeconds()} - {UncompressedSize} - {BitConverter.ToString(Hash).ToLower().Replace("-", "")} - {Path}";
        }
    }
}
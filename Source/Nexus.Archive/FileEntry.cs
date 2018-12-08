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
        public uint UnknownData { get; private set; }

        public static FileEntry FromReader(BinaryReader reader)
        {
            var ret = new FileEntry();
            ret.NameOffset = reader.ReadInt32();
            ret.Flags = (ArchiveFileFlags) reader.ReadUInt32();
            var fileTime = reader.ReadInt64();
            ret.WriteTime = DateTimeOffset.FromFileTime(fileTime);
            ret.UncompressedSize = reader.ReadInt64();
            ret.CompressedSize = reader.ReadInt64();
            ret.Hash = reader.ReadBytes(20);
            ret.UnknownData = reader.ReadUInt32();
            return ret;
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
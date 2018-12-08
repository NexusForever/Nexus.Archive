using System.IO;

namespace Nexus.Archive
{
    public struct RootIndexBlock
    {
        public ArchiveType ArchiveType;
        public uint Version;
        public uint BlockCount;
        public int BlockIndex;

        public static RootIndexBlock FromReader(BinaryReader reader)
        {
            return new RootIndexBlock
            {
                ArchiveType = (ArchiveType) reader.ReadUInt32(),
                Version = reader.ReadUInt32(),
                BlockCount = reader.ReadUInt32(),
                BlockIndex = reader.ReadInt32()
            };
        }
    }
}
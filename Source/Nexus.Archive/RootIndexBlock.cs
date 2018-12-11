using System.IO;

namespace Nexus.Archive
{
    public struct RootIndexBlock
    {
        public ArchiveType ArchiveType;
        public uint Version;
        public uint BlockCount;
        public uint BuildNumber;
        public int BlockIndex;

        public static RootIndexBlock FromReader(BinaryReader reader)
        {
            var ret = new RootIndexBlock()
            {
                ArchiveType = (ArchiveType)reader.ReadUInt32(),
                Version = reader.ReadUInt32(),
            };
            if (ret.Version == 1)
            {
                ret.BuildNumber = reader.ReadUInt32();
            }
            else if (ret.Version == 2)
            {
                ret.BlockCount = reader.ReadUInt32();
            }
            else
            {
                throw new InvalidDataException($"Unknown file version {ret.Version} for archive type {ret.ArchiveType}");
            }

            ret.BlockIndex = reader.ReadInt32();
            return ret;
        }
    }
}
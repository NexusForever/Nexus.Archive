using System.IO;

namespace Nexus.Archive
{
    public struct BlockInfoHeader
    {
        public ulong Offset;
        public ulong Size;

        public static BlockInfoHeader FromReader(BinaryReader reader)
        {
            return new BlockInfoHeader
            {
                Offset = reader.ReadUInt64(),
                Size = reader.ReadUInt64()
            };
        }
    }
}
using System.IO;
using System.Runtime.InteropServices;

namespace Nexus.Archive
{
    public struct DataHeader
    {
        //public ulong Unknown1;
        public ulong FileSize;
        public ulong Reserved; // Doesn't seem to matter.
        public ulong BlockTableOffset;
        public long BlockCount;
        public long RootBlockIndex;
        public ulong ReverseSeekGuard; // Must be zero

        public static DataHeader Create()
        {
            return new DataHeader();
        }

        public static DataHeader ReadFrom(BinaryReader binaryReader)
        {
            var ret = new DataHeader
            {
                //Unknown1 = binaryReader.ReadUInt64(),
                FileSize = binaryReader.ReadUInt64(),
                Reserved = binaryReader.ReadUInt64(),
                BlockTableOffset = binaryReader.ReadUInt64(),
                BlockCount = binaryReader.ReadInt64(),
                RootBlockIndex = binaryReader.ReadInt64(),
                ReverseSeekGuard = binaryReader.ReadUInt64()
            };
            return ret;
        }
    }
}
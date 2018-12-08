using System.IO;
using System.Runtime.InteropServices;

namespace Nexus.Archive
{
    public struct DataHeader
    {
        private const int UnknownDataSize = 28;
        public ulong Unknown1;
        public ulong FileSize;
        public ulong Unknown2;
        public ulong BlockTableOffset;
        public uint BlockCount;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = UnknownDataSize)]
        public byte[] UnknownData;

        public static DataHeader ReadFrom(BinaryReader binaryReader)
        {
            var ret = new DataHeader
            {
                Unknown1 = binaryReader.ReadUInt64(),
                FileSize = binaryReader.ReadUInt64(),
                Unknown2 = binaryReader.ReadUInt64(),
                BlockTableOffset = binaryReader.ReadUInt64(),
                BlockCount = binaryReader.ReadUInt32(),
                UnknownData = binaryReader.ReadBytes(UnknownDataSize)
            };
            return ret;
        }
    }
}
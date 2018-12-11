using System.IO;
using System.Runtime.InteropServices;

namespace Nexus.Archive
{
    public struct DataHeader
    {
        private const int UnknownDataSize = 24;
        public ulong Unknown1;
        public ulong FileSize;
        public byte UnknownFileIdentifier;
        // Always 2 for on disk files.
        public byte Unknown2;
        public ushort Unknown3;
        public uint Unknown4;
        //public ulong Unknown2; // Seems to denote file type, 0x60 XX XX XX for on disk files, other values for others.
        // Remaining  3 bytes unknown.
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
                UnknownFileIdentifier = binaryReader.ReadByte(),
                Unknown2 = binaryReader.ReadByte(),
                Unknown3 = binaryReader.ReadUInt16(),
                Unknown4 = binaryReader.ReadUInt32(),
                BlockTableOffset = binaryReader.ReadUInt64(),
                BlockCount = binaryReader.ReadUInt32(),
                UnknownData = binaryReader.ReadBytes(UnknownDataSize)
            };
            return ret;
        }
    }
}
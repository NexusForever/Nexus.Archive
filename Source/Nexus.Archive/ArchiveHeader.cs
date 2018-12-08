using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Nexus.Archive
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ArchiveHeader
    {
        private const int UnknownDataSize = 507;
        public uint Signature;
        public byte Version;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = UnknownDataSize)]
        public byte[] UnknownData;

        public static ArchiveHeader ReadFrom(Stream stream)
        {
            var ret = new ArchiveHeader();
            using (var binaryReader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                ret.Signature = binaryReader.ReadUInt32();
                ret.Version = binaryReader.ReadByte();
                ret.UnknownData = binaryReader.ReadBytes(UnknownDataSize);
                ret.DataHeader = DataHeader.ReadFrom(binaryReader);
            }

            return ret;
        }

        public DataHeader DataHeader;
    }
}
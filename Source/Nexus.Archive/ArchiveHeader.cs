using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Nexus.Archive;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ArchiveHeader
{
    public const uint PackSignature = 0x5041434b;
    private const int ReservedSectionSize = 512;
    public uint Signature; // 'PACK' - as UINT, Written as KCAP in file.
    public uint Version;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = ReservedSectionSize)]
    public byte[] Reserved;

    public static ArchiveHeader Create()
    {
        return new ArchiveHeader()
        {
            Signature = PackSignature,
            Version = 1,
            Reserved = new byte[ReservedSectionSize],
            DataHeader = DataHeader.Create()
        };
    }

    public static ArchiveHeader ReadFrom(Stream stream)
    {
        var ret = new ArchiveHeader();
        using (var binaryReader = new BinaryReader(stream, Encoding.UTF8, true))
        {
            ret.Signature = binaryReader.ReadUInt32();
            ret.Version = binaryReader.ReadUInt32();
            ret.Reserved = binaryReader.ReadBytes(ReservedSectionSize);
            ret.DataHeader = DataHeader.ReadFrom(binaryReader);
        }
        if(ret.Signature != PackSignature) throw new InvalidDataException($"File header value {ret.Signature:X4} does not match expected {PackSignature:X4}");

        return ret;
    }

    public DataHeader DataHeader;
}
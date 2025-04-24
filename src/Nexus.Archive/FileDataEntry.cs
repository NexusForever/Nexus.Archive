using System.IO;

namespace Nexus.Archive;

public class FileDataEntry
{
    public int BlockIndex { get; private set; }
    public byte[] Hash { get; private set; }
    public long UncompressedSize { get; private set; }

    internal static FileDataEntry ForSearch(byte[] hash)
    {
        return new FileDataEntry
        {
            Hash = hash
        };
    }

    public static FileDataEntry FromReader(BinaryReader reader)
    {
        return new FileDataEntry
        {
            BlockIndex = reader.ReadInt32(),
            Hash = reader.ReadBytes(20),
            UncompressedSize = reader.ReadInt64()
        };
    }
}
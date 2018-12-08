using System.IO;

namespace Nexus.Archive
{
    public class FolderPointer
    {
        public const int Size = 8;
        public int FolderBlock;
        public uint NameOffset;
        public string Name { get; internal set; }

        public static FolderPointer FromReader(BinaryReader reader)
        {
            var ret = new FolderPointer();
            ret.NameOffset = reader.ReadUInt32();
            ret.FolderBlock = reader.ReadInt32();
            return ret;
        }
    }
}
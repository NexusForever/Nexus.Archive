using System.IO;

namespace Nexus.Archive
{
    public class FileViewableData : IViewableData
    {
        private FileStream _file;

        public FileViewableData(string fileName, FileAccess fileAccess = FileAccess.Read, FileShare fileShare = FileShare.ReadWrite)
        {
            FileName = fileName;
            _file = File.Open(fileName, fileAccess != FileAccess.Write ? FileMode.Open : FileMode.OpenOrCreate, fileAccess, fileShare);
        }
        public void Dispose()
        {
            _file?.Dispose();
            _file = null;
        }

        public string FileName { get; }
        public Stream CreateView(long offset, long length)
        {
            _file.Seek(offset, SeekOrigin.Begin);
            var data = new byte[length];
            _file.ReadExactly(data, 0, data.Length);
            return new MemoryStream(data);
        }

        public long Length => _file.Length;
    }
}
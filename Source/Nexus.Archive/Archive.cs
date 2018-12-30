using System;
using System.IO;
using System.IO.Compression;
using SharpCompress.Compressors.LZMA;

namespace Nexus.Archive
{
    public sealed class Archive : IDisposable
    {
        public Archive(IndexFile index, ArchiveFile archive, ArchiveFile coreDataArchive)
        {
            IndexFile = index;
            ArchiveFile = archive;
            CoreDataArchive = coreDataArchive;
        }

        public IndexFile IndexFile { get; }
        public ArchiveFile ArchiveFile { get; }
        public ArchiveFile CoreDataArchive { get; }

        public bool HasArchiveFile => ArchiveFile != null;

        public void Dispose()
        {
            IndexFile?.Dispose();
            ArchiveFile?.Dispose();
        }

        public IArchiveFileEntry GetFileInfoByPath(string path)
        {
            return IndexFile.FindEntry(path) as IArchiveFileEntry;
        }

        /// <summary>
        ///     Returns a readable stream of file data
        /// </summary>
        /// <param name="fileEntry">The file entry to load</param>
        /// <returns></returns>
        public Stream OpenFileStream(IArchiveFileEntry fileEntry, bool handleCompression = true)
        {
            Stream baseStream = null;
            baseStream = ArchiveFile.Open(fileEntry) ?? CoreDataArchive?.Open(fileEntry) ?? OpenLocalFile(fileEntry.Path);
            return handleCompression ? HandleCompression(fileEntry, baseStream) : baseStream;
        }

        internal static Stream HandleCompression(IArchiveFileEntry fileEntry, Stream baseStream)
        {
            switch (fileEntry.Flags & (ArchiveFileFlags.CompressedLzma | ArchiveFileFlags.CompressedDeflate))
            {
                case ArchiveFileFlags.CompressedLzma:
                    var properties = new byte[5];
                    baseStream.Read(properties, 0, properties.Length);
                    return new LzmaStream(properties, baseStream, fileEntry.CompressedSize - properties.Length,
                        fileEntry.UncompressedSize);
                case ArchiveFileFlags.CompressedDeflate:
                    return new DeflateStream(baseStream, CompressionMode.Decompress, false);
                default:
                    return baseStream;
            }
        }

        private Stream OpenLocalFile(string indexFileName)
        {
            // This has some weird rules to it.
            // Seems to be Name is trimmed of Data
            // For launcher, WildStar.exe is in ..\, but the rest of the files follow the rules. (IE: Launcher.index, and LauncherData.index reside in ..\launcher
            // So much nonsense. I didn't bother with it yet. But I will....
            var basePath = Path.GetDirectoryName(Path.GetDirectoryName(IndexFile.FileName));
            var dataFolder = Path.GetFileNameWithoutExtension(IndexFile.FileName);
            var path = Path.Combine(basePath, dataFolder, indexFileName);
            return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }


        public static Archive FromFile(string name, ArchiveFile coreDataArchive = null)
        {
            string baseName;
            var directory = Path.GetDirectoryName(name);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(name);
            if (!string.IsNullOrWhiteSpace(directory))
                baseName = Path.Combine(directory, nameWithoutExtension);
            else
                baseName = nameWithoutExtension;

            var indexFile = ArchiveFileBase.FromFile(Path.ChangeExtension(baseName, "index")) as IndexFile;
            if (indexFile == null) throw new ArgumentException($"Could not find {name}");
            var archiveFileName = Path.ChangeExtension(baseName, "archive");
            ArchiveFile archiveFile = null;
            if (File.Exists(archiveFileName)) archiveFile = ArchiveFileBase.FromFile(archiveFileName) as ArchiveFile;
            return new Archive(indexFile, archiveFile, coreDataArchive);
        }
    }
}
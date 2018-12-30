using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Nexus.Archive.Patcher
{
    public class FolderPatchWriter : IPatchWriter
    {
        public ArchiveFile CoreData { get; }
        DirectoryInfo Target { get; }
        public event EventHandler<ProgressUpdateEventArgs> ProgressUpdated;

        private void OnProgressUpdated(ProgressUpdateEventArgs eventArgs)
        {
            ProgressUpdated?.Invoke(this, eventArgs);
        }
        public FolderPatchWriter(string basePath, ArchiveFile coreData)
        {
            CoreData = coreData;
            Target = Directory.CreateDirectory(basePath);
        }

        private string GetEntryPath(IArchiveFileEntry entry)
        {
            return Path.Combine(Target.FullName, entry.Path);
        }

        private bool ExistsWithMatchingHash(IArchiveFileEntry entry)
        {
            return ExistsWithMatchingHash(entry, GetEntryPath(entry));
        }

        private bool ExistsWithMatchingHash(IArchiveFileEntry entry, string path)
        {
            if (CoreData?.GetFileDataEntryByHash(entry.Hash) != null) return true;
            if (!File.Exists(path))
                return false;

            using (var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var sha1 = SHA1.Create())
                {
                    var hash = sha1.ComputeHash(fileStream);
                    if (hash.SequenceEqual(entry.Hash))
                        return true;
                }
            }
            return false;
        }

        public async Task AppendAsync(Stream stream, IArchiveFileEntry fileEntry)
        {
            var filesystemTarget = GetEntryPath(fileEntry);
            if (ExistsWithMatchingHash(fileEntry, filesystemTarget))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(filesystemTarget));
            using (var fileStream = File.Open(filesystemTarget, FileMode.Create, FileAccess.ReadWrite,
                FileShare.ReadWrite))
            {
                var source = Archive.HandleCompression(fileEntry, stream);

                void ProgressCallback(long length, long progress)
                {
                    var eventArgs = new ProgressUpdateEventArgs(fileEntry, progress, fileEntry.UncompressedSize);
                    OnProgressUpdated(eventArgs);
                }
                try
                {
                    await source.CopyToAsync(fileStream, ProgressCallback);
                }
                finally
                {
                    if (source != stream)
                    {
                        source.Close();
                        source.Dispose();
                    }
                }
            }
        }

        public Task AppendAsync(byte[] data, IArchiveFileEntry fileEntry)
        {
            return AppendAsync(new MemoryStream(data), fileEntry);
        }

        public Task<bool> Exists(IArchiveFileEntry fileEntry)
        {
            return Task.FromResult(ExistsWithMatchingHash(fileEntry));
        }

        public bool IsThreadSafe => true;
    }
}
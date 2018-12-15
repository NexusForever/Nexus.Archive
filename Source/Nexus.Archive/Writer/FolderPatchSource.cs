using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nexus.Archive.Extensions;

namespace Nexus.Archive.Writer
{
    public class FolderPatchSource : IPatchSource
    {
        private readonly string _basePath;

        public FolderPatchSource(string basePath)
        {
            this._basePath = basePath;
        }

        private Stream OpenFirst(params string[] fileNames)
        {
            foreach (var file in fileNames)
            {
                if (File.Exists(file))
                    return File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            return null;
        }

        public Task<Stream> GetDataStream(byte[] hash, IndexFile indexFile, CancellationToken cancellationToken = default)
        {
            var preferredFiles = new[]
            {
                Path.Combine(_basePath, Path.GetFileNameWithoutExtension(indexFile.FileName),
                    indexFile.RootIndex.BuildNumber.ToString(), $"{hash.ToHexString()}.bin"),
                Path.Combine(_basePath, Path.GetFileNameWithoutExtension(indexFile.FileName),
                    $"{hash.ToHexString()}.bin"),
                Path.Combine(_basePath, indexFile.RootIndex.BuildNumber.ToString(), $"{hash.ToHexString()}.bin"),
                Path.Combine(_basePath, $"{hash.ToHexString()}.bin"),
            };
            var ret = OpenFirst(preferredFiles);
            if (ret != null) return Task.FromResult(ret);

            var fileMetadata = indexFile.GetFiles().FirstOrDefault(i => i.Hash.SequenceEqual(hash));
            if (fileMetadata == null)
            {
                // uh.... ok...?
                return null;
            }

            preferredFiles = new[]
            {
                Path.Combine(_basePath, Path.GetFileNameWithoutExtension(indexFile.FileName),
                    indexFile.RootIndex.BuildNumber.ToString(), fileMetadata.Path),
                Path.Combine(_basePath, indexFile.RootIndex.BuildNumber.ToString(), fileMetadata.Path),
                Path.Combine(_basePath, fileMetadata.Path)
            };
            return Task.FromResult(OpenFirst(preferredFiles));
        }
    }
}
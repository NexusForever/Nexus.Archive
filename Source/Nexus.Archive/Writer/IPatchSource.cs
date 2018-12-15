using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Archive.Writer
{
    public interface IPatchSource
    {
        Task<Stream> GetDataStream(byte[] hash, IndexFile indexFile, CancellationToken cancellationToken = default);
    }
}
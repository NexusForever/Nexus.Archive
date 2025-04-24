using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nexus.Archive.Patcher;

public interface IPatchSource
{
    Task<Stream> DownloadHashAsync(int build, byte[] hash, CancellationToken cancellationToken = default);
    Task<Stream> DownloadFileAsync(int build, string fileName, CancellationToken cancellationToken = default);
    Task<Stream> DownloadFileAsync(string fileName, CancellationToken cancellationToken = default);
    Task<byte[]> GetFileHashAsync(string fileName, CancellationToken cancellationToken = default);
    Task<byte[]> GetFileHashAsync(int build, string fileName, CancellationToken cancellationToken = default);
    Task<int> GetServerBuildAsync(CancellationToken cancellationToken = default);
}
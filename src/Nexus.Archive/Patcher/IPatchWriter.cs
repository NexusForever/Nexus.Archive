using System;
using System.IO;
using System.Threading.Tasks;

namespace Nexus.Archive.Patcher;

public interface IPatchWriter
{
    Task AppendAsync(Stream stream, IArchiveFileEntry fileEntry);
    Task AppendAsync(byte[] data, IArchiveFileEntry fileEntry);
    Task<bool> Exists(IArchiveFileEntry fileEntry);
    bool IsThreadSafe { get; }
    event EventHandler<ProgressUpdateEventArgs> ProgressUpdated;
}
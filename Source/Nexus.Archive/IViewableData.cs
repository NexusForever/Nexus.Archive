using System;
using System.IO;

namespace Nexus.Archive;

public interface IViewableData : IDisposable
{
    string FileName { get; }
    Stream CreateView(long offset, long length);
    long Length { get; }
}
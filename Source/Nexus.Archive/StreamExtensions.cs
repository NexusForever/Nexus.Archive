using System;
using System.IO;

namespace Nexus.Archive
{
    public static class StreamExtensions
    {
        public static void CopyTo(this Stream source, Stream target, int bufferSize,
            Action<long, long> progressCallback)
        {
            byte[] buffer = new byte[bufferSize];
            int bytesRead = 0;
            long totalBytesRead = 0;
            long length = -1;
            try
            {
                length = source.Length;
            }
            catch { /* ignored */ }
            while ((bytesRead = source.Read(buffer, 0, bufferSize)) > 0)
            {
                totalBytesRead += bytesRead;
                target.Write(buffer, 0, bytesRead);
                progressCallback(length, totalBytesRead);
            }
        }
        public static void CopyTo(this Stream source, Stream target, Action<long, long> progressCallback)
        {
            CopyTo(source, target, 81920, progressCallback);
        }
    }
}
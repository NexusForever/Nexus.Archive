using System;

namespace Nexus.Archive.Extensions
{
    public static class GeneralExtensions
    {
        public static string ToHexString(this byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", "").ToLower();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Nexus.Archive.Extensions
{
    internal static class GeneralExtensions
    {
        public static string ToHexString(this byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", "").ToLower();
        }
    }
}

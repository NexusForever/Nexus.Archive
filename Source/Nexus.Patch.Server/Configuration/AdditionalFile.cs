using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nexus.Patch.Server.Configuration
{
    public class AdditionalFile
    {
        public string Path { get; set; }
        public string FileName { get; set; }
        public bool PublishBin { get; set; }
    }
}

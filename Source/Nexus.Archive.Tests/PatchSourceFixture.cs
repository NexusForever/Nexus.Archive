using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Nexus.Archive.Patcher;

namespace Nexus.Archive.Tests
{

    public class PatchSourceFixture
    {
        public IPatchSource PatchSource {get;}

        public PatchSourceFixture()
        {
            PatchSource = new HttpPatchSource("https://patch.logg.coffee/");
        }
    }
}

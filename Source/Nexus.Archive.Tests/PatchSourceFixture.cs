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

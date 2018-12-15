using System;

namespace Nexus.Archive.Writer
{
    public static class PatchSource
    {
        public static IPatchSource PatchServer(Uri baseUri)
        {
            return new PatchServerPatchSource(baseUri);
        }

        public static IPatchSource Folder(string basePath)
        {
            return new FolderPatchSource(basePath);
        }
    }
}
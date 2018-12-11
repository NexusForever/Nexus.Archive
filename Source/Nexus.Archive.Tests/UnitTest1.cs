using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Nexus.Archive.Tests
{
    public class StructureTests
    {
        private const string GamePath = "D:\\SteamLibrary\\steamapps\\common\\WildStar";
        public ITestOutputHelper TestOutputHelper { get; }

        public StructureTests(ITestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper;
        }

        private static ArchiveFileBase OpenFile(string fileName)
        {
            return ArchiveFileBase.FromFile(fileName);
            //return MemoryMappedFile.CreateFromFile(Path.Combine(RootPath, fileName),
            //    FileMode.Open);
        }

        private IndexFile ReadBootstrap()
        {
            return LoadIndexFromPath(Path.Combine("Patch", "Patch.index"));
        }

        [Fact]
        public void FileLookupTest()
        {
            var enIndex = LoadIndexFromPath(Path.Combine("Patch", "ClientDataEN.index"), true);
            var entry = enIndex.FindEntry("ClientDataEN\\en-US.bin");
            Assert.NotNull(entry);
        }
        [Fact]
        public void FileTypeHeaderChecks()
        {
            var clientDataArchive = LoadArchiveFromPath(Path.Combine("Patch", "ClientData.archive"), true);

        }

        private ArchiveFile LoadArchiveFromPath(string path, bool required = false)
        {
            var realPath = Path.Combine(GamePath, path);
            if (!File.Exists(realPath))
            {
                if (required) throw new FileNotFoundException("File not found", realPath);
                return null;
            }
            return (ArchiveFile)ArchiveFileBase.FromFile(realPath);
        }
        private IndexFile LoadIndexFromPath(string path, bool required = false)
        {
            var realPath = Path.Combine(GamePath, path);
            if (!File.Exists(realPath))
            {
                if (required) throw new FileNotFoundException("File not found", realPath);
                return null;
            }
            return (IndexFile)ArchiveFileBase.FromFile(realPath);
        }

        [Fact]
        public void ArchiveClassShouldLocateOnDiskFilesWhenNeeded()
        {
            var patchIndex = Path.Combine(GamePath, "Patch", "Patch.index");
            var archive = Archive.FromFile(patchIndex);
            var entry = archive.IndexFile.FindEntry("ClientData.index") as IArchiveFileEntry;
            Assert.NotNull(entry);
            using (var stream = archive.OpenFileStream(entry))
            {
                Assert.NotNull(stream);
            }
        }

        [Fact]
        public void TestArchiveLoading()
        {
            var patchFolder = Path.Combine(GamePath, "Patch");
            List<Archive> archives = new List<Archive>();
            var directoryInfo = Directory.CreateDirectory(patchFolder);
            foreach (var file in directoryInfo.EnumerateFiles("*.index"))
            {
                archives.Add(Archive.FromFile(file.FullName));
            }

            foreach (var archive in archives)
            {
                TestOutputHelper.WriteLine($"{archive.IndexFile.FileName} -> {archive.ArchiveFile?.FileName ?? "<MISSING>"}");
            }
        }
        [Fact]
        public void TestEnumeration()
        {
            
            var bootstrapIndex = ReadBootstrap();
            bootstrapIndex.Dispose();
            Queue<IndexFile> foundIndexFiles = new Queue<IndexFile>();
            int counter = 0;
            var sw = Stopwatch.StartNew();
            bootstrapIndex = ReadBootstrap();
            foundIndexFiles.Enqueue(bootstrapIndex);
            while (foundIndexFiles.TryDequeue(out var current))
            {
                if(current == null) continue;
                counter++;
                var folder = Path.GetFileName(Path.GetDirectoryName(current.FileName));
                foreach (var file in current.RootFolder.EnumerateFiles(true))
                {
                    if (file.Path.EndsWith(".index", StringComparison.OrdinalIgnoreCase))
                    {
                        foundIndexFiles.Enqueue(LoadIndexFromPath(file.Path, false));
                    }
                }
            }
            sw.Stop();
            TestOutputHelper.WriteLine($"Loaded {counter} index files in {sw.ElapsedMilliseconds}ms");
        }

        private ArchiveHeader ReadHeaderFromFile(MemoryMappedFile file)
        {
            var length = Marshal.SizeOf<ArchiveHeader>();
            using (var stream = file.CreateViewStream(0, length))
                return ArchiveHeader.ReadFrom(stream);
        }


    }
}

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Nexus.Archive.Extensions;
using Nexus.Archive.Patcher;
using Xunit;
using Xunit.Abstractions;

namespace Nexus.Archive.Tests
{
    public class CoreDataArchiveFixture
    {
        public ArchiveFile CoreData { get; }
        public CoreDataArchiveFixture()
        {
            var basePath = Directory.CreateDirectory("Patch").FullName;
            var fileName = Path.Combine(basePath, "CoreData.archive");
            if (!File.Exists(fileName)) return;
            CoreData = (ArchiveFile)ArchiveFile.FromFile(fileName);
        }
    }
    public class PatcherTest : IClassFixture<PatchSourceFixture>, IClassFixture<CoreDataArchiveFixture>
    {
        public ITestOutputHelper Output { get; }
        public IPatchSource PatchSource { get; }
        public ArchiveFile CoreData { get; }
        public PatcherTest(ITestOutputHelper output, PatchSourceFixture patchSourceFixture, CoreDataArchiveFixture coreDataFixture)
        {
            Output = output;
            PatchSource = patchSourceFixture.PatchSource;
            CoreData = coreDataFixture.CoreData;
            PatchPath = Directory.CreateDirectory("Patch").FullName;
        }

        private ConcurrentDictionary<string, Stopwatch> _stopWatches =
            new ConcurrentDictionary<string, Stopwatch>(StringComparer.OrdinalIgnoreCase);

        private Stopwatch GetStopwatch(IArchiveFileEntry fileEntry)
        {
            return _stopWatches.GetOrAdd(fileEntry.Hash.ToHexString(), _ => Stopwatch.StartNew());
        }

        private void RemoveStopwatch(IArchiveFileEntry fileEntry)
        {
            _stopWatches.TryRemove(fileEntry.Hash.ToHexString(), out _);
        }

        public Stopwatch DownloadStopwatch { get; }
        public string PatchPath { get; set; }

        [Fact]
        public async Task DownloadIndexFiles()
        {
            foreach (var file in Directory.GetFiles(PatchPath, "*.index")
                .Where(i => Path.GetFileNameWithoutExtension(i).ToLower() != "patch"))
            {
                // This is not correct, I just want to test the patcher itself.
                await PatchIndex(file, Path.Combine(Path.GetDirectoryName(PatchPath), Path.GetFileNameWithoutExtension(file)));
            }
        }

        private async Task PatchIndex(string file, string targetPath)
        {
            var patcher = new Patcher.Patcher(PatchSource, (IndexFile)ArchiveFileBase.FromFile(file), CoreData, targetPath);
            patcher.Progress += OnProgressUpdated;
            await patcher.Patch(CancellationToken.None);
        }

        [Fact]
        public async Task DownloadAndProcessPatchIndexAsync()
        {
            // This stuff here is bootstrap, we trust the files are correct, because we assume this was done first.
            // Because it has to be done first, or we don't have the index files to begin with.
            var patchFileName = Path.Combine(PatchPath, "Patch.index");
            var serverBuild = await PatchSource.GetServerBuildAsync().ConfigureAwait(false);
            var patchIndexHash = await PatchSource.GetFileHashAsync(Path.GetFileName(patchFileName)).ConfigureAwait(false);
            bool downloadRequired = true;
            if (File.Exists(patchFileName))
            {
                using (var sha1 = SHA1.Create())
                using (var fileStream = File.Open(patchFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var fileHash = sha1.ComputeHash(fileStream);
                    downloadRequired = !fileHash.SequenceEqual(patchIndexHash);
                }
            }

            if (downloadRequired)
            {
                void WriteProgress(long length, long progress)
                {
                    var message = length <= 0 ? $"Downloaded {progress} bytes" : $"Downloaded {progress} of {length} bytes";
                    Output.WriteLine(message);
                }
                var dataStream = await PatchSource.DownloadHashAsync(serverBuild, patchIndexHash);
                using (var fileStream =
                    File.Open(patchFileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    await dataStream.CopyToAsync(fileStream, WriteProgress);
                }
            }
            // But right here, we can just reuse our existing stuff.
            // So CoreData.archive is optional, and if it exists, we need to check it for existing data
            // Before we just blindly re-download it.
            await PatchIndex(patchFileName, PatchPath).ConfigureAwait(false);
        }

        private void OnProgressUpdated(object sender, ProgressUpdateEventArgs e)
        {
            var stopwatch = GetStopwatch(e.FileEntry);
            if (e.BytesWritten == 0)
            {
                stopwatch.Reset();
                stopwatch.Start();
            }
            var message = $"Downloaded {e.BytesWritten}";
            message = e.Length > 0 ? $"{message} of {e.Length} byte(s)" : $"{message} byte(s)";
            var seconds = stopwatch.Elapsed.TotalSeconds;
            if (seconds > 0 && e.BytesWritten > 0)
            {
                var bytesPerSecond = e.BytesWritten / seconds;
                var bandwidth = PrettyPrintBandwidth(bytesPerSecond);
                message = $"{message} ({bandwidth})";
            }
            message = $"{e.FileEntry.Path}: {message}";

            if (e.BytesWritten == e.Length)
            {
                RemoveStopwatch(e.FileEntry);
            }

            Output.WriteLine(message);
        }

        private string PrettyPrintBandwidth(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024)
            {
                return $"{bytesPerSecond:0.0} bytes/sec";
            }

            bytesPerSecond = bytesPerSecond / 1024d;
            if (bytesPerSecond < 1024)
            {
                return $"{bytesPerSecond:0.0} kb/sec";
            }

            bytesPerSecond = bytesPerSecond / 1024d;
            if (bytesPerSecond < 1024)
            {
                return $"{bytesPerSecond:0.0} mb/sec";
            }

            bytesPerSecond = bytesPerSecond / 1024d;
            return $"{bytesPerSecond:0.0} gb/sec";
        }
    }
    public class HttpPatchSourceTests : IClassFixture<PatchSourceFixture>
    {
        public ITestOutputHelper Output { get; }
        public IPatchSource PatchSource { get; }
        public HttpPatchSourceTests(ITestOutputHelper output, PatchSourceFixture patchSourceFixture)
        {
            Output = output;
            Output.WriteLine("New test instance constructed");
            PatchSource = patchSourceFixture.PatchSource;
        }


        [Fact]
        public async Task ServerBuildVersion()
        {
            var serverBuild = await PatchSource.GetServerBuildAsync();
            Output.WriteLine($"Got build # {serverBuild}");
            Assert.Equal(16042, serverBuild);
        }

        [Fact]
        public async Task GetPatchIndexWithBuildNumber()
        {
            Output.WriteLine("Downloading Patch.index with build number");
            var buildVersion = await PatchSource.GetServerBuildAsync();
            Output.WriteLine($"Got build # {buildVersion}");

            await VerifyIndex("Patch.index", buildVersion).ConfigureAwait(false);
        }

        [Fact]
        public async Task DownloadMultipleIndexFiles()
        {
            Output.WriteLine("Testing multiple index files...");
            var files = new[]
            {
                "Patch.index",
                "ClientData.index",
                "ClientDataEN.index",
                "ClientDataDE.index",
                "ClientDataFR.index",
                "Client.index",
                "Client64.index",
                "Bootstrap.index"
            };
            foreach (var file in files)
            {
                await VerifyIndex(file);
            }
        }

        private async Task VerifyIndex(string name, int build = -1)
        {
            var binaryHash = await PatchSource.GetFileHashAsync(build, name);
            if (build <= 0)
                build = await PatchSource.GetServerBuildAsync();
            var fileStream = await PatchSource.DownloadHashAsync(build, binaryHash); ;
            Assert.NotNull(fileStream);
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(fileStream);
                var expectedHash = binaryHash.ToHexString();
                var computedHash = hash.ToHexString();

                Output.WriteLine($"Got hash {computedHash} for {name}");
                Assert.Equal(expectedHash, computedHash);
            }
        }

        [Fact]
        public async Task GetPatchIndexWithoutBuildNumber()
        {
            Output.WriteLine("Downloading Patch.index without providing build number");
            await VerifyIndex("Patch.index").ConfigureAwait(false);
        }


    }
}
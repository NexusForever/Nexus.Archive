using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Nexus.Archive;

namespace Nexus.Patch.Server.Services
{
    public class GameDataFiles : IGameDataFiles
    {
        protected GameDataFiles(IEnumerable<IndexFile> indexFiles, IEnumerable<ArchiveFile> archiveFiles, string launcherFilename, byte[] launcherHash, int build)
        {
            Build = build;
            _indexFiles = indexFiles.ToList();
            // Warm up file reading now.
            // ReSharper disable ReturnValueOfPureMethodIsNotUsed - Preloading data.
            _indexFiles.SelectMany(i => i.GetFilesystemEntries()).ToList();
            // ReSharper restore ReturnValueOfPureMethodIsNotUsed
            _archiveFiles = archiveFiles.ToList();
            _launcherPath = launcherFilename;
            _launcherHash = launcherHash;
        }
        public static GameDataFiles Load(string path, int build, ILogger logger)
        {
            List<ArchiveFileBase> dataFiles = new List<ArchiveFileBase>();
            foreach (var indexFile in Directory.EnumerateFiles(path, "*.index", SearchOption.AllDirectories))
            {
                var index = ArchiveFileBase.FromFile(indexFile);
                if (index != null)
                {
                    logger.LogInformation($"Loaded file {indexFile}");
                    dataFiles.Add(index);
                }
            }

            foreach (var archiveFile in Directory.EnumerateFiles(path, "*.archive", SearchOption.AllDirectories))
            {
                var archive = ArchiveFileBase.FromFile(archiveFile);
                if (archive != null)
                {
                    logger.LogInformation($"Loaded file {archiveFile}");
                    dataFiles.Add(archive);
                }
            }

            var launcherPath = Path.Combine(path, "WildStar.exe");
            byte[] launcherHash;
            using (var launcherStream = File.Open(launcherPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sha = SHA1.Create())
            {
                launcherHash = sha.ComputeHash(launcherStream);
            }

            return new GameDataFiles(dataFiles.OfType<IndexFile>(), dataFiles.OfType<ArchiveFile>(), launcherPath, launcherHash, build);
        }

        private List<IndexFile> _indexFiles;
        private List<ArchiveFile> _archiveFiles;
        private string _launcherPath;
        private byte[] _launcherHash;

        public IEnumerable<(string filePath, string alias, byte[] hash)> OtherFiles => new[]
        {
            (_launcherPath, "Launcher.exe", _launcherHash)
        };
        public IEnumerable<IndexFile> IndexFiles => _indexFiles;
        public IEnumerable<ArchiveFile> ArchiveFiles => _archiveFiles;
        public int Build { get; }

    }
}
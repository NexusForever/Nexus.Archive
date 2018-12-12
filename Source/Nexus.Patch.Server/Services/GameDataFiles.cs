using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexus.Archive;

namespace Nexus.Patch.Server.Services
{
    public class HashLookup<T> where T : class
    {
        public byte Key { get; }
        public Dictionary<byte, HashLookup<T>> Children { get; } = new Dictionary<byte, HashLookup<T>>();
        public T Value { get; }

        public HashLookup(byte key, T value = default(T))
        {
            Key = key;
            Value = value;
        }

        public static implicit operator T(HashLookup<T> value)
        {
            return value.Value;
        }

        public HashLookup<T> Find(ArraySegment<byte> hash)
        {
            if (hash.Count == 0) return this;
            return Children.TryGetValue(hash[0], out var next)
                ? next?.Find(new ArraySegment<byte>(hash.Array, hash.Offset + 1, hash.Count - 1))
                : null;
        }

        public void Set(ArraySegment<byte> hash, T value)
        {
            if (hash.Count == 0) throw new ArgumentException();
            if (hash.Count > 1)
            {
                if (!Children.TryGetValue(hash[0], out var next) || next == null)
                {
                    next = new HashLookup<T>(hash[0]);
                    Children[hash[0]] = next;
                }
                if (!Children.ContainsKey(hash[0]))
                {
                    next = new HashLookup<T>(hash[0]);
                    Children.Add(hash[0], new HashLookup<T>(hash[0]));
                }

                next.Set(new ArraySegment<byte>(hash.Array, hash.Offset + 1, hash.Count - 1), value);
            }
            else if (hash.Count == 1)
            {
                Children[hash[0]] = new HashLookup<T>(hash[0], value);
            }

        }


    }
    public class GameDataFiles : IGameDataFiles
    {
        private interface IHashedFile
        {
            Stream Open();
        }
        private class DataEntryReference : IHashedFile
        {
            public DataEntryReference(ArchiveFile archive, FileDataEntry fileEntry)
            {
                Archive = archive;
                FileEntry = fileEntry;
            }

            public ArchiveFile Archive { get; }
            public FileDataEntry FileEntry { get; }

            public Stream Open() => Archive.Open(FileEntry);
        }

        private class LocalFile : IHashedFile
        {
            public LocalFile(string path)
            {
                Path = path;
            }

            public string Path { get; }
            public Stream Open() => File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        private HashLookup<IHashedFile> _lookup = new HashLookup<IHashedFile>(0, null);

        public GameDataFiles(IConfiguration configuration, ILogger<GameDataFiles> logger) : this(
            configuration.GetValue<string>("GameFiles", null), configuration.GetValue<int>("Build"), logger)
        {

        }
        protected GameDataFiles(string basePath, int build, ILogger<GameDataFiles> logger)
        {
            List<ArchiveFileBase> dataFiles = new List<ArchiveFileBase>();
            foreach (var indexFile in Directory.EnumerateFiles(basePath, "*.index", SearchOption.AllDirectories))
            {
                var index = ArchiveFileBase.FromFile(indexFile);
                if (index != null)
                {
                    logger.LogInformation($"Loaded file {indexFile}");
                    dataFiles.Add(index);
                }
            }

            foreach (var archiveFile in Directory.EnumerateFiles(basePath, "*.archive", SearchOption.AllDirectories))
            {
                var archive = ArchiveFileBase.FromFile(archiveFile);
                if (archive != null)
                {
                    logger.LogInformation($"Loaded file {archiveFile}");
                    dataFiles.Add(archive);
                }
            }

            var launcherPath = Path.Combine(basePath, "WildStar.exe");
            byte[] launcherHash;
            using (var launcherStream = File.Open(launcherPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sha = SHA1.Create())
            {
                launcherHash = sha.ComputeHash(launcherStream);
            }

            //return new GameDataFiles(path, dataFiles.OfType<IndexFile>(), dataFiles.OfType<ArchiveFile>(), launcherPath, launcherHash, build);
            Build = build;
            _indexFiles = dataFiles.OfType<IndexFile>().ToList();
            // Warm up file reading now.
            // ReSharper disable ReturnValueOfPureMethodIsNotUsed - Preloading data.
            _indexFiles.SelectMany(i => i.GetFilesystemEntries()).ToList();
            // ReSharper restore ReturnValueOfPureMethodIsNotUsed
            _archiveFiles = dataFiles.OfType<ArchiveFile>().ToList();
            Logger = logger;
            _launcherPath = launcherPath;
            _launcherHash = launcherHash;
            Logger.LogInformation("Indexing launcher");
            _lookup.Set(_launcherHash, new LocalFile(_launcherPath));
            Logger.LogInformation("Indexing index file hashes.");
            foreach (var index in _indexFiles)
            {
                _lookup.Set(index.FileHash, new LocalFile(index.FileName));
            }

            Logger.LogInformation("Indexing client files (Client, Client64, Launcher)");
            foreach (var localFile in new[] { "Client", "Client64", "Launcher" }.Select(i => Path.Combine(basePath, i))
                .Where(Directory.Exists)
                .SelectMany(i => Directory.EnumerateFiles(i, "*.*", SearchOption.AllDirectories)))
            {

                using (var fileStream = File.Open(localFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sha = SHA1.Create())
                {
                    var binHash = sha.ComputeHash(fileStream);
                    _lookup.Set(binHash, new LocalFile(localFile));
                }
            }

            Logger.LogInformation("Indexing archive files");
            foreach (var archive in _archiveFiles)
            {
                Logger.LogInformation("Indexing {count} hashes from {archive}", archive.FileData.Count(), Path.GetFileName(archive.FileName));
                foreach (var dataEntry in archive.FileData) //_archiveFiles.SelectMany(i => i.FileData.Select(x => new DataEntryReference(i, x))))
                {
                    _lookup.Set(dataEntry.Hash, new DataEntryReference(archive, dataEntry));
                    //Debug.Assert(_lookup.Find(dataEntry.FileEntry.Hash) != null);
                }
            }
        }

        public Stream OpenHash(byte[] hash)
        {
            return _lookup.Find(hash)?.Value?.Open();
        }

        public byte[] GetHash(string fileName)
        {
            if (OtherFiles.Any(i => string.Equals(i.alias, fileName, StringComparison.OrdinalIgnoreCase)))
            {
                return OtherFiles.First(i => string.Equals(i.alias, fileName, StringComparison.OrdinalIgnoreCase)).hash;
            }

            foreach (var index in _indexFiles)
            {
                var name = Path.GetFileName(index.FileName);
                if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
                    return index.FileHash;
            }

            return null;
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

        public ILogger<GameDataFiles> Logger { get; }
        public int Build { get; }

    }
}
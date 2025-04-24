using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexus.Archive;
using Nexus.Patch.Server.Configuration;

namespace Nexus.Patch.Server.Services;

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
        var index = 0;
        var current = this;
        for (var x = 0; x < hash.Count; x++)
            if (!current.Children.TryGetValue(hash[x], out current) || current == null) return null;
        return current;
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
        configuration.GetValue<string>("GameFiles", null), configuration.GetValue<int>("Build"), configuration.GetSection("AdditionalFiles")?.Get<AdditionalFile[]>(), logger)
    {

    }

    private static byte[] GetFileHash(string fileName)
    {
        using (var stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var sha = SHA1.Create())
        {
            return sha.ComputeHash(stream);
        }
    }

    protected GameDataFiles(string basePath, int build, IEnumerable<AdditionalFile> configuredFiles, ILogger<GameDataFiles> logger)
    {
        Logger = logger;
        var additionalFiles = configuredFiles?.ToList() ?? new List<AdditionalFile>();
        var dataFiles = new List<ArchiveFileBase>();
        _fileEntries = new List<FileEntry>();
        foreach (var indexFile in Directory.EnumerateFiles(basePath, "*.index", SearchOption.AllDirectories))
        {
            var stopwatch = Stopwatch.StartNew();
            var index = ArchiveFileBase.FromFile(indexFile);
            if (index == null) continue;
            stopwatch.Stop();
            logger.LogInformation($"Loaded file {indexFile} in {stopwatch.ElapsedMilliseconds}ms");
            dataFiles.Add(index);
        }

        foreach (var archiveFile in Directory.EnumerateFiles(basePath, "*.archive", SearchOption.AllDirectories))
        {
            var stopwatch = Stopwatch.StartNew();
            var archive = ArchiveFileBase.FromFile(archiveFile);
            if (archive == null) continue;
            stopwatch.Stop();
            logger.LogInformation($"Loaded file {archiveFile} in {stopwatch.ElapsedMilliseconds}ms");
            dataFiles.Add(archive);
        };


        //return new GameDataFiles(path, dataFiles.OfType<IndexFile>(), dataFiles.OfType<ArchiveFile>(), launcherPath, launcherHash, build);
        Build = build;
        _indexFiles = dataFiles.OfType<IndexFile>().ToList();
        // Warm up file reading now.
        // ReSharper disable ReturnValueOfPureMethodIsNotUsed - Preloading data.
        Logger.LogInformation("Preloading index file data...");
        var loadStopwatch = Stopwatch.StartNew();
        var fileList = _indexFiles.SelectMany(i => i.GetFilesystemEntries()).ToList();
        loadStopwatch.Stop();
        Logger.LogInformation("Loaded {fileCount} file and directory information objects from {count} index files in {time}ms", fileList.Count, _indexFiles.Count, loadStopwatch.ElapsedMilliseconds);
        // ReSharper restore ReturnValueOfPureMethodIsNotUsed
        _archiveFiles = dataFiles.OfType<ArchiveFile>().ToList();


        if (!additionalFiles.Any(x =>
                string.Equals(x.FileName, "Launcher.exe", StringComparison.OrdinalIgnoreCase)))
        {
            var launcherPath = Path.Combine(basePath, "Wildstar.exe");
            if (File.Exists(launcherPath))
            {
                Logger.LogWarning("Launcher.exe not specified in additional files, injecting default launcher from {path}", launcherPath);
                additionalFiles.Insert(0, new AdditionalFile()
                {
                    Path = launcherPath,
                    FileName = "Launcher.exe",
                    PublishBin = true
                });
            }
            else
                Logger.LogWarning("Launcher.exe not specified in additional files, and Wildstar.exe could not be found at {basePath}, patching will fail with Carbine/NCSoft launcher!", basePath);
        }


        Logger.LogInformation("Adding index file hashes to lookup and public names");
        foreach (var index in _indexFiles)
        {
            additionalFiles.Insert(0, new AdditionalFile()
            {
                Path = index.FileName,
                FileName = Path.GetFileName(index.FileName),
                PublishBin = true
            });
        }

        Logger.LogInformation("Adding client files (Client, Client64, Launcher)");
        foreach (var localFile in new[] { "Client", "Client64", "Launcher" }.Select(i => Path.Combine(basePath, i))
                     .Where(Directory.Exists)
                     .SelectMany(i => Directory.EnumerateFiles(i, "*.*", SearchOption.AllDirectories)))
        {
            additionalFiles.Insert(0, new AdditionalFile()
            {
                Path = localFile,
                FileName = null,
                PublishBin = false
            });
            //using (var fileStream = File.Open(localFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            //using (var sha = SHA1.Create())
            //{
            //    var binHash = sha.ComputeHash(fileStream);
            //    _lookup.Set(binHash, new LocalFile(localFile));
            //}
        }

        Logger.LogInformation("Indexing additional configured files");
        foreach (var file in additionalFiles)
        {
            var fileName = file.FileName ?? Path.GetFileName(file.Path);
            var hash = GetFileHash(file.Path);
            Logger.LogInformation("Adding file {name} ({filePath}) to index with hash {hash}", fileName, file.Path, BitConverter.ToString(hash).ToLower().Replace("-", ""));
            _lookup.Set(hash, new LocalFile(file.Path));
            if (!file.PublishBin) continue;
            Logger.LogInformation("Adding file {fileName} info to .bin file lookup", fileName);
            _fileEntries.Add(new FileEntry()
            {
                FileName = fileName,
                FilePath = file.Path,
                Hash = hash
            });
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
        var ret = _lookup.Find(hash)?.Value?.Open();
        if (Logger.IsEnabled(LogLevel.Trace))
        {
            Logger.LogTrace("OPEN: {hash} ({status})", ToHexString(hash),
                ret == null ? "NOT FOUND" : "Ok");
        }
        return ret;
    }

    public byte[] GetHash(string fileName)
    {
        var hash = OtherFiles.Where(i => i.FileName != null).FirstOrDefault(i =>
            string.Equals(i.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.Hash;
        if (Logger.IsEnabled(LogLevel.Trace))
        {
            Logger.LogTrace("LOOKUP: {fileName} ({status})", fileName, hash == null ? "NOT FOUND" : ToHexString(hash));
        }

        return hash;
    }

    private string ToHexString(byte[] bytes)
    {
        return BitConverter.ToString(bytes).ToLower().Replace("-", "");
    }

    private List<IndexFile> _indexFiles;
    private List<ArchiveFile> _archiveFiles;
    private List<FileEntry> _fileEntries;

    public IEnumerable<FileEntry> OtherFiles => _fileEntries?.ToArray() ?? Enumerable.Empty<FileEntry>();
    //public IEnumerable<(string filePath, string alias, byte[] hash)> OtherFiles => new[]
    //{
    //    (_launcherPath, "Launcher.exe", _launcherHash)
    //};
    public IEnumerable<IndexFile> IndexFiles => _indexFiles;
    public IEnumerable<ArchiveFile> ArchiveFiles => _archiveFiles;

    public ILogger<GameDataFiles> Logger { get; }
    public int Build { get; }

}
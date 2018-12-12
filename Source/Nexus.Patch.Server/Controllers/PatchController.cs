using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nexus.Archive;
using Nexus.Patch.Server.Services;

namespace Nexus.Patch.Server.Controllers
{
    [Route("")]
    [ApiController]
    public class PatchController : ControllerBase
    {
        public IGameDataFiles GameDataFiles { get; }
        public ILogger Logger { get; }
        public PatchController(IGameDataFiles gameDataFiles, ILogger<PatchController> logger)
        {
            GameDataFiles = gameDataFiles;
            Logger = logger;
        }
        [HttpHead("version.txt")]
        [HttpGet("version.txt")]
        public IActionResult VersionTxt()
        {
            return HandleResponse(Encoding.UTF8.GetBytes(GameDataFiles.Build.ToString()), "text/plain");
        }

        [HttpHead("mirrors.txt")]
        [HttpGet("mirrors.txt")]
        public IActionResult MirrorsTxt()
        {
            return HandleResponse(new byte[0], "text/plain");
        }

        [HttpHead("{name}.index.bin")]
        [HttpGet("{name}.index.bin")]
        public IActionResult IndexBinHandler(string name)
        {
            var indexFile = $"{name}.index";
            return HandleResponse(GameDataFiles.GetHash(indexFile));
            //var index = GameDataFiles.IndexFiles
            //    .FirstOrDefault(i => string.Equals(Path.GetFileName((string)i.FileName), indexFile, StringComparison.OrdinalIgnoreCase));
            //return HandleResponse(index?.FileHash);
        }

        [HttpHead("{version}/{name}.bin", Order = 20)]
        [HttpGet("{version}/{name}.bin", Order = 20)]
        public IActionResult IndexBinContentHandler(int version, string name)
        {
            return HandleResponse(GameDataFiles.GetHash(name));
            //var index = GameDataFiles.IndexFiles
            //    .FirstOrDefault(i => string.Equals(Path.GetFileName(i.FileName), name, StringComparison.OrdinalIgnoreCase));
            //if (index != null)
            //    return HandleResponse(index?.FileHash);

            //var fileId = GameDataFiles.IndexFiles.OrderByDescending(i => i.Header.DataHeader.Unknown2).SelectMany(i => i.GetFiles()).FirstOrDefault(i => string.Equals(i.FileName, name, StringComparison.OrdinalIgnoreCase));
            //if (fileId != null)
            //{
            //    Logger.LogInformation($"Found file {fileId.Path}");
            //}
            //else
            //{
            //    return HandleResponse(GameDataFiles.OtherFiles.Where(i => string.Equals(i.alias, name, StringComparison.OrdinalIgnoreCase))
            //        .Select(i => i.hash).FirstOrDefault());
            //}
            //return HandleResponse(fileId?.Hash);
        }

        private IActionResult HandleResponse(byte[] data, string contentType = "application/octet-stream")
        {
            return HandleResponse(data == null ? null : new MemoryStream(data), contentType);
        }

        private IActionResult HandleResponse(Stream stream, string contentType = "application/octet-stream")
        {
            if (stream == null) return NotFound();
            if (Request.Method == "GET")
                return File(stream, contentType);
            using (stream)
            {
                Response.ContentType = contentType;
                Response.ContentLength = stream.Length;
            }

            return Ok();
        }

        [HttpHead(@"{version}/{hash:regex(^[[a-fA-F0-9]]{{40,40}}$)}.bin", Order = 10)]
        [HttpGet(@"{version}/{hash:regex(^[[a-fA-F0-9]]{{40,40}}$)}.bin", Order = 10)]
        public IActionResult HashLookupHandler(int version, string hash)
        {
            //if (version != GameDataFiles.Build)
            //{
            //    Logger.LogInformation($"Version {version} does not match expected {GameDataFiles.Build}");
            //    return NotFound();
            //}
            var hashBytes = ToByteArray(hash);
            return HandleResponse(GameDataFiles.OpenHash(hashBytes));
            // Check index files first.
            Logger.LogInformation($"Checking index files for hash match: {hash}");
            var indexMatch = GameDataFiles.IndexFiles.FirstOrDefault(i => i.FileHash.SequenceEqual(hashBytes));
            Stream returnStream = null;
            if (indexMatch != null)
            {
                Logger.LogInformation($"{hash} matches index file {Path.GetFileName(indexMatch.FileName)}");
                returnStream = System.IO.File.Open(indexMatch.FileName, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite);
            }
            else
            {

                foreach (var archive in GameDataFiles.ArchiveFiles)
                {
                    Logger.LogInformation($"Searching archive {Path.GetFileName(archive.FileName)} for {hash}");
                    var dataEntry = archive.GetFileDataEntryByHash(hashBytes);
                    if (dataEntry != null)
                    {
                        Logger.LogInformation($"Found {hash} in archive, block #{dataEntry.BlockIndex}");
                        returnStream = archive.Open(dataEntry); // File(archive.Open(dataEntry), "application/octet-stream");
                        break;
                    }

                }

                if (returnStream == null)
                {
                    foreach (var index in GameDataFiles.IndexFiles)
                    {
                        Logger.LogInformation($"Searching index {Path.GetFileName(index.FileName)} for {hash}");
                        foreach (var fileInIndex in index.GetFiles())
                        {
                            if (fileInIndex.Hash.SequenceEqual(hashBytes))
                            {
                                Logger.LogInformation($"Found {hash} in index, attempting to open {fileInIndex.Path} as a local file.");
                                returnStream = OpenLocalFile(index, fileInIndex.Path);
                                break;
                            }
                        }
                        if (returnStream != null) break;
                    }
                }

                if (returnStream == null)
                {
                    if (GameDataFiles.OtherFiles.Any(i => i.hash.SequenceEqual(hashBytes)))
                    {

                        var aliasedDataFile = GameDataFiles.OtherFiles.First(i => i.hash.SequenceEqual(hashBytes));
                        Logger.LogInformation($"Found aliased file {aliasedDataFile.alias} with hash {hash} on disk at {aliasedDataFile.filePath}");
                        returnStream = System.IO.File.Open(aliasedDataFile.filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    }
                }
            }

            return HandleResponse(returnStream);
        }

        private Stream OpenLocalFile(IndexFile indexFile, string indexFileName)
        {
            // This has some weird rules to it.
            // Seems to be Name is trimmed of Data
            // For launcher, WildStar.exe is in ..\, but the rest of the files follow the rules. (IE: Launcher.index, and LauncherData.index reside in ..\launcher
            // So much nonsense. I didn't bother with it yet. But I will....
            var basePath = Path.GetDirectoryName(Path.GetDirectoryName(indexFile.FileName));
            var dataFolder = Path.GetFileNameWithoutExtension(indexFile.FileName);
            var path = Path.Combine(basePath, dataFolder, indexFileName);
            return System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        private static byte[] ToByteArray(string value)
        {
            return Enumerable.Range(0, value.Length / 2)
                .Select(x => Convert.ToByte(value.Substring(x * 2, 2), 16))
                .ToArray();
        }
    }
}
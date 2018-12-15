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
    [ResponseCache(Duration = 7776000, Location = ResponseCacheLocation.Any)]
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
        [ResponseCache()]
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
        }

        [HttpHead("{version}/{name}.bin", Order = 20)]
        [HttpGet("{version}/{name}.bin", Order = 20)]
        public IActionResult IndexBinContentHandler(int version, string name)
        {
            return HandleResponse(GameDataFiles.GetHash(name));
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
            var hashBytes = ToByteArray(hash);
            return HandleResponse(GameDataFiles.OpenHash(hashBytes));
        }

        private static byte[] ToByteArray(string value)
        {
            return Enumerable.Range(0, value.Length / 2)
                .Select(x => Convert.ToByte(value.Substring(x * 2, 2), 16))
                .ToArray();
        }
    }
}
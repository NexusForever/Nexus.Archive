using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nexus.Archive.Extensions;

namespace Nexus.Archive.Writer
{
    public class PatchServerPatchSource : IPatchSource
    {
        private readonly HttpClient _httpClient;
        public PatchServerPatchSource(Uri baseUri)
        {
            _httpClient = new HttpClient() {BaseAddress = baseUri};
        }

        private string GetUrlForHash(byte[] hash, IndexFile indexFile)
        {
            return $"{indexFile.RootIndex.BuildNumber}/{hash.ToHexString()}.bin";
        }



        public async Task<Stream> GetDataStream(byte[] hash, IndexFile indexFile, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync(GetUrlForHash(hash, indexFile), cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }
    }
}
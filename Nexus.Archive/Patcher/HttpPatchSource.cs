using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Nexus.Archive.Extensions;

namespace Nexus.Archive.Patcher;

public class HttpPatchSource : IPatchSource
{
    HttpClient _httpClient;
    private Task<int> _serverBuildTask = Task.FromException<int>(new InvalidOperationException("Not started"));
    public HttpPatchSource(string baseUrl) : this(new Uri(baseUrl)) { }

    public HttpPatchSource(HttpMessageHandler messageHandler, Uri baseUri)
    {
        _httpClient = new HttpClient(messageHandler)
        {
            BaseAddress = baseUri
        };

        _httpClient.DefaultRequestHeaders.AcceptEncoding.Clear();
        _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
        _httpClient.DefaultRequestHeaders.Connection.Clear();
        _httpClient.DefaultRequestHeaders.Connection.Add("keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Keep-Alive", "600");
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Nexus.Archive.Patcher", "1.0"));

    }
    public HttpPatchSource(Uri baseUri) : this(new HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.GZip,
            
    }, baseUri)
    {
    }

    public async Task<byte[]> GetFileHashAsync(string fileName, CancellationToken cancellationToken = default)
    {
        fileName = $"{fileName}.bin";
        var response = await _httpClient.GetAsync(fileName, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        if(result.Length != 20) throw new InvalidOperationException($"Expected a 20 byte SHA1 hash, got {result.Length} byte(s) instead.");
        return result;
    }
    public Task<byte[]> GetFileHashAsync(int buildVersion, string fileName, CancellationToken cancellationToken = default)
    {
        if(buildVersion > 0)
            return GetFileHashAsync($"{buildVersion}/{fileName}", cancellationToken);
        return GetFileHashAsync($"{fileName}", cancellationToken);
    }

    public async Task<int> GetServerBuildAsync(CancellationToken cancellationToken = default)
    {
        if (_serverBuildTask.IsCompleted && !_serverBuildTask.IsFaulted)
            return await _serverBuildTask;
        var response = await _httpClient.GetAsync("version.txt", cancellationToken);
        response.EnsureSuccessStatusCode();
        var task = _serverBuildTask = Task.FromResult(int.Parse(await response.Content.ReadAsStringAsync()));
        return await task;
    }
    public async Task<Stream> DownloadHashAsync(int build, byte[] hash, CancellationToken cancellationToken = default)
    {
        var requestPath = build <= 0 ? $"{hash.ToHexString()}.bin" : $"{build}/{hash.ToHexString()}.bin";
        var response = await _httpClient.GetAsync(requestPath, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }

    public async Task<Stream> DownloadFileAsync(int build, string fileName, CancellationToken cancellationToken = default)
    {
        var hash = await GetFileHashAsync(build, fileName, cancellationToken).ConfigureAwait(false);
        if (build <= 0)
        {
            build = await GetServerBuildAsync(cancellationToken).ConfigureAwait(false);
        }
        return await DownloadHashAsync(build, hash, cancellationToken);
    }

    public Task<Stream> DownloadFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        return DownloadFileAsync(0, fileName, cancellationToken);
    }
}
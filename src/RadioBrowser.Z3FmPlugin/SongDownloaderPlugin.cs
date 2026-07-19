
namespace RadioBrowser.Z3FmPlugin;

using System.Composition;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RadioBrowser.PluginContract;

[Export(typeof(ISongDownloaderPlugin))]
public class SongDownloaderPlugin : ISongDownloaderPlugin
{
    private const string BASE_URL = "https://m.z3.fm";

    [ImportingConstructor]
    public SongDownloaderPlugin()
    {
    }

    [Import]
    public IHttpClientFactory? HttpClientFactory { get; set; }

    private HttpClient? CreateHttpClient()
    {
        if (HttpClientFactory == null)
            throw new InvalidOperationException(
                "HttpClientFactory was not injected. Make sure the plugin is loaded via MEF.");

        var client = HttpClientFactory.CreateClient("Z3FmPlugin");
        client.BaseAddress = new Uri(BASE_URL);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("x-requested-with", "XMLHttpRequest");
        return client;
    }

    public string PluginName => "Z3.FM";

    public async Task<OperationResult<IEnumerable<Track>>> Search(string keyword, CancellationToken cancellationToken = default, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return new OperationResult<IEnumerable<Track>>
            {
                Success = false,
                Message = "Keyword is empty."
            };
        }

        try
        {
            var escapedKeyword = Uri.EscapeDataString(keyword);
            using (var client = CreateHttpClient())
            {
                if (client == null)
                {
                    return new OperationResult<IEnumerable<Track>>
                    {
                        Success = false,
                        Message = "Failed to create HTTP client."
                    };
                }
                var result = await client.GetFromJsonAsync<IEnumerable<Z3FmTrack>>($"mp3/search?keywords={escapedKeyword}", cancellationToken);
                return new OperationResult<IEnumerable<Track>>
                {
                    Success = true,
                    Result = result?.Select(t => t.ToTrack()) ?? Enumerable.Empty<Track>()
                };
            }
        }
        catch (Exception ex)
        {
            return new OperationResult<IEnumerable<Track>>
            {
                Success = false,
                Message = $"An error occurred while searching for tracks: {ex.Message}",
                Exception = ex
            };
        }
    }

    public async Task<OperationResult> Download(Track track, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return new OperationResult
            {
                Success = false,
                Message = "Destination path is empty."
            };
        }

        try
        {
            using (var client = CreateHttpClient())
            {
                if (client == null)
                {
                    return new OperationResult
                    {
                        Success = false,
                        Message = "Failed to create HTTP client."
                    };
                }

                var response = await client.GetAsync($"download/{track.Id}", cancellationToken);
                response.EnsureSuccessStatusCode();

                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                }

                var filePath = Path.Combine(destinationPath, track.FileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    await response.Content.CopyToAsync(fileStream, cancellationToken);
                }
            }

            return new OperationResult
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new OperationResult
            {
                Success = false,
                Message = $"An error occurred while downloading the track: {ex.Message}",
                Exception = ex
            };
        }
    }
}

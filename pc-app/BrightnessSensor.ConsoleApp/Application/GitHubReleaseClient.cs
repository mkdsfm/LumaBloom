using System.Text.Json;
using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Application;

internal sealed class GitHubReleaseClient(string repository) : IDisposable
{
    private readonly HttpClient _httpClient = CreateClient();
    private readonly string _repository = repository;

    public bool TryGetLatestPortableRelease(bool includePrerelease, out GitHubReleaseInfo? release, out string statusMessage)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildReleaseUrl(includePrerelease));
            using var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                release = null;
                statusMessage = $"GitHub Releases check failed: {(int)response.StatusCode} {response.ReasonPhrase}.";
                return false;
            }

            using var stream = response.Content.ReadAsStream();
            if (!includePrerelease)
            {
                var dto = JsonSerializer.Deserialize<GitHubReleaseDto>(stream);
                if (dto is null)
                {
                    release = null;
                    statusMessage = "GitHub release response did not contain a usable release.";
                    return false;
                }

                return TryCreateReleaseInfo(dto, includePrerelease, out release, out statusMessage);
            }

            var releases = JsonSerializer.Deserialize<List<GitHubReleaseDto>>(stream);
            var dtoWithAsset = releases?
                .Where(dto => !dto.Draft)
                .FirstOrDefault(HasPortableAsset);
            if (dtoWithAsset is null)
            {
                release = null;
                statusMessage = "GitHub releases response did not contain a usable portable Windows package.";
                return false;
            }

            return TryCreateReleaseInfo(dtoWithAsset, includePrerelease, out release, out statusMessage);
        }
        catch (Exception exception)
        {
            release = null;
            statusMessage = $"GitHub Releases check failed: {exception.Message}";
            return false;
        }
    }

    public void DownloadToFile(Uri downloadUrl, string destinationPath, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        using var response = _httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var source = response.Content.ReadAsStream(cancellationToken);
        using var destination = File.Create(destinationPath);
        source.CopyTo(destination);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LumaBloom-PC-App");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private string BuildReleaseUrl(bool includePrerelease)
    {
        return includePrerelease
            ? $"https://api.github.com/repos/{_repository}/releases?per_page=20"
            : $"https://api.github.com/repos/{_repository}/releases/latest";
    }

    private static bool TryCreateReleaseInfo(
        GitHubReleaseDto dto,
        bool includePrerelease,
        out GitHubReleaseInfo? release,
        out string statusMessage)
    {
        if (string.IsNullOrWhiteSpace(dto.TagName))
        {
            release = null;
            statusMessage = "GitHub release response did not contain a usable tag.";
            return false;
        }

        var asset = dto.Assets?
            .FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate.Name) &&
                candidate.Name.EndsWith("_win-x64-portable.zip", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(candidate.BrowserDownloadUrl, UriKind.Absolute, out _));
        if (asset is null || !Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var packageUri))
        {
            release = null;
            statusMessage = "Latest GitHub release does not contain the portable Windows package.";
            return false;
        }

        release = new GitHubReleaseInfo(
            dto.TagName,
            NormalizeVersion(dto.TagName),
            packageUri,
            asset.Name!,
            dto.HtmlUrl ?? packageUri.ToString(),
            dto.Prerelease);
        statusMessage = includePrerelease
            ? $"Latest GitHub release (including prereleases): {release.Version}."
            : $"Latest GitHub release: {release.Version}.";
        return true;
    }

    private static bool HasPortableAsset(GitHubReleaseDto dto)
    {
        return !string.IsNullOrWhiteSpace(dto.TagName) &&
               dto.Assets?.Any(candidate =>
                   !string.IsNullOrWhiteSpace(candidate.Name) &&
                   candidate.Name.EndsWith("_win-x64-portable.zip", StringComparison.OrdinalIgnoreCase) &&
                   Uri.TryCreate(candidate.BrowserDownloadUrl, UriKind.Absolute, out _)) == true;
    }

    private static string NormalizeVersion(string tag)
    {
        return tag.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? tag[1..]
            : tag;
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubAssetDto>? Assets { get; init; }
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }
    }
}

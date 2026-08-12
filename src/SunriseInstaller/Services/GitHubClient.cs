using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Sunrise.Installer.Services;

public sealed class GitHubClient : IDisposable
{
    private const long MaxDownloadBytes = 512L * 1024 * 1024;
    private readonly HttpClient httpClient;
    private readonly InstallerLog log;

    public GitHubClient(InstallerLog log, HttpMessageHandler? handler = null)
    {
        this.log = log;
        httpClient = handler is null ? new HttpClient() : new HttpClient(handler);
        httpClient.Timeout = TimeSpan.FromMinutes(10);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SunriseInstaller/1.0");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<ReleaseInfo> GetLatestReleaseAsync(
        string owner,
        string repository,
        Func<IReadOnlyList<ReleaseAsset>, ReleaseAsset> selectAsset,
        CancellationToken cancellationToken)
    {
        Uri endpoint = new($"https://api.github.com/repos/{owner}/{repository}/releases/latest");
        using HttpResponseMessage response = await SendAsync(endpoint, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InstallerException(
                owner.Equals(AppConstants.SunriseOwner, StringComparison.OrdinalIgnoreCase)
                    ? "No Sunrise release is published yet. Publish a GitHub release and try again."
                    : $"No release was found for {owner}/{repository}.");
        }

        await EnsureSuccessAsync(response, cancellationToken);
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        JsonElement root = document.RootElement;
        string tag = RequiredString(root, "tag_name");
        List<ReleaseAsset> assets = [];

        foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
        {
            string? digest = asset.TryGetProperty("digest", out JsonElement digestElement) &&
                digestElement.ValueKind == JsonValueKind.String
                    ? digestElement.GetString()
                    : null;
            assets.Add(new ReleaseAsset(
                RequiredString(asset, "name"),
                new Uri(RequiredString(asset, "browser_download_url")),
                asset.GetProperty("size").GetInt64(),
                digest));
        }

        ReleaseAsset selected = selectAsset(assets);
        if (selected.Size <= 0 || selected.Size > MaxDownloadBytes)
        {
            throw new InstallerException($"The release asset {selected.Name} has an unexpected size.");
        }

        log.Info("release_found", $"Found release {tag}.", ("repo", $"{owner}/{repository}"), ("tag", tag));
        return new ReleaseInfo(tag, selected);
    }

    public async Task DownloadAsync(
        ReleaseAsset asset,
        string destination,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(asset.DownloadUrl, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        long? contentLength = response.Content.Headers.ContentLength;
        if (contentLength > MaxDownloadBytes)
        {
            throw new InstallerException($"The download for {asset.Name} is larger than expected.");
        }

        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
        long downloaded = await FileCopy.CopyAsync(
            input,
            destination,
            contentLength ?? asset.Size,
            progress,
            cancellationToken);
        if (asset.Size > 0 && downloaded != asset.Size)
        {
            throw new InstallerException($"The download for {asset.Name} was incomplete.");
        }

        await VerifyDigestAsync(asset, destination, cancellationToken);
        log.Info("download_done", $"Downloaded {asset.Name}.", ("asset", asset.Name), ("bytes", downloaded));
    }

    public static ReleaseAsset SelectSunriseAsset(IReadOnlyList<ReleaseAsset> assets) =>
        assets.FirstOrDefault(asset =>
            asset.Name.Equals("steam_api64.dll", StringComparison.OrdinalIgnoreCase))
        ?? throw new InstallerException("The latest Sunrise release needs a steam_api64.dll asset.");

    public static ReleaseAsset SelectDepotDownloaderAsset(IReadOnlyList<ReleaseAsset> assets) =>
        assets.FirstOrDefault(asset =>
            asset.Name.Equals("DepotDownloader-windows-x64.zip", StringComparison.OrdinalIgnoreCase))
        ?? throw new InstallerException("The DepotDownloader release has no Windows x64 asset.");

    public void Dispose() => httpClient.Dispose();

    private async Task<HttpResponseMessage> SendAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InstallerException("The download timed out. Check your connection and try again.");
        }
        catch (HttpRequestException exception)
        {
            throw new InstallerException("GitHub could not be reached. Check your connection and try again.", exception);
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string detail = await response.Content.ReadAsStringAsync(cancellationToken);
        if (detail.Length > 300)
        {
            detail = detail[..300];
        }

        if (response.StatusCode == HttpStatusCode.Forbidden &&
            response.Headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string>? values) &&
            values.Contains("0"))
        {
            throw new InstallerException("GitHub's download limit was reached. Wait and try again.");
        }

        throw new InstallerException(
            $"GitHub returned HTTP {(int)response.StatusCode}. {detail}".Trim());
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InstallerException($"GitHub omitted {propertyName} from its release response.");
        }

        return property.GetString()!;
    }

    private static async Task VerifyDigestAsync(
        ReleaseAsset asset,
        string destination,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(asset.Digest))
        {
            return;
        }

        const string prefix = "sha256:";
        if (!asset.Digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InstallerException($"GitHub supplied an unsupported digest for {asset.Name}.");
        }

        string actual = await FileIntegrity.Sha256Async(destination, cancellationToken);
        string expected = asset.Digest[prefix.Length..].Trim();
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InstallerException($"The SHA-256 check failed for {asset.Name}.");
        }
    }
}

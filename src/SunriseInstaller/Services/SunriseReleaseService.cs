namespace Sunrise.Installer.Services;

public sealed class SunriseReleaseService(
    GitHubClient gitHub,
    InstallerLog log,
    string? localPayloadPath)
{
    private const long MaxPayloadBytes = 512L * 1024 * 1024;

    public Task<ReleaseInfo> GetLatestAsync(CancellationToken cancellationToken)
    {
        if (localPayloadPath is not null)
        {
            return GetLocalReleaseAsync(localPayloadPath, cancellationToken);
        }

        return gitHub.GetLatestReleaseAsync(
            AppConstants.SunriseOwner,
            AppConstants.SunriseRepository,
            GitHubClient.SelectSunriseAsset,
            cancellationToken);
    }

    public async Task<PreparedPayload> PrepareLatestAsync(
        string installRoot,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        ReleaseInfo release = await GetLatestAsync(cancellationToken);
        string stagingRoot = Path.Combine(installRoot, ".sunrise", "staging");
        Directory.CreateDirectory(stagingRoot);
        string stagingDirectory = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            string dllPath = Path.Combine(stagingDirectory, "steam_api64.dll");
            if (localPayloadPath is null)
            {
                await gitHub.DownloadAsync(release.Asset, dllPath, progress, cancellationToken);
            }
            else
            {
                await CopyLocalAsync(localPayloadPath, release.Asset, dllPath, progress, cancellationToken);
            }

            FileIntegrity.ValidateAmd64Dll(dllPath);
            string hash = await FileIntegrity.Sha256Async(dllPath, cancellationToken);
            log.Info("payload_ready", $"Prepared Sunrise {release.Tag}.", ("tag", release.Tag), ("sha256", hash));
            return new PreparedPayload(release, stagingDirectory, dllPath, hash);
        }
        catch
        {
            FileCleanup.TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    public static void Cleanup(PreparedPayload payload) =>
        FileCleanup.TryDeleteDirectory(payload.StagingDirectory);

    private async Task<ReleaseInfo> GetLocalReleaseAsync(
        string path,
        CancellationToken cancellationToken)
    {
        FileInfo file = new(path);
        if (!file.Exists)
        {
            throw new InstallerException($"The local test payload does not exist: {path}");
        }

        if (file.Length <= 0 || file.Length > MaxPayloadBytes)
        {
            throw new InstallerException($"The local test payload {file.Name} has an unexpected size.");
        }

        string extension = file.Extension;
        if (!extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new InstallerException("The local test payload must be a DLL file.");
        }

        string hash = await FileIntegrity.Sha256Async(file.FullName, cancellationToken);
        ReleaseAsset asset = new(
            file.Name,
            new Uri(file.FullName, UriKind.Absolute),
            file.Length,
            $"sha256:{hash}");
        log.Info(
            "release_found",
            "Using local test payload.",
            ("repo", "local"),
            ("tag", "local-test"),
            ("sha256", hash));
        return new ReleaseInfo("local-test", asset);
    }

    private static async Task CopyLocalAsync(
        string sourcePath,
        ReleaseAsset asset,
        string destination,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(0);
        long copied = await FileCopy.CopyAsync(
            sourcePath,
            destination,
            asset.Size,
            progress,
            cancellationToken);

        if (copied != asset.Size)
        {
            throw new InstallerException("The local test payload changed while it was being read.");
        }

        string actualHash = await FileIntegrity.Sha256Async(destination, cancellationToken);
        string expectedHash = asset.Digest!["sha256:".Length..];
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InstallerException("The local test payload changed while it was being read.");
        }

        progress?.Report(100);
    }

}

namespace Sunrise.Installer.Services;

public sealed class PayloadInstaller(InstallerLog log)
{
    public void DeleteSunriseData(string installRoot)
    {
        string x64Directory = Path.GetFullPath(Path.Combine(installRoot, "bin", "x64"));
        string expectedPrefix = x64Directory + Path.DirectorySeparatorChar;
        string sunriseDirectory = Path.GetFullPath(Path.Combine(x64Directory, "Sunrise"));
        if (!sunriseDirectory.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InstallerException("The Sunrise data folder path is unsafe.");
        }

        if (!Directory.Exists(sunriseDirectory))
        {
            return;
        }

        Directory.Delete(sunriseDirectory, true);
        log.Info("data_deleted", "Deleted the Sunrise data folder.", ("name", "Sunrise"));
    }

    public async Task ApplyAsync(
        string installRoot,
        PreparedPayload payload,
        bool preserveDepotDll,
        CancellationToken cancellationToken)
    {
        string target = Path.Combine(installRoot, AppConstants.ModRelativePath);
        string? targetDirectory = Path.GetDirectoryName(target);
        if (targetDirectory is null || !Directory.Exists(targetDirectory))
        {
            throw new InstallerException("The game bin\\x64 folder is missing. Run Install or Repair first.");
        }

        if (preserveDepotDll && File.Exists(target))
        {
            string original = Path.Combine(installRoot, ".sunrise", "original", "steam_api64.dll");
            await CopyAtomicAsync(target, original, cancellationToken);
        }

        string temporaryTarget = Path.Combine(targetDirectory, $".steam_api64-{Guid.NewGuid():N}.tmp");
        try
        {
            await FileCopy.CopyDurableAsync(payload.DllPath, temporaryTarget, cancellationToken);
            File.Move(temporaryTarget, target, true);
        }
        catch (IOException exception) when (File.Exists(target))
        {
            throw new InstallerException(
                "steam_api64.dll could not be replaced. Close Destiny 2 and any tool using the game folder.",
                exception);
        }
        finally
        {
            FileCleanup.TryDeleteFile(temporaryTarget);
        }

        string installedHash = await FileIntegrity.Sha256Async(target, cancellationToken);
        if (!installedHash.Equals(payload.DllSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InstallerException("The installed Sunrise DLL failed its SHA-256 check.");
        }

        log.Info("payload_installed", $"Installed Sunrise {payload.Release.Tag}.", ("tag", payload.Release.Tag));
    }

    private static Task CopyAtomicAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporaryPath = destination + $".{Guid.NewGuid():N}.tmp";
        return CopyAndMoveAsync(source, temporaryPath, destination, cancellationToken);
    }

    private static async Task CopyAndMoveAsync(
        string source,
        string temporaryPath,
        string destination,
        CancellationToken cancellationToken)
    {
        try
        {
            await FileCopy.CopyDurableAsync(source, temporaryPath, cancellationToken);
            File.Move(temporaryPath, destination, true);
        }
        finally
        {
            FileCleanup.TryDeleteFile(temporaryPath);
        }
    }

}

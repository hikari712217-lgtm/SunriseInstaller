using System.Diagnostics;

namespace Sunrise.Installer.Services;

public static class Preflight
{
    public static string PrepareInstallRoot(string installRoot, long requiredFreeBytes)
    {
        if (string.IsNullOrWhiteSpace(installRoot))
        {
            throw new InstallerException("Select an install folder.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(installRoot.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InstallerException("The install folder path is not valid.", exception);
        }

        string? pathRoot = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(pathRoot) || fullPath.Equals(pathRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InstallerException("Choose a folder inside a drive, not the drive itself.");
        }

        Directory.CreateDirectory(fullPath);
        EnsureWritable(fullPath);
        EnsureFreeSpace(fullPath, requiredFreeBytes);
        return fullPath;
    }

    public static void EnsureGameIsClosed()
    {
        Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppConstants.GameExecutableName));
        try
        {
            if (processes.Length > 0)
            {
                throw new InstallerException("Close Destiny 2 before installing, repairing, or updating Sunrise.");
            }
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static string FormatBytes(long bytes)
    {
        const double gibibyte = 1024d * 1024 * 1024;
        return $"{bytes / gibibyte:0.0} GiB";
    }

    private static void EnsureWritable(string directory)
    {
        string probe = Path.Combine(directory, $".sunrise-write-{Guid.NewGuid():N}.tmp");
        try
        {
            using FileStream stream = new(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.WriteByte(0);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new InstallerException(
                "The installer cannot write to this folder. Choose another folder or run it with the needed access.",
                exception);
        }
        finally
        {
            try
            {
                File.Delete(probe);
            }
            catch
            {
            }
        }
    }

    private static void EnsureFreeSpace(string directory, long requiredFreeBytes)
    {
        string root = Path.GetPathRoot(directory)
            ?? throw new InstallerException("The install drive could not be found.");
        DriveInfo drive;
        try
        {
            drive = new DriveInfo(root);
        }
        catch (ArgumentException exception)
        {
            throw new InstallerException("The selected install drive is not supported.", exception);
        }

        if (!drive.IsReady)
        {
            throw new InstallerException("The selected install drive is not ready.");
        }

        if (drive.AvailableFreeSpace < requiredFreeBytes)
        {
            throw new InstallerException(
                $"Not enough free space. {FormatBytes(requiredFreeBytes)} is required, " +
                $"but {FormatBytes(drive.AvailableFreeSpace)} is available.");
        }
    }
}

using System.IO.Compression;

namespace Sunrise.Installer.Services;

public static class SafeZip
{
    private const long MaxEntryBytes = 512L * 1024 * 1024;
    private const long MaxArchiveBytes = 1024L * 1024 * 1024;

    public static async Task ExtractAllAsync(
        string archivePath,
        string destination,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        string root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        long totalBytes = 0;

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectLink(entry);
            totalBytes = checked(totalBytes + entry.Length);
            if (entry.Length > MaxEntryBytes || totalBytes > MaxArchiveBytes)
            {
                throw new InstallerException("The downloaded archive is larger than expected.");
            }

            string outputPath = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!outputPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InstallerException("The downloaded archive contains an unsafe path.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(outputPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await using Stream input = entry.Open();
            await using FileStream output = new(
                outputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 128,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await input.CopyToAsync(output, cancellationToken);
        }
    }


    private static void RejectLink(ZipArchiveEntry entry)
    {
        int unixType = (entry.ExternalAttributes >> 16) & 0xF000;
        if (unixType == 0xA000)
        {
            throw new InstallerException("The downloaded archive contains an unsupported link.");
        }
    }
}

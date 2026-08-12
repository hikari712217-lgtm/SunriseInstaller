namespace Sunrise.Installer.Services;

internal static class FileCopy
{
    private const int BufferSize = 128 * 1024;

    public static async Task<long> CopyAsync(
        string source,
        string destination,
        long? totalBytes,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        await using FileStream input = new(
            source,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await CopyAsync(
            input,
            destination,
            totalBytes,
            progress,
            FileOptions.Asynchronous | FileOptions.SequentialScan,
            cancellationToken);
    }

    public static Task<long> CopyAsync(
        Stream input,
        string destination,
        long? totalBytes,
        IProgress<int>? progress,
        CancellationToken cancellationToken) =>
        CopyAsync(
            input,
            destination,
            totalBytes,
            progress,
            FileOptions.Asynchronous | FileOptions.SequentialScan,
            cancellationToken);

    public static async Task CopyDurableAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        await using FileStream input = new(
            source,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await CopyAsync(
            input,
            destination,
            totalBytes: null,
            progress: null,
            FileOptions.Asynchronous | FileOptions.WriteThrough,
            cancellationToken);
    }

    private static async Task<long> CopyAsync(
        Stream input,
        string destination,
        long? totalBytes,
        IProgress<int>? progress,
        FileOptions outputOptions,
        CancellationToken cancellationToken)
    {
        await using FileStream output = new(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            outputOptions);

        byte[] buffer = new byte[BufferSize];
        long copied = 0;
        while (true)
        {
            int read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;
            if (totalBytes > 0)
            {
                progress?.Report((int)Math.Clamp(copied * 100 / totalBytes.Value, 0, 100));
            }
        }

        await output.FlushAsync(cancellationToken);
        return copied;
    }
}

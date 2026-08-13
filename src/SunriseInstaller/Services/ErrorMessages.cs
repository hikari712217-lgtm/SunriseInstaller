namespace Sunrise.Installer.Services;

public static class ErrorMessages
{
    public static string For(Exception exception)
    {
        Exception current = exception;
        while (current is AggregateException { InnerExceptions.Count: 1 } aggregate)
        {
            current = aggregate.InnerExceptions[0];
        }

        return current switch
        {
            InstallerException => current.Message,
            OperationCanceledException => "The operation was cancelled.",
            UnauthorizedAccessException =>
                "Access was denied. Choose a writable folder or run with the needed access.",
            PathTooLongException => "The selected path is too long. Choose a shorter install folder.",
            FileNotFoundException or DirectoryNotFoundException =>
                "An expected file was missing. See the installer log for details.",
            IOException ioException when IsDiskFull(ioException) =>
                "The drive ran out of space. Free some space and run Repair.",
            IOException ioException when IsSharingViolation(ioException) =>
                "A file is in use by another program. Close the game and any tool using the install folder.",
            IOException => "A file could not be read or written. Check free space, access, and open programs.",
            _ => "The operation failed unexpectedly. See the installer log for details.",
        };
    }

    private static bool IsDiskFull(IOException exception)
    {
        int code = exception.HResult & 0xFFFF;
        return code is 0x27 or 0x70;
    }

    private static bool IsSharingViolation(IOException exception)
    {
        int code = exception.HResult & 0xFFFF;
        return code is 0x20 or 0x21;
    }
}

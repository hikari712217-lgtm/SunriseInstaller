namespace Sunrise.Installer;

public sealed record AppOptions(string? TestPayloadPath)
{
    public bool IsTestMode => TestPayloadPath is not null;

    public static bool TryParse(string[] args, out AppOptions options, out string? error)
    {
        options = new AppOptions(TestPayloadPath: null);
        error = null;

        if (args.Length == 0)
        {
            return true;
        }

        if (args.Length != 2 ||
            (!args[0].Equals("-test", StringComparison.OrdinalIgnoreCase) &&
             !args[0].Equals("--test", StringComparison.OrdinalIgnoreCase)))
        {
            error = "Usage: SunriseInstaller.exe -test \"<local DLL>\"";
            return false;
        }

        try
        {
            string path = Path.GetFullPath(args[1].Trim());
            if (!File.Exists(path))
            {
                error = $"The local file does not exist: {path}";
                return false;
            }

            string extension = Path.GetExtension(path);
            if (!extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                error = "The local file must be a DLL file.";
                return false;
            }

            options = new AppOptions(path);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = "The local file path is not valid.";
            return false;
        }
    }
}

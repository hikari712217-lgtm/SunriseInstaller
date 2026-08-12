namespace Sunrise.Installer;

public static class AppConstants
{
    public const uint SteamAppId = 1085660;
    public const string GameExecutableName = "destiny2.exe";
    public const string ModRelativePath = @"bin\x64\steam_api64.dll";
    public const string SunriseOwner = "stanuwu";
    public const string SunriseRepository = "Sunrise";
    public const string DepotDownloaderOwner = "SteamRE";
    public const string DepotDownloaderRepository = "DepotDownloader";
    public const long FreshInstallFreeBytes = 110L * 1024 * 1024 * 1024;
    public const long RepairFreeBytes = 5L * 1024 * 1024 * 1024;
    public const long UpdateFreeBytes = 256L * 1024 * 1024;

    public static readonly DepotSpec[] Depots =
    [
        new(1085661, 7180122903232116872),
        new(1085662, 2210332166360342287),
    ];

    public static string AppDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SunriseInstaller");
}

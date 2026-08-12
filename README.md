# Sunrise Installer

Installer for [Sunrise](https://github.com/stanuwu/Sunrise).

## License

Sunrise Installer is Copyright (C) 2026 stanuwu. It is licensed under version 2 of the
[GNU General Public License](LICENSE). The full terms are stored with the project.

## DepotDownloader Credit and License

Sunrise Installer uses [DepotDownloader](https://github.com/SteamRE/DepotDownloader) to download Steam depots. DepotDownloader is developed by the SteamRE Team and uses SteamKit2.
Copyright for DepotDownloader belongs to its authors and contributors.

DepotDownloader is also licensed under GNU GPL version 2. Its full license is stored in
[DEPOTDOWNLOADER_LICENSE.txt](DEPOTDOWNLOADER_LICENSE.txt). The published Sunrise Installer contains
no DepotDownloader binary. DepotDownloader is not linked and is not a package or project dependency.

On first use, Sunrise Installer downloads the official `DepotDownloader-windows-x64.zip` release
from SteamRE into `%LOCALAPPDATA%`. It checks the GitHub digest when supplied, then extracts and
starts DepotDownloader as a separate program.

## Logo credit

Logo credit: [Solus](https://www.youtube.com/@Solus-yt).

## Operations

| action         | result                                                              |
|----------------|---------------------------------------------------------------------|
| Install        | Downloads the correct version of the game and installs the mod.     |
| Repair         | Validates the game, deletes the user config and reinstalls the mod. |
| Check / Update | Checks if a new mod version is released and installs it.            |

Uses Steam app `1085660` with these manifests:

| depot     | manifest              |
|-----------|-----------------------|
| `1085661` | `7180122903232116872` |
| `1085662` | `2210332166360342287` |

Install requires ~110 GiB of free space.

## Sunrise releases

Downloads latest from `https://github.com/stanuwu/Sunrise/releases`.
## Test mode

Run the installer with a local Sunrise DLL:

```powershell
.\SunriseInstaller.exe -test "C:\path\to\steam_api64.dll"
```

## Local data
`%LOCALAPPDATA%\SunriseInstaller\tools`. 

`%LOCALAPPDATA%\SunriseInstaller\logs`.

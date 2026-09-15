# Vault Injector

A Windows tray utility that fetches secrets from HashiCorp Vault and pastes them at the current
cursor position via a global hotkey, so you never have to copy/paste secrets through an
intermediate app.

## How it works

1. The app runs in the background with a tray icon - no taskbar window.
2. You log in to Vault once (paste a token you obtained yourself, e.g. via `vault login
   -method=oidc`) from the Settings window.
3. You configure a base path and a list of secret "menu entries" (an alias, a Vault path relative
   to the base path, and either a specific field or the whole secret as JSON).
4. Press the configured hotkey (default **Ctrl+Alt+V**) anywhere in Windows - a small menu pops up
   at your cursor listing your aliases.
5. Pick one: the app fetches that value from Vault, puts it on the clipboard, and simulates
   Ctrl+V into whatever window/field had focus. The previous clipboard contents are restored a
   moment later (configurable).

## Requirements

- Windows 11 (also runs on Windows 10)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (17.8+) with the **.NET desktop development** workload, or the `dotnet` CLI
- A reachable Vault server with a KV (v1 or v2) secrets engine mounted, and a token/policy that can
  read the paths you want to expose

## Building and running

Open `VaultInjector.sln` in Visual Studio, set **VaultInjector.App** as the startup project, and
press F5 - or from the CLI:

```powershell
dotnet build VaultInjector.sln
dotnet run --project src/VaultInjector.App
```

`dotnet test` runs the unit tests (`src/VaultInjector.Tests`), which cover the platform-agnostic
logic in `VaultInjector.Core` (config persistence, Vault path building) without needing Windows.

## Building an installer

`src/VaultInjector.App/Properties/PublishProfiles/ClickOnceProfile.pubxml` publishes a self-contained
ClickOnce installer (no separate .NET install needed on the target machine). ClickOnce's manifest
generation needs the *full-framework* MSBuild from a Visual Studio install - the cross-platform `dotnet`
CLI's MSBuild can restore for it but can't run the publish step itself (`MSB4803 UpdateManifest is not
supported on the .NET Core version of MSBuild`). From a **Developer PowerShell for VS 2022** prompt:

```powershell
dotnet restore src\VaultInjector.App\VaultInjector.App.csproj -r win-x64
msbuild src\VaultInjector.App\VaultInjector.App.csproj `
  /t:Publish /p:PublishProfile=ClickOnceProfile /p:Configuration=Release `
  /p:RuntimeIdentifier=win-x64 /p:SelfContained=true
```

(or open the project in Visual Studio and use **Build > Publish VaultInjector.App**, which picks up the
same profile). The installer lands in
`src/VaultInjector.App/bin/Release/net8.0-windows/win-x64/app.publish/` - hand someone the whole folder
and have them double-click `VaultInjector.application` to install it (Start Menu shortcut + an uninstall
entry under Settings > Apps, both added automatically). There's no separate `setup.exe`; ClickOnce for
.NET (as opposed to .NET Framework) installs directly from the `.application` manifest.

Because manifest signing is off (`SignManifests=false` in the profile - signing needs a code-signing
certificate this repo doesn't have), Windows SmartScreen will flag the installer as from an "Unknown
Publisher" on first run; click through it, or get a cert and flip `SignManifests`/set
`ManifestCertificateThumbprint` in the profile to remove that warning. Bump `ApplicationVersion` in the
profile before each new release so ClickOnce treats it as an upgrade rather than a downgrade.

### Hosting it on the internal static server

`ClickOnceHostedProfile.pubxml` (same folder) is the same setup but published straight onto
`\\cm-dev.adrentech.com\static\VaultInjector\`, which backs `https://cm-dev.adrentech.com/static/VaultInjector/`
- so instead of handing someone a folder, they just browse to
`https://cm-dev.adrentech.com/static/VaultInjector/VaultInjector.application` and install from there. It
also turns on ClickOnce auto-update (checked in the background after each launch, so the tray app never
blocks startup on a network check) - already-installed clients pick up a new version next time they run
it, no reinstall needed.

```powershell
dotnet restore src\VaultInjector.App\VaultInjector.App.csproj -r win-x64
msbuild src\VaultInjector.App\VaultInjector.App.csproj `
  /t:Publish /p:PublishProfile=ClickOnceHostedProfile /p:Configuration=Release `
  /p:RuntimeIdentifier=win-x64 /p:SelfContained=true
```

Needs write access to the `\\cm-dev.adrentech.com\static` share. As of this writing that account can
create and overwrite files there but **not delete them** - harmless for normal republishing (ClickOnce
mostly adds new versioned folders and overwrites the top-level `.application` manifest, it doesn't need
to delete anything), but don't count on ever being able to clean up a botched publish there yourself.
Same version-bumping rule as above: bump `ApplicationVersion` (reset `ApplicationRevision` to 0) before
every publish, or existing installs won't see it as an update.

### Building a single setup.exe

Both profiles above are ClickOnce, which installs into a versioned `Application Files\...` tree with no
single installer file. For one plain `VaultInjectorSetup.exe` instead, `FolderProfile.pubxml` publishes a
self-contained, single-file build (no separate .NET install needed on the target machine, and unlike
ClickOnce this doesn't need full-framework MSBuild - plain `dotnet publish` is enough), and
`installer/VaultInjector.iss` (an [Inno Setup](https://jrsoftware.org/isinfo.php) script, not bundled
with this repo - install it separately, e.g. `winget install JRSoftware.InnoSetup`) wraps that into the
installer:

```powershell
dotnet publish src\VaultInjector.App\VaultInjector.App.csproj -p:PublishProfile=FolderProfile -c Release
iscc installer\VaultInjector.iss
```

The installer lands at `installer\Output\VaultInjectorSetup.exe`. It installs per-user (no admin prompt,
matching the app itself never elevating), adds a Start Menu shortcut and an optional desktop shortcut,
registers a normal uninstall entry under Settings > Apps, and offers to launch the app once install
finishes. Bump `AppVersion` in the script before each release; `AppId` must never change across releases
- Inno Setup uses it, not the name, to recognize a reinstall as an upgrade of the same product rather
than a second copy.
> *compiled* on non-Windows machines (e.g. WSL) for CI/dev-loop convenience. It has no effect on
> Windows and the app must still be *run* on Windows, since it depends on Win32 APIs, WPF and
> Windows Forms.

## Project layout

```
VaultInjector.sln
src/
  VaultInjector.Core/    Platform-agnostic domain logic: config model, JSON config store,
                          Vault client wrapper (VaultSharp), path building. No Windows/WPF deps.
  VaultInjector.App/     The Windows app: tray icon, global hotkey (Win32 RegisterHotKey),
                          clipboard/paste simulation, DPAPI token storage, Settings UI (WPF).
  VaultInjector.Tests/   xUnit tests for VaultInjector.Core.
```

## Configuration

Everything is set from the tray icon's **Open Settings...** menu item.

| Setting | Where | Notes |
|---|---|---|
| Vault address | Vault Connection tab | e.g. `https://vault.example.com:8200` |
| Namespace | Vault Connection tab | Optional, Vault Enterprise only |
| KV mount path / version | Vault Connection tab | e.g. mount `secret`, KV v2 (the default) |
| Base path | Vault Connection tab | Prefix applied to every secret entry below it, e.g. `myapp/prod` |
| Login | Vault Connection tab | Paste a token you obtained yourself (any auth method, e.g. `vault login -method=oidc`); the app validates it against `auth/token/lookup-self` and stores it encrypted |
| Secret entries (alias, path, field or whole-JSON) | Secrets Menu tab | What shows up in the context menu, and what gets pasted for each item |
| Hotkey | Hotkey tab | Global shortcut that opens the context menu at the cursor (default Ctrl+Alt+V) |
| Start with Windows | General tab | Adds/removes a per-user Run registry entry - no admin rights needed |
| Clipboard restore | General tab | Whether/how long before the clipboard is restored after a paste |
| Log level / retained days | General tab | See Logging below |

Non-secret settings are stored as JSON at `%AppData%\VaultInjector\config.json`.

## Security notes

- The Vault token is never written to `config.json`. It's encrypted with Windows DPAPI
  (`CurrentUser` scope) and stored separately at `%AppData%\VaultInjector\token.dat` - only the
  same Windows user account, on the same machine, can decrypt it.
- Secret *values* are never logged. Vault calls are logged with path, HTTP method, status code and
  timing only (see Logging).
- The clipboard briefly holds the pasted secret; by default the app restores whatever was on the
  clipboard before the paste after ~1.5s (configurable). Disable this only if you understand the
  tradeoff.
- `Skip TLS certificate verification` exists for trusted internal test environments only - never
  enable it against a Vault reachable over an untrusted network.
- The app requests no elevation (`asInvoker`) and needs none.

## Logging

Serilog writes daily-rolling log files to `%AppData%\VaultInjector\logs\log-YYYYMMDD.txt`
(reachable from the tray menu's **Open Logs Folder**). Every Vault call is logged with:

```
2026-09-14 10:15:02.123 +02:00 [INF] VaultInjector.Core.Services.VaultService: Vault call GET secrets/secret/myapp/prod/db (KV v2) completed in 84ms with status 200 fields=3
```

Failures are logged at Warning/Error with the exception and, where available, the HTTP status code
Vault returned. The log level is configurable from the General tab and applies immediately; the
retained-days setting applies after a restart.

## Known limitations

- One global hotkey (menu picker); it must not collide with another application's global hotkey or
  registration will fail (the app warns via a tray balloon and logs it).
- Login only supports pasting a token you obtained yourself - there's no interactive OIDC/browser
  flow built in, by design (use `vault login` or your usual SSO tooling, then paste the resulting
  token here).

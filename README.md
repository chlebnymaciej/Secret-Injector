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

> Note: `VaultInjector.App.csproj` sets `EnableWindowsTargeting=true` so the solution can also be
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

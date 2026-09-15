; Vault Injector installer script (Inno Setup 6, https://jrsoftware.org/isinfo.php - not bundled with
; this repo). Produces a single VaultInjectorSetup.exe: no admin rights needed to install or run it,
; matching the app itself (asInvoker, never elevates).
;
; 1. Publish first (self-contained, single-file - see FolderProfile.pubxml):
;      dotnet publish ..\src\VaultInjector.App\VaultInjector.App.csproj -p:PublishProfile=FolderProfile -c Release
; 2. Compile this script:
;      iscc VaultInjector.iss
;    (or open it in the Inno Setup IDE and press Compile/F9)
;
; The installer lands in installer\Output\VaultInjectorSetup.exe.
;
; AppId must NEVER change across releases - Inno Setup uses it (not AppName) to recognize "this is an
; upgrade of the same product" instead of installing a second, separate copy side by side. Bump
; AppVersion before every release so the installed version number / upgrade detection stays meaningful.

#define AppName "Vault Injector"
#define AppVersion "1.0.0"
#define AppPublisher "Vault Injector"
#define AppExeName "VaultInjector.exe"
#define PublishDir "..\src\VaultInjector.App\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{6C6E2B6E-6E9C-4B4E-9C5B-6E2B6C1D6A2E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\Vault Injector
DefaultGroupName=Vault Injector
DisableProgramGroupPage=yes
; No admin prompt for install/uninstall - the app itself never elevates either (see app.manifest).
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputBaseFilename=VaultInjectorSetup
OutputDir=Output
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; Only the single published exe - .pdb debug symbols next to it in the publish folder are intentionally
; not shipped. (Wildcard-including that folder would also pick those up.)
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Vault Injector"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall Vault Injector"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Vault Injector"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch Vault Injector"; Flags: postinstall skipifsilent nowait

[UninstallRun]
; Best-effort: the app is a tray-only background process with no window to close gracefully, so make
; sure it isn't still running (and locking its own exe) when uninstall tries to delete it.
Filename: "{cmd}"; Parameters: "/C taskkill /IM ""{#AppExeName}"" /F"; Flags: runhidden skipifdoesntexist waituntilterminated

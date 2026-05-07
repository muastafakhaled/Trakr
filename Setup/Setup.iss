; ─────────────────────────────────────────────────────────────────────────────
;  Trakr — Inno Setup 6 Installer
;
;  HOW TO BUILD
;  ────────────
;  1. Publish the app (framework-dependent, tiny output):
;       dotnet publish -c Release -r win-x64 --no-self-contained -o publish
;
;  2. Install Inno Setup 6 (free): https://jrsoftware.org/isinfo.php
;
;  3. Right-click this file → "Compile"  — or open Inno Setup IDE and press F9
;
;  Output: Setup\Output\Trakr_Setup_v1.0.0.exe  (~2 MB)
; ─────────────────────────────────────────────────────────────────────────────

#define AppName      "Trakr"
#define AppVersion   "1.0.0"
#define AppPublisher "Mustafa Khaled"
#define AppExe       "Trakr.exe"
#define PublishDir   "..\publish"

; .NET 8 Desktop Runtime x64 — always points to latest 8.x
#define DotNetUrl "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"

[Setup]
AppId={{F3A8B2C4-7D1E-4F5A-9B3C-2E6D8A0F1B4E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Trakr
DefaultGroupName={#AppName}
OutputDir=Output
OutputBaseFilename=Trakr_Setup_v{#AppVersion}
SetupIconFile=..\Resources\tray.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
; No admin needed — installs to per-user Program Files if no elevation
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
CloseApplicationsFilter=*Trakr*

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
; Framework-dependent publish — 3 files, ~300 KB total, no runtime bundled
Source: "{#PublishDir}\Trakr.exe";                DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\Trakr.dll";                DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\Trakr.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; \
  Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "reg.exe"; \
  Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""Trakr"" /f"; \
  Flags: runhidden; RunOnceId: "RemoveStartup"

; ─────────────────────────────────────────────────────────────────────────────
[Code]

// ── Kill running instance before install ──────────────────────────────────────
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM Trakr.exe', '', SW_HIDE,
       ewWaitUntilTerminated, ResultCode);
  Result := True;
end;

// ── Check if .NET 8 Desktop Runtime x64 is installed ─────────────────────────
function IsDotNet8Installed(): Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  // .NET installs to C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.x.x
  if FindFirst(ExpandConstant('{pf}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'),
               FindRec) then
  begin
    Result := True;
    FindClose(FindRec);
  end;
end;

// ── Download and silently install .NET 8 Desktop Runtime ─────────────────────
procedure DownloadDotNet8();
var
  TempFile: String;
  ResultCode: Integer;
  PSCommand: String;
begin
  TempFile := ExpandConstant('{tmp}\dotnet8-desktop-runtime.exe');

  // Use PowerShell to download (available on all modern Windows)
  PSCommand := '-NoProfile -NonInteractive -Command ' +
    '"[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; ' +
    'Invoke-WebRequest -Uri ''' + '{#DotNetUrl}' + ''' -OutFile ''' + TempFile + '''"';

  WizardForm.StatusLabel.Caption := 'Downloading .NET 8 Desktop Runtime…';

  if not Exec('powershell.exe', PSCommand, '', SW_HIDE,
              ewWaitUntilTerminated, ResultCode) then
  begin
    MsgBox('Could not download .NET 8 Desktop Runtime.' + #13#10 +
           'Please install it manually from:' + #13#10 +
           'https://dotnet.microsoft.com/en-us/download/dotnet/8.0',
           mbError, MB_OK);
    Exit;
  end;

  if ResultCode <> 0 then
  begin
    MsgBox('Download failed (error ' + IntToStr(ResultCode) + ').' + #13#10 +
           'Please install .NET 8 Desktop Runtime manually from:' + #13#10 +
           'https://dotnet.microsoft.com/en-us/download/dotnet/8.0',
           mbError, MB_OK);
    Exit;
  end;

  WizardForm.StatusLabel.Caption := 'Installing .NET 8 Desktop Runtime…';
  Exec(TempFile, '/install /quiet /norestart', '', SW_SHOW,
       ewWaitUntilTerminated, ResultCode);
end;

// ── Hook: check runtime before copying files ──────────────────────────────────
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not IsDotNet8Installed() then
  begin
    if MsgBox('.NET 8 Desktop Runtime is required but not installed.' + #13#10 + #13#10 +
              'Click OK to download and install it automatically (~55 MB).' + #13#10 +
              'Click Cancel to install it manually later.',
              mbConfirmation, MB_OKCANCEL) = IDOK then
      DownloadDotNet8()
    else
      MsgBox('The app is installed but will not run until .NET 8 Desktop Runtime is installed.' + #13#10 +
             'Download it from: https://dotnet.microsoft.com/en-us/download/dotnet/8.0',
             mbInformation, MB_OK);
  end;
end;

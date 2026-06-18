; ============================================================
;  Sqwizzey Switch — Inno Setup script
;
;  Build steps:
;    1. dotnet publish -c Release -r win-x64 --self-contained true
;         -p:PublishSingleFile=true -o publish\
;    2. Open this file in Inno Setup Compiler (iscc.exe) and click Build.
;    3. Installer lands in installer\ directory.
;
;  Inno Setup download: https://jrsoftware.org/isdl.php
; ============================================================

#define AppName     "SqwizzeySwitch"
#define AppVersion  "1.1.0"
#define AppPublisher "Sqwizzey Switch"
#define AppExe      "SqwizzeySwitch.exe"

[Setup]
AppId={{F3A1C2D4-8B5E-4F9A-A3D7-1C2E4B6F8A0D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppVerName={#AppName} {#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=installer
OutputBaseFilename=SqwizzeySwitch-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupIconFile=assets\icon.ico
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExe}
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
; Launch after install — nowait so installer closes immediately
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName} now"; \
  Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kill running instance before uninstall
Filename: "taskkill.exe"; Parameters: "/F /IM {#AppExe}"; Flags: runhidden; \
  RunOnceId: "KillSqwizzeySwitch"

[Registry]
; Remove autorun entry on uninstall (user may have set it via the tray menu)
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueName: "{#AppName}"; Flags: deletevalue uninsdeletevalue

[Code]
// Kill running instance before upgrading
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
    Exec('taskkill.exe', '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

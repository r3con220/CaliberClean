; ═══════════════════════════════════════════════════════
;  CaliberClean v0.7.0 — Inno Setup Installer Script
;  Publisher: Caliber Media LLC
;  Generated for: net8.0-windows, self-contained win-x64
; ═══════════════════════════════════════════════════════

#define AppName      "CaliberClean"
#define AppVersion   "0.7.0"
#define AppPublisher "Caliber Media LLC"
#define AppExeName   "CaliberClean.exe"
#define AppId        "{{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
#define SourceDir    "..\publish\win-x64"
#define IconFile     "..\CaliberClean.ico"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://calibervoice.com
AppSupportURL=https://calibervoice.com
AppUpdatesURL=https://calibervoice.com

; Install location
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; Output
OutputDir=Output
OutputBaseFilename=CaliberCleanSetup
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#AppExeName}

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Appearance
WizardStyle=modern
WizardSizePercent=120

; Privileges — install to Program Files, requires admin
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Architecture
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Versioning info shown in Apps & Features
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} — System Cleanup Utility
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
; All self-contained publish output
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

; Desktop (optional — shown only if task selected)
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove any local app data written at runtime (optional — comment out to preserve user data)
; Type: filesandordirs; Name: "{localappdata}\CaliberClean"

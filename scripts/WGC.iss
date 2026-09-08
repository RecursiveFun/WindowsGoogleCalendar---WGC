; Inno Setup script for WGC
; Compile with Inno Setup after running scripts\publish.ps1
; Optional signing: set SignTool in Inno or sign the installer afterward.

#define AppName "WGC"
#define AppVersion "1.0.0"
#define AppPublisher "WGC"
#define AppExeName "WGC.exe"
#define PublishDir "..\artifacts\WGC-1.0.0-win-x64"

[Setup]
AppId={{8F3C2B1A-9D4E-4F6A-B7C8-1E2D3C4B5A6F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename=WGCSetup-{#AppVersion}
SetupIconFile=..\CalendarDesktop\Assets\app.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

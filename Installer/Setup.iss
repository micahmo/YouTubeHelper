#define MyAppName "YouTubeHelper"
#define MyAppExeName "YouTubeHelper.exe"
#define MyAppVersion GetEnv("VERSION")
#define MyAppPublisher "Micah Morrison"
#define MyAppURL "https://github.com/micahmo/youtubehelper"
; This is relative to SourceDir
#define RepoRoot "..\..\..\..\"
#define Configuration GetEnv("CONFIGURATION")

[Setup]
AppId={{744FA957-AB5E-455A-8CEC-A29448D1FB93}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
; Start menu folder
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; This is relative to the .iss file location
SourceDir=..\YouTubeHelper\bin\{#Configuration}\net8.0-windows
OutputDir={#RepoRoot}\Installer
SetupIconFile={#RepoRoot}\YouTubeHelper\Images\logo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputBaseFilename={#MyAppName}Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "*"; DestDir: "{app}"; Flags: recursesubdirs

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch YouTubeHelper"; Flags: nowait postinstall skipifsilent

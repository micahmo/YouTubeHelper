#define MyAppName "YouTubeHelper"
#define MyAppExeName "YouTubeHelper.exe"
#define MyAppVersion GetEnv("VERSION")
#define MyAppPublisher "Micah Morrison"
#define MyAppURL "https://github.com/micahmo/youtubehelper"
; This is relative to SourceDir
#define RepoRoot "..\..\..\..\"
#define Configuration GetEnv("CONFIGURATION")
#define NetRuntimeMinorVersion "11"
#define NetRuntimeVersion "8.0." + NetRuntimeMinorVersion
#define NetRuntime "windowsdesktop-runtime-" + NetRuntimeVersion + "-win-x64.exe"

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
; .NET Desktop Runtime install can trigger this, but it doesn't actually require a restart
RestartIfNeededByRun=no
; Let the user manually restart apps
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
Source: "..\..\..\..\Installer\{#NetRuntime}"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NetRuntimeNotInstalled

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; .NET Desktop Runtime
Filename: "{tmp}\{#NetRuntime}"; Flags: runascurrentuser; StatusMsg: "Installing .NET Desktop Runtime..."; Check: NetRuntimeNotInstalled
Filename: "{app}\{#MyAppExeName}"; Description: "Launch YouTubeHelper"; Flags: nowait postinstall skipifsilent

[Code]
function NetRuntimeNotInstalled: Boolean;
var
  InstalledRuntimes: TArrayOfString;
  I: Integer;
  MinorVersion: String;
  MinorVersionInt: Longint;
begin
  Result := True;
  
  // Check if ANY .NET Desktop Runtime exists
  if RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App') then
  begin
    // Get all of the installed runtimes
    if RegGetValueNames(HKEY_LOCAL_MACHINE, 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.NETCore.App', InstalledRuntimes) then
    begin
      for I := 0 to GetArrayLength(InstalledRuntimes)-1 do
      begin 
        // See if the runtime starts with 8.0.
        if WildcardMatch(InstalledRuntimes[I], '8.0.*') then
        begin
          // Get just the minor version and convert it to an int
          MinorVersion := InstalledRuntimes[I];
          Delete(MinorVersion, 1, 4);
          MinorVersionInt := StrToIntDef(MinorVersion, 0);
          
          // Check if it's at least the version we want
          if MinorVersionInt >= {#NetRuntimeMinorVersion} then
          begin
            // Finally, this system has a new enough version installed
            Result := False;
            Break;
          end
        end
      end
    end
  end
end;

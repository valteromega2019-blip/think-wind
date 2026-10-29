; Compile only after publishing the independent project; see README.
#define MyAppVersion "0.8.0-alpha"
[Setup]
AppId={{05EC91A4-D290-4AB9-B291-988D4C61CA6A}
AppName=Think Wind
AppVersion={#MyAppVersion}
VersionInfoVersion=0.8.0.0
DefaultDirName={localappdata}\Programs\ThinkWind
PrivilegesRequired=lowest
DefaultGroupName=Think Wind
OutputDir=..\artifacts\installer
OutputBaseFilename=ThinkWind-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\ThinkWind\Assets\ThinkWind.ico
UninstallDisplayIcon={app}\ThinkWind.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Think Wind"; Filename: "{app}\ThinkWind.exe"
[Run]
Filename: "{app}\ThinkWind.exe"; Description: "Abrir Think Wind"; Flags: nowait postinstall skipifsilent
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "ThinkWind"; Flags: uninsdeletevalue

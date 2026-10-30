; Compile only after publishing the independent project; see README.
#define MyAppVersion "0.15.1-alpha"
#ifndef PublishDir
#define PublishDir "..\artifacts\publish-v0.15.1-alpha"
#endif
[Setup]
AppId={{05EC91A4-D290-4AB9-B291-988D4C61CA6A}
AppName=Think Wind
AppVersion={#MyAppVersion}
VersionInfoVersion=0.15.1.0
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
RestartApplications=no
[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Think Wind"; Filename: "{app}\ThinkWind.exe"
[Run]
Filename: "{app}\ThinkWind.exe"; Description: "Abrir Think Wind"; Flags: nowait postinstall skipifsilent
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "ThinkWind"; Flags: uninsdeletevalue

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if not Exec(ExpandConstant('{app}\ThinkWind.exe'), '--configure-installation',
      ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      Log('Unable to run automatic taskbar configuration.');
      SuppressibleMsgBox('Nao foi possivel configurar as barras automaticamente. Abra Personalizar > Sistema no Think Wind e ative Usar somente a barra do Think Wind. No Windows, ative Ocultar automaticamente a barra de tarefas.', mbError, MB_OK, IDOK);
    end
    else if ResultCode <> 0 then
    begin
      Log(Format('Automatic taskbar configuration failed, code %d.', [ResultCode]));
      SuppressibleMsgBox('A instalacao foi concluida, mas a configuracao automatica das barras falhou. Feche o Think Wind e tente novamente, ou configure as duas opcoes manualmente. Detalhes em %LOCALAPPDATA%\ThinkWind\installation-setup.log, quando disponivel.', mbError, MB_OK, IDOK);
    end;
  end;
end;

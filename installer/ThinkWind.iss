; Compile only after publishing the independent project; see README.
#define MyAppVersion "0.18.3-alpha"
#ifndef PublishDir
#define PublishDir "..\artifacts\publish-v0.18.3-alpha"
#endif
[Setup]
AppId={{05EC91A4-D290-4AB9-B291-988D4C61CA6A}
AppName=Think Wind
AppVersion={#MyAppVersion}
VersionInfoVersion=0.18.3.0
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
[Tasks]
Name: "pawnio"; Description: "Instalar PawnIO oficial para sensores, se necessario (requer administrador)"; Flags: checkedonce
[Files]
Source: "..\artifacts\dependencies\PawnIO_setup.exe"; DestDir: "{tmp}"; Flags: dontcopy
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Think Wind"; Filename: "{app}\ThinkWind.exe"
[Run]
Filename: "{app}\ThinkWind.exe"; Description: "Abrir Think Wind"; Flags: nowait postinstall skipifsilent
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "ThinkWind"; Flags: uninsdeletevalue

[Code]
var
  DriverRestartRequired: Boolean;

function PawnIOCompatible: Boolean;
var
  Location, Version: String;
  PackedVersion: Int64;
begin
  Result := False;
  if not RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO', 'InstallLocation', Location) then Exit;
  if not RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO', 'DisplayVersion', Version) then Exit;
  if not StrToVersion(Version, PackedVersion) then Exit;
  Result := (ComparePackedVersion(PackedVersion, PackVersionComponents(2, 2, 0, 0)) >= 0)
    and FileExists(AddBackslash(Location) + 'PawnIOLib.dll')
    and RegKeyExists(HKLM64, 'SYSTEM\CurrentControlSet\Services\PawnIO');
end;

procedure InstallPawnIO;
var
  Installer: String;
  ExitCode: Integer;
begin
  if not WizardIsTaskSelected('pawnio') or PawnIOCompatible then Exit;
  ExtractTemporaryFile('PawnIO_setup.exe');
  Installer := ExpandConstant('{tmp}\PawnIO_setup.exe');
  if CompareText(GetSHA256OfFile(Installer), '1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032') <> 0 then
    RaiseException('O pacote PawnIO nao corresponde ao instalador oficial esperado.');
  if not ShellExec('runas', Installer, '-install -silent', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    SuppressibleMsgBox('PawnIO nao foi instalado. Os sensores basicos continuam disponiveis. Para sensores avancados, instale o driver oficial e autorize o coletor em Personalizar.', mbInformation, MB_OK, IDOK)
  else if ExitCode = 3010 then DriverRestartRequired := True
  else if ExitCode <> 0 then
    SuppressibleMsgBox('O instalador PawnIO retornou codigo ' + IntToStr(ExitCode) + '. Os sensores avancados podem ficar indisponiveis.', mbInformation, MB_OK, IDOK);
end;

function NeedRestart: Boolean;
begin
  Result := DriverRestartRequired;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    InstallPawnIO;
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

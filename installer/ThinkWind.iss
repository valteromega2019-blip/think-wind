; Compile only after publishing the independent project; see README.
#define MyAppVersion "0.23.0-beta"
#ifndef PublishDir
#define PublishDir "..\artifacts\publish-v0.23.0-beta"
#endif
[Setup]
AppId={{B7408A21-58E2-4E3A-A842-84EF85965350}
AppName=Orby Launcher
AppPublisher=Think
VersionInfoCompany=Think
VersionInfoDescription=Instalador do Orby Launcher
AppVersion={#MyAppVersion}
VersionInfoVersion=0.23.0.0
DefaultDirName={localappdata}\Programs\OrbyLauncher
PrivilegesRequired=lowest
DefaultGroupName=Orby Launcher
UsePreviousGroup=no
OutputDir=..\artifacts\installer
OutputBaseFilename=OrbyLauncher-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\ThinkWind\Assets\OrbyLauncher.ico
WizardSmallImageFile=..\src\ThinkWind\Assets\OrbyLauncher.png
UninstallDisplayIcon={app}\OrbyLauncher.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
[Tasks]
Name: "pawnio"; Description: "Instalar PawnIO oficial para sensores, se necessario (requer administrador)"; Flags: checkedonce
[Files]
Source: "..\artifacts\dependencies\PawnIO_setup.exe"; DestDir: "{tmp}"; Flags: dontcopy
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\ThinkWind\Assets\OrbyLauncher.ico"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\Orby Launcher"; Filename: "{app}\OrbyLauncher.exe"; IconFilename: "{app}\OrbyLauncher.ico"; AppUserModelID: "Think.OrbyLauncher"
[Run]
Filename: "{app}\OrbyLauncher.exe"; Description: "Abrir Orby Launcher"; Flags: nowait postinstall skipifsilent
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "OrbyLauncher"; Flags: uninsdeletevalue

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
    { Integrated updates preserve the user's startup/taskbar choices. }
    if ExpandConstant('{param:ORBYUPDATE|0}') = '1' then Exit;
    if not Exec(ExpandConstant('{app}\OrbyLauncher.exe'), '--configure-installation',
      ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      Log('Unable to run automatic taskbar configuration.');
      SuppressibleMsgBox('Nao foi possivel configurar as barras automaticamente. Abra Personalizar > Sistema no Orby Launcher e ative Usar somente a barra do Orby Launcher. No Windows, ative Ocultar automaticamente a barra de tarefas.', mbError, MB_OK, IDOK);
    end
    else if ResultCode <> 0 then
    begin
      Log(Format('Automatic taskbar configuration failed, code %d.', [ResultCode]));
      SuppressibleMsgBox('A instalacao foi concluida, mas a configuracao automatica das barras falhou. Feche o Orby Launcher e tente novamente, ou configure as duas opcoes manualmente. Detalhes em %LOCALAPPDATA%\OrbyLauncher\installation-setup.log, quando disponivel.', mbError, MB_OK, IDOK);
    end;
  end;
end;

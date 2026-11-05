; Compile only after publishing the independent project; see README.
#define MyAppVersion "0.29.1-beta"
#ifndef PublishDir
#define PublishDir "..\artifacts\publish-v0.29.1-beta"
#endif
[Setup]
AppId={{B7408A21-58E2-4E3A-A842-84EF85965350}
AppName=Orby Launcher
AppPublisher=Think
VersionInfoCompany=Think
VersionInfoDescription=Instalador do Orby Launcher
AppVersion={#MyAppVersion}
VersionInfoVersion=0.29.1.0
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
ShowLanguageDialog=yes
LanguageDetectionMethod=uilanguage
DisableWelcomePage=no
#include "OrbyLanguages.iss"
[Tasks]
Name: "pawnio"; Description: "{cm:PawnTask}"; Flags: checkedonce
[Files]
Source: "..\artifacts\dependencies\PawnIO_setup.exe"; DestDir: "{tmp}"; Flags: dontcopy
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\ThinkWind\Assets\OrbyLauncher.ico"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\Orby Launcher"; Filename: "{app}\OrbyLauncher.exe"; IconFilename: "{app}\OrbyLauncher.ico"; AppUserModelID: "Think.OrbyLauncher"
[Run]
Filename: "{app}\OrbyLauncher.exe"; Description: "{cm:LaunchProgram,Orby Launcher}"; Flags: nowait postinstall skipifsilent
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "OrbyLauncher"; Flags: uninsdeletevalue

[UninstallRun]
Filename: "{app}\OrbyLauncher.exe"; Parameters: "--remove-startup"; Flags: runhidden skipifdoesntexist waituntilterminated; RunOnceId: "RemoveOrbyStartup"

[Code]
var
  DriverRestartRequired: Boolean;

procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption := WizardForm.WelcomeLabel2.Caption + #13#10#13#10 + CustomMessage('LauncherLanguageNote');
  if ExpandConstant('{param:ORBYUPDATE|0}') = '1' then
  begin
    WizardForm.Caption := CustomMessage('UpdateTitle');
    WizardForm.PageNameLabel.Caption := CustomMessage('UpdateTitle');
    WizardForm.PageDescriptionLabel.Caption := CustomMessage('UpdateDescription');
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpInstalling) and (ExpandConstant('{param:ORBYUPDATE|0}') = '1') then
  begin
    WizardForm.PageNameLabel.Caption := CustomMessage('UpdateTitle');
    WizardForm.PageDescriptionLabel.Caption := CustomMessage('UpdateDescription');
  end;
end;

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
    RaiseException(CustomMessage('PawnHashFailure'));
  if not ShellExec('runas', Installer, '-install -silent', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    SuppressibleMsgBox(CustomMessage('PawnInstallFailed'), mbInformation, MB_OK, IDOK)
  else if ExitCode = 3010 then DriverRestartRequired := True
  else if ExitCode <> 0 then
    SuppressibleMsgBox(ExpandConstant('{cm:PawnExitCode,' + IntToStr(ExitCode) + '}'), mbInformation, MB_OK, IDOK);
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
    if ExpandConstant('{param:ORBYUPDATE|0}') = '1' then
      WizardForm.StatusLabel.Caption := CustomMessage('UpdateFinishing');
    InstallPawnIO;
    { Integrated updates preserve the user's startup/taskbar choices. }
    if ExpandConstant('{param:ORBYUPDATE|0}') = '1' then Exit;
    if not Exec(ExpandConstant('{app}\OrbyLauncher.exe'), '--configure-installation',
      ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      Log('Unable to run automatic taskbar configuration.');
      SuppressibleMsgBox(CustomMessage('TaskbarSetupFailed'), mbError, MB_OK, IDOK);
    end
    else if ResultCode <> 0 then
    begin
      Log(Format('Automatic taskbar configuration failed, code %d.', [ResultCode]));
      SuppressibleMsgBox(ExpandConstant('{cm:TaskbarSetupExitFailed}'), mbError, MB_OK, IDOK);
    end;
  end;
end;


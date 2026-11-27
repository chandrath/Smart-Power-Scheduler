; Single Source of Truth (SSOT) Inno Setup Script
; Values default to dev config if not passed via command line /D parameters.

#ifndef MyAppName
  #define MyAppName "Smart Power Scheduler"
#endif
#ifndef MyAppExeName
  #define MyAppExeName "SmartPowerScheduler.exe"
#endif
#ifndef MyAppVersion
  ; Fallback if compiled outside build.ps1; build.ps1 passes /DMyAppVersion from DevConfig.json (SSOT)
  #define MyAppVersion "0.0.0.0"
#endif
#ifndef MyPublisher
  #define MyPublisher "Appsphinx"
#endif
#ifndef MyAppURL
  #define MyAppURL "https://github.com/chandrath/Smart-Power-Scheduler"
#endif
#ifndef MyAppId
  #define MyAppId "9E2F9F24-B1F6-4A0B-88EE-C2A8389DB67B"
#endif
#ifndef SourceDir
  #define SourceDir "..\bin\Publish\SelfContained"
#endif
#ifndef StreamDeckPluginDir
  #define StreamDeckPluginDir "..\StreamDeck.Plugin\com.appsphinx.smartpowerscheduler.sdPlugin"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\..\artifacts"
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename "SmartPowerScheduler-Setup"
#endif
#ifndef AppArch
  #define AppArch "x64"
#endif

[Setup]
AppId={{{#MyAppId}}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyPublisher}\{#MyAppName}
UsePreviousAppDir=no
DisableProgramGroupPage=yes
OutputBaseFilename={#OutputBaseFilename}
OutputDir={#OutputDir}
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode={#AppArch}
ArchitecturesAllowed={#AppArch}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
CloseApplicationsFilter=SmartPowerScheduler.exe,SmartPowerScheduler.StreamDeck.exe
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "compact"; Description: "Compact installation (Application only)"
Name: "full"; Description: "Full installation (Includes Stream Deck integration)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "main"; Description: "Power Timer Application (Required)"; Types: full compact custom; Flags: fixed
Name: "streamdeck"; Description: "Elgato Stream Deck Integration (Optional)"; Types: full; Flags: disablenouninstallwarning

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Components: main; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StreamDeckPluginDir}\*"; DestDir: "{userappdata}\Elgato\StreamDeck\Plugins\com.appsphinx.smartpowerscheduler.sdPlugin"; Components: streamdeck; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\Elgato\StreamDeck\Plugins\com.appsphinx.smartpowerscheduler.sdPlugin"

[Code]
function IsProcessRunning(const ExeName: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('cmd.exe', '/c tasklist /NH /FI "IMAGENAME eq ' + ExeName + '" | findstr /I "' + ExeName + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function IsStreamDeckInstalled(): Boolean;
begin
  Result := FileExists(ExpandConstant('{commonpf64}\Elgato\StreamDeck\StreamDeck.exe')) or
            FileExists(ExpandConstant('{commonpf}\Elgato\StreamDeck\StreamDeck.exe')) or
            FileExists(ExpandConstant('{autopf}\Elgato\StreamDeck\StreamDeck.exe')) or
            FileExists(ExpandConstant('{commonpf32}\Elgato\StreamDeck\StreamDeck.exe')) or
            FileExists(ExpandConstant('{localappdata}\Programs\Elgato\StreamDeck\StreamDeck.exe')) or
            RegKeyExists(HKCU, 'Software\Elgato Systems GmbH\StreamDeck') or
            RegKeyExists(HKLM, 'Software\Elgato Systems GmbH\StreamDeck') or
            RegKeyExists(HKLM64, 'Software\Elgato Systems GmbH\StreamDeck') or
            IsProcessRunning('StreamDeck.exe');
end;

var
  StreamDeckAutoConfigured: Boolean;

procedure TaskKillStreamDeckPlugin();
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM SmartPowerScheduler.StreamDeck.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CloseStreamDeckApp();
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM StreamDeck.exe /IM SmartPowerScheduler.StreamDeck.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1000);
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpSelectComponents) and (not StreamDeckAutoConfigured) then
  begin
    StreamDeckAutoConfigured := True;
    if IsStreamDeckInstalled() then
    begin
      WizardForm.TypesCombo.ItemIndex := 1;
      WizardSelectComponents('main,streamdeck');
    end
    else
    begin
      WizardForm.TypesCombo.ItemIndex := 0;
      WizardSelectComponents('main');
    end;
  end;
end;

procedure InitializeWizard();
begin
  if IsStreamDeckInstalled() then
  begin
    WizardSelectComponents('main,streamdeck');
    WizardForm.TypesCombo.ItemIndex := 1;
  end
  else
  begin
    WizardSelectComponents('main');
    WizardForm.TypesCombo.ItemIndex := 0;
  end;
end;

function GetInstalledVersion(var VersionStr: String): Boolean;
var
  UninstallSubKey: String;
begin
  UninstallSubKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}_is1';
  Result := RegQueryStringValue(HKCU, UninstallSubKey, 'DisplayVersion', VersionStr) or
            RegQueryStringValue(HKLM, UninstallSubKey, 'DisplayVersion', VersionStr);
end;

function IsExistingInstallOrConfig(): Boolean;
var
  AppDataDir: String;
  DummyVer: String;
begin
  AppDataDir := ExpandConstant('{userappdata}\{#MyPublisher}\SmartPowerScheduler');
  Result := DirExists(AppDataDir) or GetInstalledVersion(DummyVer);
end;

var
  RemoveUserConfig: Boolean;

function PromptOptInDialog(const DialogTitle, PromptText, CheckCaption: String; var IsChecked: Boolean): Boolean;
var
  Form: TSetupForm;
  InfoLabel: TNewStaticText;
  OptCheckBox: TNewCheckBox;
  OkButton, CancelButton: TNewButton;
begin
  Result := True;
  IsChecked := False;

  Form := CreateCustomForm();
  try
    Form.ClientWidth := ScaleX(380);
    Form.ClientHeight := ScaleY(140);
    Form.Caption := DialogTitle;
    Form.Position := poScreenCenter;

    InfoLabel := TNewStaticText.Create(Form);
    InfoLabel.Parent := Form;
    InfoLabel.Left := ScaleX(16);
    InfoLabel.Top := ScaleY(16);
    InfoLabel.Width := ScaleX(348);
    InfoLabel.AutoSize := False;
    InfoLabel.WordWrap := True;
    InfoLabel.Height := ScaleY(36);
    InfoLabel.Caption := PromptText;

    OptCheckBox := TNewCheckBox.Create(Form);
    OptCheckBox.Parent := Form;
    OptCheckBox.Left := ScaleX(16);
    OptCheckBox.Top := ScaleY(60);
    OptCheckBox.Width := ScaleX(348);
    OptCheckBox.Caption := CheckCaption;
    OptCheckBox.Checked := False;

    OkButton := TNewButton.Create(Form);
    OkButton.Parent := Form;
    OkButton.Left := ScaleX(204);
    OkButton.Top := ScaleY(100);
    OkButton.Width := ScaleX(75);
    OkButton.Height := ScaleY(24);
    OkButton.Caption := 'OK';
    OkButton.ModalResult := mrOk;
    OkButton.Default := True;

    CancelButton := TNewButton.Create(Form);
    CancelButton.Parent := Form;
    CancelButton.Left := ScaleX(289);
    CancelButton.Top := ScaleY(100);
    CancelButton.Width := ScaleX(75);
    CancelButton.Height := ScaleY(24);
    CancelButton.Caption := 'Cancel';
    CancelButton.ModalResult := mrCancel;
    CancelButton.Cancel := True;

    if Form.ShowModal() = mrOk then
      IsChecked := OptCheckBox.Checked
    else
      Result := False;
  finally
    Form.Free();
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  AppDataDir: String;
  InstalledVerStr: String;
  InstalledVer, NewVer: Int64;
  IsUpgrade, ShouldReset: Boolean;
  ResultCode: Integer;
begin
  Result := '';
  TaskKillStreamDeckPlugin();

  if WizardIsComponentSelected('streamdeck') and IsProcessRunning('StreamDeck.exe') then
  begin
    if WizardSilent() or (MsgBox('Elgato Stream Deck is currently running. It must be closed to install / update the plugin files. Would you like Setup to close Stream Deck automatically?', mbConfirmation, MB_YESNO) = IDYES) then
    begin
      CloseStreamDeckApp();
    end
    else
    begin
      Result := 'Installation cannot continue while Elgato Stream Deck is running. Please close Stream Deck and run Setup again.';
      Exit;
    end;
  end;

  IsUpgrade := False;
  if GetInstalledVersion(InstalledVerStr) and StrToVersion(InstalledVerStr, InstalledVer) and StrToVersion('{#MyAppVersion}', NewVer) then
  begin
    if ComparePackedVersion(InstalledVer, NewVer) < 0 then
      IsUpgrade := True;
  end;

  if (not WizardSilent()) and (not IsUpgrade) and IsExistingInstallOrConfig() then
  begin
    ShouldReset := False;
    if not PromptOptInDialog('Existing Configuration', 'Existing {#MyAppName} configuration was detected. Choose whether to reset it:', 'Reset saved settings and history to defaults', ShouldReset) then
    begin
      Result := 'Installation cancelled by user.';
      Exit;
    end;
    if ShouldReset then
    begin
      Exec('taskkill.exe', '/F /IM SmartPowerScheduler.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Sleep(300);
      AppDataDir := ExpandConstant('{userappdata}\{#MyPublisher}\SmartPowerScheduler');
      if DirExists(AppDataDir) then
        DelTree(AppDataDir, True, True, True);
    end;
  end;
end;

function InitializeSetup(): Boolean;
var
  InstalledVerStr: String;
  InstalledVer, NewVer: Int64;
begin
  if GetInstalledVersion(InstalledVerStr) and StrToVersion(InstalledVerStr, InstalledVer) and StrToVersion('{#MyAppVersion}', NewVer) then
  begin
    if ComparePackedVersion(InstalledVer, NewVer) > 0 then
    begin
      if not WizardSilent() then
        MsgBox('A newer version (' + InstalledVerStr + ') of {#MyAppName} is already installed.' #13#10 'Installing an older version ({#MyAppVersion}) is not allowed.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;

  TaskKillStreamDeckPlugin();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  PluginDir: String;
  AppDataDir: String;
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Kill app processes only (never touch StreamDeck.exe here)
    Exec('taskkill.exe', '/F /IM SmartPowerScheduler.exe /IM SmartPowerScheduler.StreamDeck.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(500);
  end
  else if CurUninstallStep = usPostUninstall then
  begin
    PluginDir := ExpandConstant('{userappdata}\Elgato\StreamDeck\Plugins\com.appsphinx.smartpowerscheduler.sdPlugin');
    if DirExists(PluginDir) then
      DelTree(PluginDir, True, True, True);

    if RemoveUserConfig then
    begin
      AppDataDir := ExpandConstant('{userappdata}\{#MyPublisher}\SmartPowerScheduler');
      if DirExists(AppDataDir) then
        DelTree(AppDataDir, True, True, True);
    end;
  end;
end;

function InitializeUninstall(): Boolean;
var
  PluginDir: String;
  AppDataDir: String;
  ResultCode: Integer;
begin
  RemoveUserConfig := False;
  AppDataDir := ExpandConstant('{userappdata}\{#MyPublisher}\SmartPowerScheduler');
  if (not UninstallSilent()) and DirExists(AppDataDir) then
  begin
    if not PromptOptInDialog('Uninstall {#MyAppName}', 'Saved configuration and history were found for {#MyAppName}. Choose whether to remove them:', 'Remove saved configuration, settings, and history', RemoveUserConfig) then
    begin
      Result := False;
      Exit;
    end;
  end;

  PluginDir := ExpandConstant('{userappdata}\Elgato\StreamDeck\Plugins\com.appsphinx.smartpowerscheduler.sdPlugin');

  // Kill plugin subprocess silently first
  Exec('taskkill.exe', '/F /IM SmartPowerScheduler.StreamDeck.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // Prompt if Stream Deck itself is running and plugin dir exists
  if DirExists(PluginDir) and IsProcessRunning('StreamDeck.exe') then
  begin
    if MsgBox('Elgato Stream Deck is currently running. It must be closed to cleanly remove the Stream Deck plugin files. Would you like the uninstaller to close Stream Deck automatically?', mbConfirmation, MB_YESNO) = IDYES then
      Exec('taskkill.exe', '/F /IM StreamDeck.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
    else
    begin
      MsgBox('Uninstallation cannot cleanly proceed while Elgato Stream Deck is running. Please close Stream Deck and try again.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;

  // Prompt if the main app is running
  if IsProcessRunning('SmartPowerScheduler.exe') then
  begin
    if MsgBox('Smart Power Scheduler is currently running. It must be closed to complete uninstallation. Close it automatically?', mbConfirmation, MB_YESNO) = IDYES then
      Exec('taskkill.exe', '/F /IM SmartPowerScheduler.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
    else
    begin
      MsgBox('Uninstallation cannot proceed while Smart Power Scheduler is running. Please close it and try again.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;

  Sleep(500);
  Result := True;
end;

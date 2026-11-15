; Single Source of Truth (SSOT) Inno Setup Script
; Values default to dev config if not passed via command line /D parameters.

#ifndef MyAppName
  #define MyAppName "Smart Sleep Scheduler"
#endif
#ifndef MyAppExeName
  #define MyAppExeName "SmartSleepScheduler.exe"
#endif
#ifndef MyAppVersion
  #define MyAppVersion "0.9.0.2"
#endif
#ifndef MyPublisher
  #define MyPublisher "Appsphinx"
#endif
#ifndef MyAppURL
  #define MyAppURL "https://github.com/chandrath/Smart-Sleep-Scheduler"
#endif
#ifndef MyAppId
  #define MyAppId "9E2F9F24-B1F6-4A0B-88EE-C2A8389DB67B"
#endif
#ifndef SourceDir
  #define SourceDir "..\bin\Publish\SelfContained"
#endif
#ifndef StreamDeckPluginDir
  #define StreamDeckPluginDir "..\StreamDeck.Plugin\com.appsphinx.smartsleepscheduler.sdPlugin"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\..\artifacts"
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename "SmartSleepScheduler-Setup"
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
DisableProgramGroupPage=yes
OutputBaseFilename={#OutputBaseFilename}
OutputDir={#OutputDir}
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "compact"; Description: "Compact installation (Application only)"
Name: "full"; Description: "Full installation (Includes Stream Deck integration)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "main"; Description: "Sleep Timer Application (Required)"; Types: full compact custom; Flags: fixed
Name: "streamdeck"; Description: "Elgato Stream Deck Integration (Optional)"; Types: full; Flags: disablenouninstallwarning

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Components: main; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StreamDeckPluginDir}\*"; DestDir: "{userappdata}\Elgato\StreamDeck\Plugins\com.appsphinx.smartsleepscheduler.sdPlugin"; Components: streamdeck; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\Elgato\StreamDeck\Plugins\com.appsphinx.smartsleepscheduler.sdPlugin"

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
  Exec('taskkill.exe', '/F /IM SmartSleepScheduler.StreamDeck.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CloseStreamDeckApp();
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM StreamDeck.exe /IM SmartSleepScheduler.StreamDeck.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
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

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  TaskKillStreamDeckPlugin();

  if WizardIsComponentSelected('streamdeck') and IsProcessRunning('StreamDeck.exe') then
  begin
    if MsgBox('Elgato Stream Deck is currently running. It must be closed to install / update the plugin files. Would you like Setup to close Stream Deck automatically?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      CloseStreamDeckApp();
    end
    else
    begin
      Result := 'Installation cannot continue while Elgato Stream Deck is running. Please close Stream Deck and run Setup again.';
    end;
  end;
end;

function InitializeSetup(): Boolean;
begin
  TaskKillStreamDeckPlugin();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  PluginDir: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    TaskKillStreamDeckPlugin();
  end
  else if CurUninstallStep = usPostUninstall then
  begin
    PluginDir := ExpandConstant('{userappdata}\Elgato\StreamDeck\Plugins\com.appsphinx.smartsleepscheduler.sdPlugin');
    if DirExists(PluginDir) then
    begin
      DelTree(PluginDir, True, True, True);
    end;
  end;
end;

function InitializeUninstall(): Boolean;
var
  PluginDir: String;
begin
  PluginDir := ExpandConstant('{userappdata}\Elgato\StreamDeck\Plugins\com.appsphinx.smartsleepscheduler.sdPlugin');
  TaskKillStreamDeckPlugin();

  if DirExists(PluginDir) and IsProcessRunning('StreamDeck.exe') then
  begin
    if MsgBox('Elgato Stream Deck is currently running. It must be closed to cleanly remove the Stream Deck plugin files. Would you like the uninstaller to close Stream Deck automatically?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      CloseStreamDeckApp();
    end
    else
    begin
      MsgBox('Uninstallation cannot cleanly proceed while Elgato Stream Deck is running. Please close Stream Deck and try again.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;

  Result := True;
end;

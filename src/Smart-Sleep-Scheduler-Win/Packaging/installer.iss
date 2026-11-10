; Single Source of Truth (SSOT) Inno Setup Script
; Values default to dev config if not passed via command line /D parameters.

#ifndef MyAppName
  #define MyAppName "Smart Sleep Scheduler"
#endif
#ifndef MyAppExeName
  #define MyAppExeName "SmartSleepScheduler.exe"
#endif
#ifndef MyAppVersion
  #define MyAppVersion "0.9.0.1"
#endif
#ifndef MyPublisher
  #define MyPublisher "SmartSleep"
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
DefaultDirName={autopf}\{#MyAppName}
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

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

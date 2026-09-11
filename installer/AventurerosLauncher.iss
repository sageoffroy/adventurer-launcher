#define MyAppName "Aventureros de Azeroth Launcher"
#ifndef MyAppVersion
#define MyAppVersion "0.2.1"
#endif
#define MyAppPublisher "Aventureros de Azeroth"
#define MyAppExeName "AventurerosLauncher.exe"

[Setup]
AppId={{6B67778A-8D2C-4CC4-96A9-7069FEEC85C7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Aventureros de Azeroth
DefaultGroupName=Aventureros de Azeroth
OutputDir=output
OutputBaseFilename=AventurerosLauncherSetup
SetupIconFile=..\src\AdventurerLauncher\Assets\launcher.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Aventureros de Azeroth"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Aventureros de Azeroth"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir Aventureros de Azeroth"; Flags: nowait postinstall skipifsilent; Check: not WizardSilent
Filename: "{app}\{#MyAppExeName}"; Flags: nowait; Check: WizardSilent

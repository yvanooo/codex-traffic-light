#define AppName "Codex 红绿灯"
#define AppVersion "1.0.1"
#define AppPublisher "Gyk"
#define AppExeName "CodexTrafficLight.App.exe"
#define PublishDir "..\dist\CodexTrafficLight-installer-files"

[Setup]
AppId={{8B80A5D1-493C-4F63-9D40-9EA8C8793F4E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\CodexTrafficLight
AppendDefaultDirName=yes
DisableProgramGroupPage=yes
OutputDir=..\dist\installer
OutputBaseFilename=CodexTrafficLightSetup-{#AppVersion}
SetupIconFile=..\src\CodexTrafficLight.App\Assets\app-icon.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "运行 {#AppName}"; Flags: nowait postinstall skipifsilent

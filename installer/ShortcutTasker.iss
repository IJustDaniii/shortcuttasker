#define AppName "ShortcutTasker"
#define AppVersion "1.1.4"
#define SourceExe "..\build\ShortcutTasker.exe"

[Setup]
AppId={{B5DDA51D-CBD4-47F3-BE56-796ED7A810AF}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=IJustDaniii
AppPublisherURL=https://github.com/IJustDaniii/shortcuttasker
AppSupportURL=https://github.com/IJustDaniii/shortcuttasker/issues
AppUpdatesURL=https://github.com/IJustDaniii/shortcuttasker/releases
DefaultDirName={localappdata}\Programs\ShortcutTasker
DefaultGroupName=ShortcutTasker
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=ShortcutTasker-Setup
SetupIconFile=..\assets\icon.ico
UninstallDisplayIcon={app}\ShortcutTasker.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
AppMutex=Local\AtajosLibres.Instancia.v1
ArchitecturesAllowed=x86 x64compatible
VersionInfoVersion=1.1.4.0

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "ShortcutTasker.exe"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\ShortcutTasker"; Filename: "{app}\ShortcutTasker.exe"
Name: "{autodesktop}\ShortcutTasker"; Filename: "{app}\ShortcutTasker.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos adicionales:"; Flags: unchecked

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ShortcutTasker"; ValueData: """{app}\ShortcutTasker.exe"" --background"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "AtajosLibres"; Flags: deletevalue

[Run]
Filename: "{app}\ShortcutTasker.exe"; Description: "Abrir ShortcutTasker"; Flags: nowait postinstall skipifsilent

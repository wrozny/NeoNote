#ifndef PublishDirectory
  #define PublishDirectory "..\artifacts\win-x64"
#endif
#ifndef AppVersion
  #define AppVersion "0.3.1"
#endif

[Setup]
AppId={{97DC0D4D-786F-4AB3-B9C7-1F791D5CD0BE}
AppName=NeoNote
AppVersion={#AppVersion}
AppPublisher=wrozny
DefaultDirName={localappdata}\Programs\NeoNote
DefaultGroupName=NeoNote
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir=..\artifacts
OutputBaseFilename=NeoNote-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\MarkdownPreview\Assets\NeoNote.ico
UninstallDisplayIcon={app}\NeoNote.exe
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Files]
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\NeoNote"; Filename: "{app}\NeoNote.exe"

[Registry]
Root: HKCU; Subkey: "Software\Classes\Applications\NeoNote.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "NeoNote"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\NeoNote.exe\shell\open\command"; ValueType: string; ValueData: """{app}\NeoNote.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: none; ValueName: "NeoNote.Document"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\NeoNote.Document"; ValueType: string; ValueData: "Markdown document"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\NeoNote.Document\shell\open\command"; ValueType: string; ValueData: """{app}\NeoNote.exe"" ""%1"""

[Run]
Filename: "https://developer.microsoft.com/microsoft-edge/webview2/"; Description: "Install Microsoft Edge WebView2 Runtime (if missing)"; Flags: shellexec postinstall skipifsilent unchecked
Filename: "{app}\NeoNote.exe"; Description: "Launch NeoNote"; Flags: nowait postinstall skipifsilent

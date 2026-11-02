#ifndef PublishDirectory
  #define PublishDirectory "..\artifacts\win-x64"
#endif
#ifndef AppVersion
  #define AppVersion "0.2.0"
#endif

[Setup]
AppId={{97DC0D4D-786F-4AB3-B9C7-1F791D5CD0BE}
AppName=NeoPad
AppVersion={#AppVersion}
AppPublisher=wrozny
DefaultDirName={localappdata}\Programs\NeoPad
DefaultGroupName=NeoPad
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir=..\artifacts
OutputBaseFilename=NeoPad-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\MarkdownPreview\Assets\NeoPad.ico
UninstallDisplayIcon={app}\NeoPad.exe
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Files]
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\NeoPad"; Filename: "{app}\NeoPad.exe"

[Registry]
Root: HKCU; Subkey: "Software\Classes\Applications\NeoPad.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "NeoPad"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\NeoPad.exe\shell\open\command"; ValueType: string; ValueData: """{app}\NeoPad.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: none; ValueName: "NeoPad.Document"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\NeoPad.Document"; ValueType: string; ValueData: "Markdown document"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\NeoPad.Document\shell\open\command"; ValueType: string; ValueData: """{app}\NeoPad.exe"" ""%1"""

[Run]
Filename: "https://developer.microsoft.com/microsoft-edge/webview2/"; Description: "Install Microsoft Edge WebView2 Runtime (if missing)"; Flags: shellexec postinstall skipifsilent unchecked
Filename: "{app}\NeoPad.exe"; Description: "Launch NeoPad"; Flags: nowait postinstall skipifsilent

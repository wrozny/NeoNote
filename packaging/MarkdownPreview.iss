#ifndef PublishDirectory
  #define PublishDirectory "..\artifacts\win-x64"
#endif
#ifndef AppVersion
  #define AppVersion "0.1.1"
#endif

[Setup]
AppId={{97DC0D4D-786F-4AB3-B9C7-1F791D5CD0BE}
AppName=MarkdownPreview
AppVersion={#AppVersion}
AppPublisher=wrozny
DefaultDirName={localappdata}\Programs\MarkdownPreview
DefaultGroupName=MarkdownPreview
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir=..\artifacts
OutputBaseFilename=MarkdownPreview-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\MarkdownPreview.exe
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Files]
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\MarkdownPreview"; Filename: "{app}\MarkdownPreview.exe"

[Registry]
Root: HKCU; Subkey: "Software\Classes\Applications\MarkdownPreview.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "MarkdownPreview"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\MarkdownPreview.exe\shell\open\command"; ValueType: string; ValueData: """{app}\MarkdownPreview.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: none; ValueName: "MarkdownPreview.Document"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\MarkdownPreview.Document"; ValueType: string; ValueData: "Markdown document"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\MarkdownPreview.Document\shell\open\command"; ValueType: string; ValueData: """{app}\MarkdownPreview.exe"" ""%1"""

[Run]
Filename: "https://developer.microsoft.com/microsoft-edge/webview2/"; Description: "Install Microsoft Edge WebView2 Runtime (if missing)"; Flags: shellexec postinstall skipifsilent unchecked
Filename: "{app}\MarkdownPreview.exe"; Description: "Launch MarkdownPreview"; Flags: nowait postinstall skipifsilent

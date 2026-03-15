; PassKey Inno Setup Script
; Builds the Windows installer for PassKey Desktop + BrowserHost

[Setup]
AppName=PassKey
AppVersion=1.0.0
AppVerName=PassKey 1.0.0
AppPublisher=Giuseppe Imperato
AppPublisherURL=https://github.com/pexatar/PassKey
AppSupportURL=https://github.com/pexatar/PassKey/issues
AppUpdatesURL=https://github.com/pexatar/PassKey/releases
DefaultDirName={autopf}\PassKey
DefaultGroupName=PassKey
LicenseFile=..\LICENSE
SetupIconFile=..\src\PassKey.Desktop\Assets\PassKey.ico
OutputDir=Output
OutputBaseFilename=PassKey-Setup-x64
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\PassKey.Desktop.exe
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
; Published self-contained output (Desktop + BrowserHost)
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PassKey"; Filename: "{app}\PassKey.Desktop.exe"
Name: "{group}\{cm:UninstallProgram,PassKey}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\PassKey"; Filename: "{app}\PassKey.Desktop.exe"; Tasks: desktopicon

[Registry]
; passkey:// URL scheme handler
Root: HKCU; Subkey: "SOFTWARE\Classes\passkey"; ValueType: string; ValueData: "PassKey Protocol"; Flags: uninsdeletekey
Root: HKCU; Subkey: "SOFTWARE\Classes\passkey"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "SOFTWARE\Classes\passkey\shell\open\command"; ValueType: string; ValueData: """{app}\PassKey.Desktop.exe"" ""%1"""; Flags: uninsdeletekey

; Native Messaging Host for Chrome
Root: HKCU; Subkey: "SOFTWARE\Google\Chrome\NativeMessagingHosts\com.passkey.host"; ValueType: string; ValueData: "{app}\com.passkey.host.json"; Flags: uninsdeletevalue

; Native Messaging Host for Firefox
Root: HKCU; Subkey: "SOFTWARE\Mozilla\NativeMessagingHosts\com.passkey.host"; ValueType: string; ValueData: "{app}\com.passkey.host.json"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\PassKey.Desktop.exe"; Description: "Launch PassKey"; Flags: nowait postinstall skipifsilent

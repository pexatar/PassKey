; PassKey Inno Setup Script
; Builds the Windows installer for PassKey Desktop + BrowserHost

[Setup]
AppId={{A7F3C2D1-8E4B-4F9A-B6D5-3C1E7A2F0D84}
AppName=PassKey
AppVersion=1.0.9
AppVerName=PassKey 1.0.9
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
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.17763
PrivilegesRequired=lowest
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
; Windows App Runtime 1.8 redistributable — installed silently before the app launches.
; HVCI (Hypervisor-Protected Code Integrity) rejects DLLs that are not in the Windows
; Code Integrity system catalog. The official runtime installer registers trusted, cataloged
; DLLs; the NuGet-bundled copies (WindowsAppSDKSelfContained) do not.
Source: "WindowsAppRuntimeInstall-x64.exe"; DestDir: "{tmp}"; Flags: ignoreversion deleteafterinstall

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
; 1. Install Windows App Runtime 1.8 silently (waits for completion before continuing).
;    RunOnceId prevents re-running on reinstall/repair if the runtime is already present.
Filename: "{tmp}\WindowsAppRuntimeInstall-x64.exe"; Parameters: "--quiet"; \
    StatusMsg: "Installing Windows App Runtime 1.8..."; \
    Flags: waituntilterminated; RunOnceId: "WinAppRuntime18"

; 2. Launch PassKey after installation completes.
Filename: "{app}\PassKey.Desktop.exe"; Description: "Launch PassKey"; Flags: nowait postinstall skipifsilent

; PassKey Inno Setup Script
; Builds the Windows installer for PassKey Desktop + BrowserHost

[Setup]
AppId={{A7F3C2D1-8E4B-4F9A-B6D5-3C1E7A2F0D84}
AppName=PassKey
AppVersion=1.0.15
AppVerName=PassKey 1.0.15
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
PrivilegesRequired=admin
UninstallDisplayIcon={app}\PassKey.Desktop.exe
WizardStyle=modern dynamic
SetupLogging=yes
CloseApplications=yes
RestartApplications=yes

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
; PassKey.Desktop is published self-contained (.NET bundled), so no separate .NET
; installer is needed.  The App Runtime provides Microsoft.UI.Xaml.dll and WinRT
; support; it is loaded at runtime via Bootstrap.Initialize().
Source: "WindowsAppRuntimeInstall-x64.exe"; DestDir: "{tmp}"; Flags: ignoreversion deleteafterinstall

; Published self-contained output (Desktop + BrowserHost)
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PassKey"; Filename: "{app}\PassKey.Desktop.exe"
Name: "{group}\{cm:UninstallProgram,PassKey}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\PassKey"; Filename: "{app}\PassKey.Desktop.exe"; Tasks: desktopicon

; Registry entries for passkey:// URL scheme and Native Messaging Hosts are NOT
; written here. PassKey.Desktop.exe registers them in HKCU at first launch via
; ProtocolActivationService.EnsureRegistered() — this avoids HKCU/HKLM conflicts
; when the installer runs elevated and also keeps uninstall clean (app removes them
; on uninstall).

[Run]
; 1. Install Windows App Runtime 1.8 silently.
;    Idempotent: exits immediately if the same or newer version is already present.
Filename: "{tmp}\WindowsAppRuntimeInstall-x64.exe"; Parameters: "--quiet"; \
    StatusMsg: "Installing Windows App Runtime 1.8..."; \
    Flags: waituntilterminated

; 2. Launch PassKey after installation completes.
Filename: "{app}\PassKey.Desktop.exe"; Description: "Launch PassKey"; Flags: nowait postinstall skipifsilent

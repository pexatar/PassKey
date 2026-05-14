; PassKey Inno Setup Script
; Builds the Windows installer for PassKey Desktop + BrowserHost

[Setup]
AppId={{A7F3C2D1-8E4B-4F9A-B6D5-3C1E7A2F0D84}
AppName=PassKey
AppVersion=1.0.17
AppVerName=PassKey 1.0.17
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
; Windows App Runtime 1.8 redistributable — installed silently if not already present.
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
; Launch PassKey after installation completes.
; (Windows App Runtime installation is handled by the [Code] section below,
;  which catches the 0xC0000142 DLL-init crash that occurs when the runtime
;  is already installed on the system.)
Filename: "{app}\PassKey.Desktop.exe"; Description: "Launch PassKey"; Flags: nowait postinstall skipifsilent

[Code]
{ ── Windows App Runtime bootstrap ─────────────────────────────────────────── }
{ Runs WindowsAppRuntimeInstall-x64.exe via Exec() so errors are handled        }
{ gracefully. The standalone EXE can crash with STATUS_DLL_INIT_FAILED           }
{ (0xC0000142) on systems where the runtime is already installed, which causes   }
{ a dialog when launched from the [Run] section. Using Exec() suppresses that.   }

procedure TryInstallWindowsAppRuntime();
var
  InstallerPath : String;
  ResultCode    : Integer;
begin
  InstallerPath := ExpandConstant('{tmp}\WindowsAppRuntimeInstall-x64.exe');

  if not FileExists(InstallerPath) then
  begin
    Log('WinAppRuntime: installer not found at ' + InstallerPath + ' — skipping.');
    Exit;
  end;

  Log('WinAppRuntime: launching installer...');

  if not Exec(InstallerPath, '--quiet', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    { Exec returns False when the process could not be created at all.            }
    { The most common cause on systems with the runtime already installed is      }
    { STATUS_DLL_INIT_FAILED (0xC0000142): the installer EXE crashes immediately  }
    { because a side-by-side manifest dependency can't load.  This is benign —   }
    { the runtime is already present and PassKey will launch correctly.           }
    Log('WinAppRuntime: installer failed to start (OS error ' + IntToStr(ResultCode)
        + '). Runtime is likely already installed — continuing.');
  end
  else
  begin
    { ResultCode 0  = success / already at this or newer version                 }
    { 0x80073D21 (2147954465) = package already registered — also success        }
    if (ResultCode = 0) or (ResultCode = 2147954465) then
      Log('WinAppRuntime: installer completed successfully (exit code ' + IntToStr(ResultCode) + ').')
    else
      Log('WinAppRuntime: installer returned exit code ' + IntToStr(ResultCode)
          + '. PassKey may still work if the runtime is already installed.');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    TryInstallWindowsAppRuntime();
end;

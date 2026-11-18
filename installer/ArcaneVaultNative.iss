; Arcane Vault native per-user installer. Keep ALL private user data outside {app}.
; Build: ISCC.exe /DAppVersion=0.9.109 installer\ArcaneVaultNative.iss
#ifndef AppVersion
  #error AppVersion must be passed by CI as /DAppVersion=x.y.z
#endif

[Setup]
AppId={{F8E586DB-6401-4BEA-A7CB-5D4A7E436F3E}
AppName=Arcane Vault
AppVersion={#AppVersion}
AppPublisher=RNGesus
UninstallDisplayName=Arcane Vault
CreateUninstallRegKey=yes
Uninstallable=yes
AppPublisherURL=https://github.com/thejohnfavaro/arcanevault
; A clean single app folder. A chosen destination IS the application root.
; Example: D:\Games\ArcaneVault, not D:\Games\ArcaneVault\AppData\Local\ArcaneVaultNativeApps.
DefaultDirName={localappdata}\Programs\ArcaneVault
DefaultGroupName=Arcane Vault
DisableDirPage=no
PrivilegesRequired=lowest
UsePreviousAppDir=yes
OutputDir=..\native-installer
OutputBaseFilename=ArcaneVaultSetup-win-x64
SetupIconFile=..\ArcaneVault.Next.Desktop\Assets\ArcaneVault.ico
UninstallDisplayIcon={app}\ArcaneVault.exe
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
CloseApplications=yes
RestartApplications=no
WizardStyle=modern
; CI generates these Windows BMPs from approved, repository-tracked art.
WizardImageFile=brand\ArcaneVaultWizard.bmp
WizardSmallImageFile=brand\ArcaneVaultWizardSmall.bmp

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
; A permanent, never-versioned taskbar and Start Menu target.
Source: "..\preview\ArcaneVaultLauncher\ArcaneVault.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ArcaneVault.Next.Desktop\Assets\ArcaneVault.ico"; DestDir: "{app}"; Flags: ignoreversion
; Never replace an already activated newer version on reinstall.
Source: "..\preview\ArcaneVaultLauncher\active-version.txt"; DestDir: "{app}"; Flags: onlyifdoesntexist
; Install a self-contained native build into the existing side-by-side update root.
Source: "..\preview\ArcaneVaultNextStandalone\*"; DestDir: "{app}\versions\v{#AppVersion}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Arcane Vault"; Filename: "{app}\ArcaneVault.exe"; WorkingDir: "{app}"; IconFilename: "{app}\ArcaneVault.ico"; AppUserModelID: "RNGesus.ArcaneVault"
Name: "{autodesktop}\Arcane Vault"; Filename: "{app}\ArcaneVault.exe"; WorkingDir: "{app}"; IconFilename: "{app}\ArcaneVault.ico"; AppUserModelID: "RNGesus.ArcaneVault"; Tasks: desktopicon

[Run]
Filename: "{app}\ArcaneVault.exe"; Description: "Launch Arcane Vault"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; This directory contains only managed application binaries, NEVER collections.
Type: filesandordirs; Name: "{app}\versions"

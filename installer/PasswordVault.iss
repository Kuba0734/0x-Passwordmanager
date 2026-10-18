; Setup für Password Vault – wird von deploy.ps1 aufgerufen:
;   ISCC.exe /DAppVersion=1.2.3 /DSourceDir=<Publish-Ordner> /DOutputDir=<artifacts> installer\PasswordVault.iss
; Installiert pro Benutzer ohne Admin-Rechte nach %LOCALAPPDATA%\Programs, damit der
; eingebaute Updater die Dateien später selbst überschreiben darf.

#ifndef AppVersion
  #error AppVersion fehlt (/DAppVersion=x.y.z)
#endif
#ifndef SourceDir
  #error SourceDir fehlt (/DSourceDir=...)
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#define AppName "Password Vault"
#define AppExe "PasswordVault.App.exe"

[Setup]
; Feste AppId – nie ändern, sonst erkennt Windows Updates nicht als dieselbe App
AppId={{6B1F2C1E-7A3D-4E5B-9C0A-2F8D4E6A1B37}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Kuba0734
AppPublisherURL=https://github.com/Kuba0734/0x-Passwordmanager
VersionInfoVersion={#AppVersion}
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppName}
DisableDirPage=yes
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\PasswordVault.App\Assets\app.ico
OutputDir={#OutputDir}
OutputBaseFilename=PasswordVaultSetup-x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "de"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Laufende App (auch nur im Infobereich) beenden, sonst bleiben Dateien gesperrt
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExe}"; Flags: runhidden; RunOnceId: "KillApp"

[Registry]
; Autostart-Eintrag der App (AutostartService) bei der Deinstallation entfernen – nicht anlegen
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "PasswordVault"; Flags: uninsdeletevalue dontcreatekey

; Nutzerdaten (Tresor, Einstellungen) liegen in %LOCALAPPDATA%\PasswordVault und bleiben erhalten.

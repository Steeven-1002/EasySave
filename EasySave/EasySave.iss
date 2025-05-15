[Setup]
AppName=EasySave by ProSoft
AppVersion=1.1.0
DefaultDirName={autopf}\EasySave
DefaultGroupName=EasySave
OutputDir=.\Installer
OutputBaseFilename=EasySave_Installer_v1.1.0
Compression=lzma
SolidCompression=yes
DisableProgramGroupPage=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=poweruser
SetupIconFile=extras\EasySave_Icon.ico

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\EasySave"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Fichiers à copier dans C:\EasySaveConfig
Source: "extras\lang_en.json"; DestDir: "{userappdata}\EasySave\lang"; Flags: ignoreversion comparetimestamp
Source: "extras\lang_fr.json"; DestDir: "{userappdata}\EasySave\lang"; Flags: ignoreversion comparetimestamp
Source: "extras\EasySave_Icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\EasySave"; Filename: "{app}\EasySave.exe"; WorkingDir: "{app}"; IconFilename: "{app}\EasySave_Icon.ico"
Name: "{commondesktop}\EasySave"; Filename: "{app}\EasySave.exe"; Tasks: desktopicon; IconFilename: "{app}\EasySave_Icon.ico"

[Tasks]
Name: "desktopicon"; Description: "Créer une icône sur le bureau"; GroupDescription: "Icônes supplémentaires :"

[Run]
Filename: "{app}\EasySave.exe"; Description: "Lancer EasySave"; Flags: nowait postinstall skipifsilent


[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\EasySave"

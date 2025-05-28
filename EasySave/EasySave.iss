[Setup]
AppName=EasySave by ProSoft
AppVersion=3.0.0
DefaultDirName={autopf}\EasySave
DefaultGroupName=EasySave
OutputDir=.\Installer
OutputBaseFilename=EasySave_Installer_v3.0.0
Compression=lzma
SolidCompression=yes
DisableProgramGroupPage=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=poweruser
SetupIconFile=extras\EasySave_Icon.ico

[Files]
; EasySave app
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; CryptoSoft
Source: "CryptoSoft\CryptoSoft.exe"; DestDir: "{autopf}\CryptoSoft"; Flags: ignoreversion

; Icône
Source: "extras\EasySave_Icon.ico"; DestDir: "{app}"; Flags: ignoreversion

; .NET Desktop Runtime (ex: .NET 8)
Source: "extras\dotnet-desktop-runtime-8.0.0-win-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\EasySave"; Filename: "{app}\EasySave_by_ProSoft.exe"; WorkingDir: "{app}"; IconFilename: "{app}\EasySave_Icon.ico"
Name: "{commondesktop}\EasySave"; Filename: "{app}\EasySave_by_ProSoft.exe"; Tasks: desktopicon; IconFilename: "{app}\EasySave_Icon.ico"

[Tasks]
Name: "desktopicon"; Description: "Créer une icône sur le bureau"; GroupDescription: "Icônes supplémentaires :"

[Run]
; Installe le runtime .NET 8 si nécessaire
Filename: "{tmp}\dotnet-desktop-runtime-8.0.0-win-x64.exe"; Parameters: "/install /quiet /norestart"; Check: NeedsDotNet; StatusMsg: "Installation du .NET Runtime..."

; Lancer EasySave après installation
Filename: "{app}\EasySave_by_ProSoft.exe"; Description: "Lancer EasySave"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\EasySave"

[Registry]
Root: HKLM; Subkey: "Software\ProSoft\EasySave"; ValueType: string; ValueName: "CryptoSoftPath"; ValueData: "{autopf}\CryptoSoft\CryptoSoft.exe"; Flags: uninsdeletevalue

[Code]
function NeedsDotNet(): Boolean;
var
  key: string;
begin
  key := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft .NET Runtime - 8 (Windows Desktop)';
  Result := not RegKeyExists(HKLM, key);
end;

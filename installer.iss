; Discord SysInfo RPC Installer Script
; Requires Inno Setup 6.x - https://jrsoftware.org/isinfo.php

#define MyAppName "Discord SysInfo RPC"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "Eranary / Disflare"
#define MyAppURL "https://disflare.com"
#define MyAppExeName "DiscordSysInfoRPC.exe"

[Setup]
AppId={{8A7B6C5D-4E3F-2A1B-0C9D-8E7F6A5B4C3D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoCopyright=Copyright (C) 2026 {#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableDirPage=no
DisableProgramGroupPage=yes
AllowNoIcons=yes

; 64-bit architecture
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Output settings
OutputDir=installer_output
OutputBaseFilename=DiscordSysInfoRPC_Setup

; Compression
Compression=lzma2/ultra64
SolidCompression=yes

; UI / icons
WizardStyle=modern
SetupIconFile=icodiscord.ico

; Privileges (admin needed for Program Files and hardware sensor driver)
PrivilegesRequired=admin
UsedUserAreasWarning=no

; Uninstaller
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

#include "InstallerCode.iss"

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIconTask}"; GroupDescription: "{cm:TasksGroup}"; Flags: checkedonce

[Files]
Source: "release\DiscordSysInfoRPC.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\icodiscord.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "release\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "release\*.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist onlyifdoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallEntry}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:RunApp}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallDelete]
Type: files; Name: "{app}\config.json"
Type: dirifempty; Name: "{app}"
Type: filesandordirs; Name: "{userappdata}\DiscordSysInfoRPC"

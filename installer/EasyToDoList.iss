#ifndef AppVersion
  #error AppVersion is required. Build with scripts/package-release.ps1.
#endif
#ifndef FileVersion
  #error FileVersion is required.
#endif
#ifndef PublishDir
  #error PublishDir is required.
#endif
#ifndef InstallerOutputDir
  #error InstallerOutputDir is required.
#endif

[Setup]
AppId={{B8FD5D8B-4472-4ACF-AD13-BBF48BFE1D41}
AppName=EasyToDoList
AppVersion={#AppVersion}
AppVerName=EasyToDoList {#AppVersion}
AppPublisher=Btrung-uit
AppPublisherURL=https://github.com/Btrung-uit/EasyToDoList
DefaultDirName={localappdata}\Programs\EasyToDoList
DefaultGroupName=EasyToDoList
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
AppMutex=Local\EasyToDoList-Running
CloseApplications=no
RestartApplications=no
UninstallDisplayIcon={app}\EasyToDoList.App.exe
SetupIconFile=..\assets\branding\easytodolist.ico
OutputDir={#InstallerOutputDir}
OutputBaseFilename=EasyToDoList-{#AppVersion}-win-x64-setup
VersionInfoVersion={#FileVersion}
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\EasyToDoList"; Filename: "{app}\EasyToDoList.App.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\EasyToDoList"; Filename: "{app}\EasyToDoList.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\EasyToDoList.App.exe"; Description: "Open EasyToDoList"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupCommand: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'EasyToDoList', StartupCommand) then
      if CompareText(StartupCommand, '"' + ExpandConstant('{app}\EasyToDoList.App.exe') + '" --tray') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'EasyToDoList');
end;

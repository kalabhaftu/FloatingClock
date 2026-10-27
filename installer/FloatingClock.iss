; Floating Clock — Inno Setup installer script
; Builds a clean Windows installer with optional desktop & startup shortcuts

[Setup]
AppName=Floating Clock
AppVersion=1.0.0
AppVerName=Floating Clock 1.0.0
AppPublisher=FloatingClock
DefaultDirName={autopf}\Floating Clock
DefaultGroupName=Floating Clock
OutputDir=..\output
OutputBaseFilename=FloatingClockSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
SetupIconFile=..\src\FloatingClock\Assets\app.ico
UninstallDisplayIcon={app}\FloatingClock.exe
CloseApplications=yes
RestartApplications=no
PrivilegesRequired=lowest
DisableProgramGroupPage=yes

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Floating Clock";           Filename: "{app}\FloatingClock.exe"
Name: "{group}\Uninstall Floating Clock";  Filename: "{uninstallexe}"
Name: "{autodesktop}\Floating Clock";      Filename: "{app}\FloatingClock.exe"; Tasks: desktopicon
Name: "{userstartup}\Floating Clock";      Filename: "{app}\FloatingClock.exe"; Tasks: startupicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut";   GroupDescription: "Additional shortcuts:"
Name: "startupicon"; Description: "Start with &Windows";          GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\FloatingClock.exe"; Description: "Launch Floating Clock"; Flags: nowait postinstall skipifsilent

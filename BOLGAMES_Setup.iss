; -- Скрипт установщика BOLGAMES Launcher --

[Setup]
AppName=BOLGAMES Launcher
AppVersion=1.0.4
AppPublisher=bmv1986
DefaultDirName={localappdata}\BOLGAMES
DefaultGroupName=BOLGAMES
OutputDir=C:\Users\bmv86\Downloads\BOLGAMES\InstallerOutput
OutputBaseFilename=BOLGAMES_Launcher_Setup
Compression=lzma2/max
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
; Если есть иконка, раскомментируй строку ниже и положи icon.ico в папку BOLGAMES
SetupIconFile=C:\Users\bmv86\Downloads\BOLGAMES\icon.ico
UninstallDisplayIcon={app}\BOLGAMES.exe

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Копируем ВСЕ файлы из папки publish (включая библиотеки .NET)
Source: "C:\Users\bmv86\Downloads\BOLGAMES\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\BOLGAMES Launcher"; Filename: "{app}\BOLGAMES.exe"; IconFilename: "{app}\BOLGAMES.exe"
Name: "{userdesktop}\BOLGAMES Launcher"; Filename: "{app}\BOLGAMES.exe"; IconFilename: "{app}\BOLGAMES.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BOLGAMES.exe"; Description: "{cm:LaunchProgram,BOLGAMES Launcher}"; Flags: nowait postinstall skipifsilent

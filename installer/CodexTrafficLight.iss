#define AppName "Codex 红绿灯"
#define AppVersion "1.0.3"
#define AppPublisher "Gyk"
#define AppExeName "CodexTrafficLight.App.exe"
; 发布文件由 tools\publish-installer.ps1 在运行 ISCC 前创建。
#define PublishDir "..\dist\CodexTrafficLight-installer-files"

[Setup]
; 默认按用户安装，使应用不需要提权。
AppId={{8B80A5D1-493C-4F63-9D40-9EA8C8793F4E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
; 安装到 Program Files 下的独立目录，避免和源码目录或 Codex 配置目录混在一起。
DefaultDirName={autopf}\CodexTrafficLight
AppendDefaultDirName=yes
DisableProgramGroupPage=yes
; 安装包输出到 dist\installer，文件名自动带版本号。
OutputDir=..\dist\installer
OutputBaseFilename=CodexTrafficLightSetup-{#AppVersion}
SetupIconFile=..\src\CodexTrafficLight.App\Assets\app-icon.ico
; 使用较高压缩率，减少自包含运行时带来的安装包体积。
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
; 当前发布脚本只生成 win-x64，自包含文件也按 x64 安装。
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
; 复制自包含发布输出，但不把调试符号放入安装包。
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; 开始菜单快捷方式总会安装；桌面快捷方式由可选桌面任务控制。
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "运行 {#AppName}"; Flags: nowait postinstall skipifsilent

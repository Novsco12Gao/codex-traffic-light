namespace CodexTrafficLight.Tests;

/// <summary>
/// 验证影响安装路径和快捷方式的安装脚本设置。
/// 这些断言能防止安装包输出目录、快捷方式和安装后启动配置被误删。
/// </summary>
public sealed class InstallerScriptTests
{
    [Fact]
    public void InnoSetupScriptInstallsIntoDedicatedFolderWithShortcuts()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "installer", "CodexTrafficLight.iss");

        Assert.True(File.Exists(scriptPath), "Expected installer/CodexTrafficLight.iss to exist.");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("#define AppName \"Codex 红绿灯\"", script);
        Assert.Contains("#define AppExeName \"CodexTrafficLight.App.exe\"", script);
        Assert.Contains("#define PublishDir \"..\\dist\\CodexTrafficLight-installer-files\"", script);
        Assert.Contains("DefaultDirName={autopf}\\CodexTrafficLight", script);
        Assert.Contains("AppendDefaultDirName=yes", script);
        Assert.Contains(@"Name: ""chinesesimp""; MessagesFile: ""compiler:Languages\ChineseSimplified.isl""", script);
        Assert.Contains("OutputDir=..\\dist\\installer", script);
        Assert.Contains("OutputBaseFilename=CodexTrafficLightSetup-{#AppVersion}", script);
        Assert.Contains(@"Source: ""{#PublishDir}\*""; DestDir: ""{app}""; Excludes: ""*.pdb""; Flags: ignoreversion recursesubdirs createallsubdirs", script);
        Assert.Contains(@"Name: ""{autoprograms}\{#AppName}""; Filename: ""{app}\{#AppExeName}""", script);
        Assert.Contains(@"Name: ""{autodesktop}\{#AppName}""; Filename: ""{app}\{#AppExeName}""; Tasks: desktopicon", script);
        Assert.Contains(@"Filename: ""{app}\{#AppExeName}""; Description: ""运行 {#AppName}""; Flags: nowait postinstall skipifsilent", script);
    }

    private static string FindRepositoryRoot()
    {
        // 测试执行从 bin 输出目录开始，因此向上查找解决方案文件。
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CodexTrafficLight.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate CodexTrafficLight.sln.");
    }
}

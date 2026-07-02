param(
    # dotnet publish 使用的构建配置，默认发布 Release。
    [string]$Configuration = "Release",

    # 自包含 Windows 安装包使用的运行时标识。
    [string]$Runtime = "win-x64"
)

# 任何一步失败都立即停止，避免用旧的发布目录继续生成安装包。
$ErrorActionPreference = "Stop"

# 所有路径都相对仓库根目录计算，因此脚本可以从任意当前目录运行。
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\CodexTrafficLight.App\CodexTrafficLight.App.csproj"
$publishDir = Join-Path $repoRoot "dist\CodexTrafficLight-installer-files"
$installerScript = Join-Path $repoRoot "installer\CodexTrafficLight.iss"

# 先把 WPF 应用发布为自包含文件夹，供 Inno Setup 打包。
dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=false `
    -o $publishDir

# 优先检查常见安装位置，再回退到 PATH 中的 ISCC.exe。
$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
)

$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        $iscc = $command.Source
    }
}

if (-not $iscc) {
    # 没有 Inno Setup 时仍保留发布目录，方便用户之后手动运行安装器编译。
    Write-Host "Installer files published to: $publishDir"
    Write-Host "Inno Setup 6 is not installed, so the setup EXE was not built."
    Write-Host "Install Inno Setup 6, then run this script again."
    exit 2
}

# 使用 Inno Setup 脚本生成最终安装包。
& $iscc $installerScript

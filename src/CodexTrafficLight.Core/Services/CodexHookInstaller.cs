using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodexTrafficLight.Core.Services;

/// <summary>
/// 安装 Codex hook 配置项，以及写入状态文件的 PowerShell 脚本。
/// 安装过程是幂等的，多次调用只会更新本应用拥有的 hook 项。
/// </summary>
public sealed class CodexHookInstaller
{
    // 用于识别本应用拥有的 hook 项，避免影响用户自己的 hooks。
    private const string Marker = "codex_traffic_light_write_status.ps1";
    private const string AppExeName = "CodexTrafficLight.App.exe";
    private readonly CodexPaths _paths;

    /// <summary>
    /// 使用统一路径对象定位 hooks.json、脚本目录和会话目录。
    /// </summary>
    public CodexHookInstaller(CodexPaths paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// 创建或更新 hook 配置，并返回 hooks.json 路径。
    /// 方法会先确保脚本存在，再把 Codex 生命周期事件映射到红黄绿状态。
    /// </summary>
    public string InstallOrUpdate()
    {
        _paths.EnsureCodexDirectory();
        EnsureHookScript();

        var root = LoadRoot();
        var hooks = root["hooks"] as JsonObject ?? new JsonObject();
        root["hooks"] = hooks;

        // 将 Codex 生命周期事件映射为应用显示的颜色。
        AddOwnedEvent(hooks, "UserPromptSubmit", "red");
        AddOwnedEvent(hooks, "PermissionRequest", "yellow");
        AddOwnedEvent(hooks, "Stop", "green");
        AddOwnedEvent(hooks, "SessionStart", "green");

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        if (!File.Exists(_paths.HooksPath) || File.ReadAllText(_paths.HooksPath) != json)
        {
            File.WriteAllText(_paths.HooksPath, json);
        }

        return _paths.HooksPath;
    }

    private JsonObject LoadRoot()
    {
        // 没有 hooks.json 时从空对象开始，后续只写入本应用需要的 hooks 节点。
        if (!File.Exists(_paths.HooksPath))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(_paths.HooksPath)) as JsonObject ?? new JsonObject();
        }
        catch
        {
            // 替换为新配置对象前，先保留无效的用户配置。
            var backupPath = _paths.HooksPath + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bak";
            File.Copy(_paths.HooksPath, backupPath, overwrite: false);
            return new JsonObject();
        }
    }

    private void AddOwnedEvent(JsonObject hooks, string eventName, string state)
    {
        // 一个 Codex 事件可以有多个 hook；这里只移除本应用旧项，并保留其它来源的 hook。
        var existing = hooks[eventName] as JsonArray ?? new JsonArray();
        var cleaned = new JsonArray();

        // 保留第三方或用户 hook 项，只替换本应用旧项。
        foreach (var item in existing)
        {
            if (item is not JsonObject obj || !ContainsOwnedCommand(obj))
            {
                cleaned.Add(item?.DeepClone());
            }
        }

        cleaned.Add(CreateHookEntry(eventName, state));
        hooks[eventName] = cleaned;
    }

    private static bool ContainsOwnedCommand(JsonObject entry)
    {
        // 通过脚本文件名识别本应用写入的命令，而不是依赖数组位置。
        var handlers = entry["hooks"] as JsonArray;
        if (handlers is null)
        {
            return false;
        }

        return handlers.Any(handler =>
            handler is JsonObject obj &&
            obj["command"]?.GetValue<string>().Contains(Marker, StringComparison.OrdinalIgnoreCase) == true);
    }

    private JsonObject CreateHookEntry(string eventName, string state)
    {
        // Codex 要求每个事件对应一个包含 hooks 数组的对象。
        return new JsonObject
        {
            ["hooks"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = BuildPowerShellCommand(state, eventName),
                    ["statusMessage"] = $"Codex 红绿灯：{state}"
                }
            }
        };
    }

    private string BuildPowerShellCommand(string state, string eventName)
    {
        // 生成给 Codex 调用的完整 PowerShell 命令，所有参数都单独加引号。
        return $"powershell -NoProfile -ExecutionPolicy Bypass -File {QuoteArg(_paths.HookScriptPath)} -State {QuoteArg(state)} -EventName {QuoteArg(eventName)}";
    }

    private static string QuoteArg(string value)
    {
        // Codex hooks.json 中的命令参数需要手动转义双引号。
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private void EnsureHookScript()
    {
        // hook 脚本由应用生成，这样安装包更新后可以同步修复脚本逻辑。
        Directory.CreateDirectory(_paths.HookScriptDirectory);

        var appPath = ResolveAppPath();
        // 生成的脚本在 Codex hooks 中运行，因此避免依赖应用程序集。
        var script = $$"""
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('red','yellow','green','unknown')]
    [string]$State,

    [Parameter(Mandatory=$false)]
    [string]$EventName = 'unknown'
)

$codexDir = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $env:USERPROFILE '.codex' }
if (-not (Test-Path -LiteralPath $codexDir)) {
    New-Item -ItemType Directory -Path $codexDir -Force | Out-Null
}

$toolDir = Join-Path $codexDir 'codex-traffic-light'
$sessionsDir = Join-Path $toolDir 'sessions'
$diagnosticsDir = Join-Path $toolDir 'diagnostics'
New-Item -ItemType Directory -Path $sessionsDir -Force | Out-Null
New-Item -ItemType Directory -Path $diagnosticsDir -Force | Out-Null

$settingsPath = '{{EscapePowerShellSingleQuotedString(_paths.SettingsPath)}}'
$appPath = '{{EscapePowerShellSingleQuotedString(appPath)}}'

function Test-CodexTrafficLightAutoLaunchEnabled {
    if (-not (Test-Path -LiteralPath $settingsPath)) {
        return $true
    }

    try {
        $settings = Get-Content -Raw -LiteralPath $settingsPath -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
        if ($null -eq $settings.AutoLaunchOnCodexActivity) {
            return $true
        }

        return [bool]$settings.AutoLaunchOnCodexActivity
    } catch {
        return $true
    }
}

function Start-CodexTrafficLightIfNeeded {
    if ($EventName -notin @('SessionStart', 'UserPromptSubmit', 'PermissionRequest')) {
        return
    }

    if (-not (Test-CodexTrafficLightAutoLaunchEnabled)) {
        return
    }

    if (-not (Test-Path -LiteralPath $appPath)) {
        return
    }

    $running = Get-Process -Name 'CodexTrafficLight.App' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($running) {
        return
    }

    Start-Process -FilePath $appPath
}

try {
    [Console]::InputEncoding = [Text.Encoding]::UTF8
    [Console]::OutputEncoding = [Text.Encoding]::UTF8
} catch {
}

$rawHookInput = ''
try {
    # Codex 通过标准输入发送 hook 元数据；这里容忍空内容或格式错误。
    $rawHookInput = [Console]::In.ReadToEnd()
    $rawHookInput = $rawHookInput.TrimStart([char]0xFEFF)
} catch {
    $rawHookInput = ''
}

$hookInput = $null
if (-not [string]::IsNullOrWhiteSpace($rawHookInput)) {
    try {
        $hookInput = $rawHookInput | ConvertFrom-Json -ErrorAction Stop
    } catch {
        $hookInput = $null
    }
}

$currentProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $PID"
$parentProcess = $null
if ($currentProcess.ParentProcessId) {
    $parentProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $($currentProcess.ParentProcessId)" -ErrorAction SilentlyContinue
}

$codexProcess = $parentProcess
$cursor = $parentProcess
for ($i = 0; $i -lt 6 -and $cursor; $i++) {
    # 沿父进程向上查找，因为 hook 的 PowerShell 进程通常位于 Codex 下方。
    if ($cursor.Name -match 'codex') {
        $codexProcess = $cursor
        break
    }

    if (-not $cursor.ParentProcessId) {
        break
    }

    $cursor = Get-CimInstance Win32_Process -Filter "ProcessId = $($cursor.ParentProcessId)" -ErrorAction SilentlyContinue
}

$processId = if ($codexProcess) { [int]$codexProcess.ProcessId } else { [int]$PID }
$processStart = $null
try {
    if ($codexProcess -and $codexProcess.CreationDate) {
        if ($codexProcess.CreationDate -is [datetime]) {
            $processStart = $codexProcess.CreationDate.ToString('o')
        } else {
            $processStart = [Management.ManagementDateTimeConverter]::ToDateTime($codexProcess.CreationDate).ToString('o')
        }
    }
} catch {
    $processStart = $null
}

$inputSessionId = $null
if ($hookInput -and $hookInput.session_id) {
    $inputSessionId = [string]$hookInput.session_id
} elseif ($hookInput -and $hookInput.conversation_id) {
    $inputSessionId = [string]$hookInput.conversation_id
}

$inputPrompt = $null
if ($hookInput -and $hookInput.prompt) {
    $inputPrompt = ([string]$hookInput.prompt) -replace '\s+', ' '
    $inputPrompt = $inputPrompt.Trim()
    if ($inputPrompt.Length -gt 36) {
        $inputPrompt = $inputPrompt.Substring(0, 36)
    }
}

$sessionId = $env:CODEX_TRAFFIC_LIGHT_SESSION_ID
if ([string]::IsNullOrWhiteSpace($sessionId)) {
    if (-not [string]::IsNullOrWhiteSpace($inputSessionId)) {
        $sessionId = "codex-$inputSessionId"
    } else {
        # 当 Codex 未提供 ID 时，退回使用 PID 和进程启动标记。
        $startToken = if ($processStart) { $processStart } else { 'unknown-start' }
        $sessionId = "pid-$processId-" + ($startToken -replace '[^0-9A-Za-z]', '')
    }
}
$safeSessionId = $sessionId -replace '[^0-9A-Za-z_-]', '_'

$source = if ($codexProcess -and $codexProcess.CommandLine -match 'app-server') { 'vscode-plugin' } else { 'cli' }

$workingDirectory = if ($env:CODEX_TRAFFIC_LIGHT_WORKING_DIRECTORY) {
    $env:CODEX_TRAFFIC_LIGHT_WORKING_DIRECTORY
} elseif ($hookInput -and $hookInput.cwd) {
    [string]$hookInput.cwd
} else {
    (Get-Location).Path
}

$sessionPath = Join-Path $sessionsDir "$safeSessionId.json"
$sessionTempPath = Join-Path $sessionsDir "$safeSessionId.json.tmp"
$previousDisplayName = $null
if (Test-Path -LiteralPath $sessionPath) {
    try {
        $previousSession = Get-Content -Raw -LiteralPath $sessionPath -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
        if ($previousSession.displayName) {
            $previousDisplayName = [string]$previousSession.displayName
        }
    } catch {
        $previousDisplayName = $null
    }
}

$displayName = if ($env:CODEX_TRAFFIC_LIGHT_TASK_NAME) {
    $env:CODEX_TRAFFIC_LIGHT_TASK_NAME
} elseif ($env:CODEX_TRAFFIC_LIGHT_SESSION_NAME) {
    $env:CODEX_TRAFFIC_LIGHT_SESSION_NAME
} elseif (-not [string]::IsNullOrWhiteSpace($inputPrompt)) {
    $inputPrompt
} elseif (-not [string]::IsNullOrWhiteSpace($previousDisplayName)) {
    $previousDisplayName
} else {
    Split-Path -Leaf $workingDirectory
}
if ([string]::IsNullOrWhiteSpace($displayName)) {
    $displayName = 'Codex'
}

$statusPath = Join-Path $codexDir 'codex_traffic_light_state.json'
$payload = [ordered]@{
    state = $State
    event = $EventName
    updatedAt = (Get-Date).ToString('o')
} | ConvertTo-Json -Compress

Set-Content -LiteralPath $statusPath -Value $payload -Encoding UTF8

$sessionPayload = [ordered]@{
    sessionId = $safeSessionId
    displayName = $displayName
    workingDirectory = $workingDirectory
    source = $source
    state = $State
    event = $EventName
    processId = $processId
    processStartTime = $processStart
    updatedAt = (Get-Date).ToString('o')
} | ConvertTo-Json -Compress

Set-Content -LiteralPath $sessionTempPath -Value $sessionPayload -Encoding UTF8
# 原子替换可避免 WPF 监听器读到半写入的 JSON 文件。
Move-Item -LiteralPath $sessionTempPath -Destination $sessionPath -Force

$diagnosticPath = Join-Path $diagnosticsDir 'latest-hook-context.json'
$diagnosticPayload = [ordered]@{
    event = $EventName
    state = $State
    hookProcessId = [int]$PID
    parentProcessId = if ($parentProcess) { [int]$parentProcess.ProcessId } else { $null }
    parentCommandLine = if ($parentProcess) { $parentProcess.CommandLine } else { $null }
    codexProcessId = $processId
    codexCommandLine = if ($codexProcess) { $codexProcess.CommandLine } else { $null }
    workingDirectory = $workingDirectory
    source = $source
    environmentSessionId = $env:CODEX_TRAFFIC_LIGHT_SESSION_ID
    inputSessionId = $inputSessionId
    promptPreview = $inputPrompt
    rawHookInput = if ($rawHookInput.Length -gt 2000) { $rawHookInput.Substring(0, 2000) } else { $rawHookInput }
    updatedAt = (Get-Date).ToString('o')
} | ConvertTo-Json -Compress -Depth 4

Set-Content -LiteralPath $diagnosticPath -Value $diagnosticPayload -Encoding UTF8

Start-CodexTrafficLightIfNeeded
""";

        if (!File.Exists(_paths.HookScriptPath) || File.ReadAllText(_paths.HookScriptPath) != script)
        {
            File.WriteAllText(_paths.HookScriptPath, script);
        }
    }

    private static string ResolveAppPath()
    {
        // 已安装应用可通过 Environment.ProcessPath 发现自身可执行文件。
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) &&
            string.Equals(Path.GetFileName(processPath), AppExeName, StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        var baseDirectoryCandidate = Path.Combine(AppContext.BaseDirectory, AppExeName);
        if (File.Exists(baseDirectoryCandidate))
        {
            return baseDirectoryCandidate;
        }

        return Path.Combine(AppContext.BaseDirectory, AppExeName);
    }

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        // PowerShell 单引号字符串通过连续两个单引号转义字面单引号。
        return value.Replace("'", "''");
    }
}

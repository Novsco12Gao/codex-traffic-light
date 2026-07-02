param(
    # 可选的友好名称，会显示在红绿灯会话抽屉中。
    # 不传时使用当前工作目录名称，方便用户区分多个命令行窗口。
    [Parameter(Mandatory = $false)]
    [Alias("Name")]
    [string]$TaskName,

    # 该脚本选项之后的所有参数都会转发给真正的 codex 命令。
    # 例如 --dangerously-bypass-approvals-and-sandbox 会原样传给 codex。
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$CodexArgs
)

# hook 脚本读取这些变量，为每次命令创建稳定会话身份。
# 这样即使用户同时打开多个 Codex CLI，任务抽屉也能把它们拆成独立会话。
$env:CODEX_TRAFFIC_LIGHT_SESSION_ID = [guid]::NewGuid().ToString("N")
$env:CODEX_TRAFFIC_LIGHT_SESSION_NAME = if ([string]::IsNullOrWhiteSpace($TaskName)) { Split-Path -Leaf (Get-Location) } else { $TaskName }
$env:CODEX_TRAFFIC_LIGHT_TASK_NAME = $env:CODEX_TRAFFIC_LIGHT_SESSION_NAME
$env:CODEX_TRAFFIC_LIGHT_WORKING_DIRECTORY = (Get-Location).Path

# 设置已安装 hooks 期望的会话上下文后启动 Codex。
# 当前 PowerShell 进程中的环境变量会传给子进程，hook 脚本即可读取这些值。
codex @CodexArgs

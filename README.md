# Codex 红绿灯

这是一个纯本地 Windows 小工具，用悬浮红黄绿灯显示 Codex CLI 的运行状态。

## 功能

- 始终置顶的三灯红绿灯窗口。
- 红灯表示 Codex 正在处理任务。
- 黄灯表示 Codex 正在等待权限确认。
- 绿灯表示当前轮次结束或空闲。
- 支持托盘右键菜单、手动切灯、单灯/三灯切换、深浅色切换、本周周报。
- 自动写入 Codex hooks，但不会覆盖用户已有 hooks。
- 多个 Codex CLI 或 VS Code 插件聊天同时运行时，会按会话显示任务状态抽屉。
- 不需要服务器；只有手动点击 `检查更新` 时会访问 GitHub 上的 `version.json`。

## 本地运行

```powershell
dotnet run --project src/CodexTrafficLight.App
```

## 打包安装器

```powershell
powershell -ExecutionPolicy Bypass -File tools\publish-installer.ps1
```

输出目录：

```text
dist\installer\CodexTrafficLightSetup-1.0.3.exe
```

安装器会让用户选择安装位置，并把程序文件安装到独立的 `CodexTrafficLight` 文件夹中，同时创建开始菜单快捷方式，可选创建桌面快捷方式。

## 检查更新

托盘菜单的 `检查更新` 会请求远程 `version.json`，只提示新版并询问是否打开 GitHub 下载页，不会自动下载或替换 EXE。

当前默认读取 `https://raw.githubusercontent.com/Novsco12Gao/codex-traffic-light/main/version.json`。发布新版时，更新根目录 `version.json` 的版本号、更新说明和 GitHub Release 地址即可。

## Codex hooks 信任

程序会把 hooks 写入 Codex 配置目录：

```text
%CODEX_HOME%\hooks.json
```

如果没有设置 `CODEX_HOME`，则写入：

```text
%USERPROFILE%\.codex\hooks.json
```

写入后，打开 Codex 并运行：

```text
/hooks
```

信任 Codex 红绿灯 hooks。未信任时，Codex 会跳过这些命令 hook。

## 本地文件

```text
%CODEX_HOME%\codex_traffic_light_state.json
%CODEX_HOME%\codex_traffic_light_settings.json
%CODEX_HOME%\codex_traffic_light_stats.json
%CODEX_HOME%\codex-traffic-light\codex_traffic_light_write_status.ps1
%CODEX_HOME%\codex-traffic-light\sessions\*.json
%CODEX_HOME%\codex-traffic-light\diagnostics\latest-hook-context.json
```

未设置 `CODEX_HOME` 时，上述文件位于 `%USERPROFILE%\.codex`。

## 多会话模式

只开一个 Codex 会话时，界面保持普通小红绿灯。检测到两个及以上 Codex CLI 或 VS Code 插件聊天时，齿轮旁会出现数量角标；点击齿轮可展开右侧任务抽屉。抽屉按黄灯、红灯、绿灯排序，黄灯表示等待权限确认，红灯表示处理中，绿灯表示已完成。

hook 会优先读取 Codex 传入的 `session_id`、`cwd` 和 `prompt`。因此同一个 VS Code 工作区里的两个插件聊天也可以拆成两条会话，任务名会优先使用最近一次用户输入的内容，而不是只显示路径。

会话默认按短时规则隐藏旧状态：黄灯 5 分钟无更新后隐藏，红灯 10 分钟无更新后隐藏，绿灯 5 分钟后隐藏。如果之后继续旧聊天，下一次发送消息会用同一个 `session_id` 恢复监控。

托盘菜单里的 `显示已结束会话` 可以临时查看已经完成但默认隐藏的绿灯会话；`清理已完成会话` 会删除这些已完成会话文件。

hook 默认会尝试从父进程识别触发事件的 Codex CLI。如果现场发现多个 CLI 无法稳定区分，可以使用备用启动器启动 Codex：

```powershell
.\tools\codex-light.ps1
```

启动器会为当前 Codex CLI 注入独立会话 ID、会话名称和工作目录。

也可以显式指定任务名称，让抽屉比只显示路径更直观：

```powershell
.\tools\codex-light.ps1 -TaskName "MES 主项目"
```

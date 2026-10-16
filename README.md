# CodexQuota

[![Windows Build](https://github.com/Levil-ww/CodexQuota/actions/workflows/windows.yml/badge.svg)](https://github.com/Levil-ww/CodexQuota/actions/workflows/windows.yml)
[![Release](https://img.shields.io/github/v/release/Levil-ww/CodexQuota)](https://github.com/Levil-ww/CodexQuota/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Windows 桌面 Codex 额度悬浮窗，显示 **5 小时与每周剩余额度**，支持拖动定位、重置时间提示、自动刷新、开机启动和系统托盘管理。

![CodexQuota 悬浮窗预览](docs/preview.png)

> 预览使用演示数据。实际读数来自本机 Codex CLI 登录的账户。本项目为独立社区工具，与 OpenAI 无隶属关系。

## 功能

- 首次启动在屏幕顶部居中，保持置顶，不占用任务栏。
- 左键拖动到任意位置，包括 GPT/Codex 头像上方；松开后记住位置。
- 显示两项**剩余百分比**，每 60 秒自动刷新，也可手动刷新。
- 悬停查看重置时间、本地时区和上次成功更新时间。
- 剩余低于等于 25% 显示橙色，低于等于 10% 显示红色。
- 托盘菜单支持显示/隐藏、恢复顶部居中、开机启动和退出。
- 连接失败保留上次读数并显示 `*`；未知额度显示 `--`。
- 单实例运行，开机启动默认关闭，无第三方运行库依赖。

## 下载与运行

1. 前往 [Releases](https://github.com/Levil-ww/CodexQuota/releases/latest) 下载 `CodexQuota-Windows.zip`。
2. 解压到准备长期存放、当前用户可写的文件夹。
3. 双击 `CodexQuota.exe`。无需管理员权限。
4. 右键悬浮窗或托盘图标，按需勾选“开机启动”。

系统要求：Windows 10/11、.NET Framework 4.8，以及已安装并使用 ChatGPT 账户登录的 Codex CLI。程序会优先寻找 Codex 桌面版附带的 `codex.exe`，其次寻找 PATH 中的 `codex.exe`。

若暂时没有发布包，可从 [Actions](https://github.com/Levil-ww/CodexQuota/actions) 中成功的构建下载程序包（需要登录 GitHub），或按照下文自行构建。

## 使用

| 操作 | 效果 |
| --- | --- |
| 左键拖动 | 移动悬浮窗并保存位置 |
| 鼠标悬停 | 查看额度、重置时间、时区和刷新状态 |
| 右键 → 立即刷新 | 立即查询当前额度 |
| 右键 → 显示 / 隐藏悬浮窗 | 暂时收起或显示 |
| 右键 → 恢复顶部居中 | 将窗口移回主屏幕顶部 |
| 右键 → 开机启动 | 创建或删除当前用户的启动快捷方式 |
| 双击托盘图标 | 重新显示悬浮窗 |
| 右键 → 退出 | 结束程序并移除托盘图标 |

绿色圆点表示上次读取成功，灰色表示尚未读取，橙色表示最近读取失败。重置时间按 Windows 本地时区显示。

这是独立置顶窗口，手动放到头像上方后，不会自动跟随 GPT/Codex 窗口移动或最小化。

## 数据与隐私

程序通过标准输入/输出启动本机 `codex app-server`，完成 `initialize` / `initialized` 握手后调用 `account/rateLimits/read`。每次读取完成后关闭该连接，不开放额外网络监听端口。协议见 [官方 App Server 文档](https://learn.chatgpt.com/docs/app-server)。

- 优先使用 `rateLimitsByLimitId.codex`，没有多额度桶字段时回退到 `rateLimits`。
- 依据 `windowDurationMins` 识别 300 分钟与 10080 分钟窗口。
- 剩余比例由 `100 - usedPercent` 计算；重置时间来自 `resetsAt`。
- 不发起模型生成或聊天请求，不发送聊天内容，不直接读取、复制或记录登录令牌。
- Codex CLI 会使用自身登录信息连接额度服务；程序读到的是 **CLI 当前账户** 的额度，可能与桌面版切换后的账户不同。

位置保存在程序旁的 `settings.json`。开机启动使用当前用户启动目录下的 `Codex Quota.lnk`。

## 常见问题

**显示 CLI 未登录**

在自己的终端执行 `codex login`，完成 ChatGPT 账户登录后右键选择“立即刷新”。仅 API Key 登录可能无法提供订阅额度。

**找不到 Codex CLI**

检查是否安装 Codex；若安装路径特殊，可设置当前用户环境变量后重新启动悬浮窗：

```powershell
[Environment]::SetEnvironmentVariable('CODEX_QUOTA_CLI', 'C:\path\to\codex.exe', 'User')
```

**显示 `--` 或百分比后的 `*`**

`--` 代表该额度窗口未返回，不能理解为剩余 100%。`*` 表示保留的旧数据；悬停查看错误原因，检查网络或登录状态后再次刷新。

**移动文件夹或卸载**

先取消开机启动，再退出程序。移动后重新勾选；卸载则删除文件夹即可。若已提前删除程序，可按 `Win+R`，输入 `shell:startup`，删除 `Codex Quota.lnk`。

## 从源码构建

在 Windows PowerShell 5.1 或 PowerShell 7 中执行：

```powershell
git clone https://github.com/Levil-ww/CodexQuota.git
cd CodexQuota
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\dist\CodexQuota.exe
```

构建脚本使用 Windows 自带的 .NET Framework C# 编译器，不需要安装 .NET SDK 或还原 NuGet 包。脚本会编译程序并执行 9 项测试；任何编译或测试失败都会返回失败状态。

```powershell
# 生成分发包和 SHA-256 校验文件
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1

# 读取真实额度，需要 Codex CLI 已登录；等待进程退出再读取结果
$probe = Start-Process .\dist\CodexQuota.exe -ArgumentList '--probe' -Wait -PassThru
$probe.ExitCode
Get-Content .\dist\probe-result.txt
```

`--probe` 成功返回 0；两个额度窗口均未知返回 2；连接或解析失败返回 1。诊断文件包含账户额度，提交 Issue 前请自行决定是否分享。

在退出运行中的悬浮窗后，可用 `--smoke-test` 渲染演示数据到 `dist/preview.png` 并自动退出。自动化构建执行解析及快捷方式测试，不读取真实账户额度。

## 项目结构

```text
source/
  Program.cs          悬浮窗、托盘、位置保存和刷新调度
  CodexClient.cs      CLI 查找、协议握手、读取与超时处理
  QuotaCore.cs        额度解析与剩余百分比
  StartupShortcut.cs  开机启动快捷方式
  QuotaTests.cs       额度与快捷方式测试
  App.config          .NET Framework 运行时配置
docs/preview.png      演示预览
build.ps1             编译与测试
package.ps1           分发包与校验和
.github/workflows/    Windows 持续集成与标签发布
```

欢迎通过 [Issues](https://github.com/Levil-ww/CodexQuota/issues) 反馈问题或建议，通过 Pull Request 参与开发。提交前请阅读 [贡献指南](CONTRIBUTING.md)。版本记录见 [CHANGELOG](CHANGELOG.md)。

## 许可证

[MIT](LICENSE) © 2026 Levil-ww

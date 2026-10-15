# Codex / ChatGPT Taskbar Auto-hide Fix

修复一种 Windows 任务栏兼容性问题：**Codex 窗口最大化后，鼠标移到屏幕底边无法唤出自动隐藏的任务栏；还原窗口后又正常。**

这是社区提供的临时兼容工具，与 OpenAI、Microsoft、Electron 无官方关系。已编译程序和源码均在本仓库中，可直接下载，无需安装开发环境。

**[下载 v1.0.0 ZIP](https://github.com/louzc/codex-taskbar-autohide-fix/releases/download/v1.0.0/ChatGPTTaskbarFix-v1.0.0-win.zip)** · **[单独下载 EXE](https://github.com/louzc/codex-taskbar-autohide-fix/releases/latest/download/ChatGPTTaskbarFix.exe)** · **[所有版本](https://github.com/louzc/codex-taskbar-autohide-fix/releases)**

## 有没有其他人遇到？

有。以下均为原始项目中的公开记录，核对日期为 2026-10-15：

| 记录 | 与本问题的关系 |
| --- | --- |
| [OpenAI/Codex #45369](https://github.com/openai/codex/issues/45369) | 2026-09-14 提交，核对时仍开放；报告最大化时无法通过鼠标唤出任务栏，还原后正常。后续有其他用户报告 Windows 11 上的相同现象。 |
| [同一 issue 的技术讨论](https://github.com/openai/codex/issues/45369#issuecomment-6096740909) | 另一位报告者测试了 NonRudeHWND、MarkFullscreenWindow 及任务栏置顶的组合，观察到底部命中窗口恢复为 Explorer；其测试范围与本工具不同。 |
| [Electron #5990](https://github.com/electron/electron/issues/5990) | 2016 年的历史记录：前台、最大化的无边框窗口会阻止任务栏从底边弹出。该记录已关闭，不能据此认定当前版本仍有同一个 Electron 缺陷。 |
| [Windows Terminal #16476](https://github.com/microsoft/terminal/issues/16476) | 另一个应用中，最大化窗口表现得像全屏窗口，遮挡任务栏的底边触发。 |

这些记录说明类似现象并非个例，也不限于某一个应用。它们不代表所有案例都有相同根因。

## 适用范围

- **实测环境**：Windows 10 Pro 22H2，19045.7725；Codex 应用版本 26.1007.71326，Windows 包版本 26.1007.9446.0；单显示器、底部任务栏、125% 缩放。
- **当前识别目标**：默认 C:\Program Files\WindowsApps 下、发布者标识为 2p2nqsd0c76g0 的 OpenAI.Codex 安装包，其 app\ChatGPT.exe 进程创建的、可最大化的 Chrome_WidgetWin_1 顶层窗口。版本号不固定，因此同一路径形式的后续包更新也可匹配。
- 本机窗口显示为 ChatGPT，但实际来自 OpenAI.Codex 包。因此，这个项目名称及支持范围以 Codex 包为准。
- 独立版 ChatGPT、其他 Electron 软件、自定义安装路径目前不会被匹配。
- Windows 11、ARM64、多显示器及其他任务栏边缘尚未在本工具上实测；代码中的 ARM64 包路径匹配不代表已完成兼容验证。
- 需要 Windows 的 .NET Framework 4.8。程序使用 AnyCPU 构建，不需要 Node.js、Python 或网络连接。

## 使用

1. 下载并解压 ZIP，保留整个解压目录。
2. 双击 bin\ChatGPTTaskbarFix.exe。
3. 打开 Codex，最大化，再把鼠标移到屏幕底边停留约 1 秒。

**程序在后台运行，没有主窗口和托盘图标。** 它不会修改任务栏自动隐藏设置。若任务栏原本未开启自动隐藏，它不会帮你开启。

程序只处理符合上述范围的应用窗口；浏览器等其他窗口不会被添加标记。建议按普通用户权限运行。若目标应用以管理员身份运行，普通权限程序可能无法写入其窗口属性。

### 登录时自动启动

在解压目录打开 PowerShell，运行：

    & .\scripts\Install.ps1

安装脚本将程序放入当前用户的 %LOCALAPPDATA%\ChatGPTTaskbarFix，创建名为 ChatGPT Taskbar Compatibility 的登录启动快捷方式，并立即运行程序。安装后，下载目录可以移动或删除。

这与直接双击运行不同：直接运行不会自动创建登录启动项。

### 停止与卸载

仅停止后台程序：

    & .\bin\ChatGPTTaskbarFix.exe --stop

或停止已安装的程序：

    & "$env:LOCALAPPDATA\ChatGPTTaskbarFix\ChatGPTTaskbarFix.exe" --stop

卸载自动启动及安装文件：

    & .\scripts\Uninstall.ps1

正常停止时，程序会恢复它添加前的窗口属性。直接结束进程可能保留当前窗口上的临时标记，直到该窗口被销毁。当前 Windows 会话中只运行一个实例；启动第二个实例会直接退出。

## 工作原理

Windows Shell 可能把具有自定义边框的最大化窗口识别为全屏窗口，影响任务栏的层级或边缘唤出行为。

本工具对匹配的窗口调用：

1. SetProp(hwnd, "NonRudeHWND", TRUE)；
2. ITaskbarList2::MarkFullscreenWindow(hwnd, FALSE)。

微软文档说明，MarkFullscreenWindow(FALSE) 本身仍允许 Shell 自动判定全屏；NonRudeHWND 则用于阻止这种自动全屏分类。文档建议在显示窗口前设置属性，本工具还会为已经显示的匹配窗口应用标记；这一用法在上面的实测环境中验证有效。参见 [微软接口说明](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-itaskbarlist2-markfullscreenwindow)。

程序启动时扫描一次当前顶层窗口，随后使用前台切换与窗口显示事件处理目标窗口；一秒定时器用于退出信号检查和已销毁窗口记录清理。它不连续枚举全部窗口，也不修改应用安装文件、不注入 DLL、不重启 Explorer。

该标记会随窗口销毁而消失，所以后台程序需要在新的目标窗口出现时重新应用。它不会修改应用自身的最大化代码。

**已知限制**：工具会阻止匹配窗口被 Shell 识别为全屏，因此应用主动进入真正全屏时，任务栏相关行为也可能受影响。需要原生全屏行为时，可先停止工具。不同 Windows 版本可能需要不同处理；本程序没有实现讨论中额外的任务栏强制置顶操作。

## 本次验证结果

2026-10-15，在上述 Windows 10 环境中：

- 刷新任务栏自动隐藏状态后，最大化时的问题仍然存在。
- 应用上述两个调用后，用户实际确认鼠标移到底边能唤出任务栏。
- 自动程序的首次应用、正常停止后恢复原值、再次启动后重新应用均通过检查。
- 已确认登录启动快捷方式指向安装后的程序。
- 后台进程当时约占用 25 MB 私有内存；不同环境和运行状态下可能变化。

**没有实际重启 Windows 或 Codex 来验证登录及应用重启场景，也没有在其他设备上验证。** 登录启动配置已检查，不能等同于所有启动场景均已验证。

## 查看状态与隐私

本地文件位于 %LOCALAPPDATA%\ChatGPTTaskbarFix：

- status.json：最近一次应用结果，包括进程号、窗口句柄、目标程序路径、时间和 Shell 调用返回值；
- events.log：启动、应用、错误及正常停止记录；达到约 128 KiB 后会在下次写入时清空；
- installation.json：使用安装脚本时生成的安装位置、时间与程序 SHA-256。

这些文件只保存在本机，程序没有上传功能，也不读取聊天内容。路径等字段可能含本机信息，提交问题时可只提供必要片段并自行隐去个人目录。发布包未包含本机运行日志或状态文件。

## 校验与自行编译

下载的 EXE 可用 bin\SHA256SUMS.txt 或 Release 附件中的 SHA256SUMS.txt 校验：

    Get-FileHash .\bin\ChatGPTTaskbarFix.exe -Algorithm SHA256

本次发布的预编译 EXE SHA-256：

    a6d758009c54530da654f19d04265d246c0d5d609358ebf1f47489af3fd78ccf

程序未做代码签名。校验和用于核对发布文件的一致性，不是第三方安全审计结论。

自行编译：

    & .\scripts\Build.ps1

构建脚本调用 Windows .NET Framework 自带的 C# 编译器，没有 NuGet 包和下载步骤，结果写入 bin。编译器版本、构建时间等因素可能导致自行编译的文件哈希与预编译版本不同。

仓库结构：

    src/ChatGPTTaskbarFix.cs     源码
    bin/ChatGPTTaskbarFix.exe    已编译程序
    bin/SHA256SUMS.txt           程序校验和
    scripts/Build.ps1            本机构建
    scripts/Install.ps1          当前用户安装与登录启动
    scripts/Uninstall.ps1        卸载与恢复

## English summary

Small, unofficial Windows workaround for the auto-hidden taskbar failing to reveal while a Codex window is maximized. It sets NonRudeHWND and clears the Shell fullscreen marking for eligible OpenAI.Codex package windows. The packaged process is named ChatGPT.exe; standalone ChatGPT packages and arbitrary Electron apps are not supported. Source and a prebuilt executable are included. Tested on one Windows 10 system; Windows 11 and restart/sign-in scenarios remain unverified.

## License

[MIT](LICENSE)

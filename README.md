# 静音卫士 MuteGuardian

[![Release](https://img.shields.io/badge/release-v1.0.0-blue.svg)](https://github.com/0594/MuteGuardian/releases/latest)
[![Download](https://img.shields.io/badge/download-MuteGuardian.exe-2ea44f.svg?logo=windows&logoColor=white)](https://github.com/0594/MuteGuardian/releases/download/v1.0.0/MuteGuardian.exe)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-0078D6.svg?logo=windows&logoColor=white)](#已知边界)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.x-512BD4.svg?logo=dotnet&logoColor=white)](#从源码编译)

**直接下载：[MuteGuardian.exe（57 KB，免安装）](https://github.com/0594/MuteGuardian/releases/download/v1.0.0/MuteGuardian.exe)** ·
[全部版本](https://github.com/0594/MuteGuardian/releases)

Windows 常驻小工具：**满足离开条件时静音系统主音量，真正回到桌面后自动恢复原音量**。

它不是屏保，也不会替换系统已有的屏保——只在后台判断"人是否离开"，然后管音量。

- 单文件 exe（约 56 KB），基于 .NET Framework，用 Windows 自带的 `csc.exe` 编译，不需要安装 SDK 或运行时
- 只操作**默认播放设备的主音量与静音状态**，不碰其他系统设置，不联网
- 托盘常驻，三种图标区分状态：监控中 / 已静音 / 已暂停

> 程序内部标题、托盘名称、文件属性里显示的都是中文名「静音卫士」；仓库里的文件名使用 ASCII（`MuteGuardian`），这样 `build.bat` 在任何代码页下都能正常执行。

## 功能

| 静音生效条件（可勾选多个，任一满足即静音） | 说明 |
|---|---|
| 键鼠无动作 N 分钟 | 独立计时，1–180 分钟可调，默认不勾选 |
| 屏保启动时 | 开始时间由系统屏保设置决定 |
| 屏幕关闭时 | 显示器断电即静音；"变暗"不算关闭 |
| 锁屏时（Win+L） | 默认勾选 |

**恢复条件**（同时满足，且需连续稳定 1.5 秒）：

```
显示器点亮 + 屏保已退出 + 会话已解锁 + 检测到新的键鼠输入
```

几个刻意处理的细节：

- 屏保要求登录时，屏保退出到进入登录界面之间那几百毫秒不算"回来"，密码输完回到桌面才恢复，不会在登录界面突然出声
- 只是点亮屏幕（仍停留在锁屏）也不会恢复
- 静音期间手动调了音量或取消静音，恢复时以手动设置为准，不覆盖
- `最长静音 N 小时` 是**兜底**：只在"离开条件全部解除、但程序仍未恢复"的卡死状态下生效，正常锁屏过夜不会周期性放声
- 程序被强杀不会执行恢复，可用 `MuteGuardian.exe /u` 一键解除

## 使用

1. 编译得到 `MuteGuardian.exe`（或直接使用自己编译的产物），放到固定目录，例如 `C:\Tools\MuteGuardian\`
2. 双击运行 → 托盘出现蓝色喇叭图标，开始监控
3. 默认会写入开机自启（`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值名 `MuteGuard`），可在设置里取消
   - 程序换目录后，下次启动会自动把自启路径指向新位置，不留死键

托盘右键菜单：状态（含当前音量）、立即静音/恢复、暂停监控、设置、开机自动启动、打开日志、退出。

## 配置与日志

| 路径 | 内容 |
|---|---|
| `%APPDATA%\MuteGuard\config.ini` | 配置，UTF-8，手改后重启生效 |
| `%APPDATA%\MuteGuard\log.txt` | 日志，每次静音/恢复都记录触发原因，超过 1 MB 自动重建 |

日志示例：

```
20:02:47  mute: saved volume=54% muted=False -> mute, hr=0
20:02:47  === muted (idle=60s ss=False lock=True display=False) ===
20:03:10  restore: volume->54% muted->False, hr=0
20:03:10  === restore (条件满足) ===
```

`ss` = 屏保运行、`lock` = 会话锁定、`display` = 显示器点亮、`idle` = 空闲秒数，一眼能看出是哪个条件触发的。

## 命令行

```
MuteGuardian.exe               启动守护（托盘）
MuteGuardian.exe /settings     直接打开设置窗口
MuteGuardian.exe /test <log>   自检：静音 2 秒后恢复原状态，结果写日志
MuteGuardian.exe /q <log>      输出当前音量/静音状态
MuteGuardian.exe /u            兜底解除静音
MuteGuardian.exe /install      开启开机自启
MuteGuardian.exe /uninstall    关闭开机自启
```

## 从源码编译

依赖只有 Windows 自带的 .NET Framework 4.x 编译器：

```bat
build.bat
```

`build.bat` 会先用 `tools\IcoGen.cs` 生成多尺寸图标 `MuteGuardian.ico`，再编译出 `MuteGuardian.exe`。手动编译也可以：

```bat
csc.exe /codepage:65001 /target:winexe /platform:anycpu /optimize+ ^
  /win32icon:MuteGuardian.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /out:MuteGuardian.exe MuteGuardian.cs
```

`/codepage:65001` 必须保留：源码是 UTF-8，漏掉它中文界面会乱码。`/win32icon` 用于内嵌图标，可以省略。

## 实现要点

- 音量：Windows Core Audio `IAudioEndpointVolume`（COM），读写主音量标量 + 静音状态，恢复时精确还原到进入前的值
- 屏保检测：轮询 `SPI_GETSCREENSAVERRUNNING`，只对系统启动的屏保有效（手动运行 .scr 不会置位）
- 会话锁定：`WTSRegisterSessionNotification` 接收 `WM_WTSSESSION_CHANGE`（LOCK / UNLOCK / LOGON）
- 显示器开关：`RegisterPowerSettingNotification` 订阅 `GUID_CONSOLE_DISPLAY_STATE`
- 空闲计时：`GetLastInputInfo`
- 单实例：命名 Mutex；托盘用 WinForms `NotifyIcon`，图标在代码里绘制，不依赖资源文件

## 已验证场景（Windows 11）

- 音量链路：54% → 静音 → 恢复 54%，`/test` 自检多次通过
- 屏保条件：系统屏保启动后 1 秒内检测到并静音；屏保退出且有输入后恢复
- 空闲条件：达到阈值静音，键鼠动作后 1.5 秒恢复
- 关屏 + 锁屏：显示器关闭 → 锁屏 → 静音；仅点亮屏幕（仍在锁屏）保持静音；解锁后 2 秒内恢复
- 兜底：把阈值临时改到秒级验证，离开条件仍成立时兜底不触发，不会周期性放声

## 已知边界

- 只改默认播放设备的主音量；独占模式、ASIO 播放器、部分游戏的音频不受影响
- 本工具不提供锁屏或拦截输入的能力，只处理音量
- 若系统关闭了屏保（策略或电源设置），"屏保条件"自然失效，可用关屏/锁屏条件替代
- 混合 DPI 多显示器场景未专门适配，影响仅限托盘图标清晰度，与静音功能无关

## 目录结构

```
MuteGuardian.cs    主程序源码（C# / WinForms + P/Invoke）
build.bat          一键编译（先生成图标，再编译 exe）
tools/IcoGen.cs    图标生成器，输出 16/24/32/48/64/128/256 多尺寸 ICO
```

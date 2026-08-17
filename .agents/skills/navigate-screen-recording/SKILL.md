---
name: navigate-screen-recording
description: 定位并维护轻截的屏幕录制模块、录制协调器、帮助进程、音视频会话、实时标注与录制产物。用于修改录屏启动、暂停、编码、音频或完成流程时。
---

# 屏幕录制导航

## 工作流

1. 先读 `docs/project-memory.md` 的录屏、产物与模块约定，再读 `references/iteration-log.md`。
2. 区分模块交互、录制协调、底层会话、帮助进程和宿主能力，再按下表定位。
3. 修改前确认进程生命周期、取消/异常、实时标注、保存历史和打包文件联动。
4. 完整走通录制状态机后更新滚动日志。

## 文件导航

| 修改目标 | 首要入口 | 常见联动 |
| --- | --- | --- |
| 模块功能入口 | `ScreenshotTool.ScreenRecording/ScreenRecordingFeature.cs`、`ScreenRecordingModule.cs` | 模块契约、选区输入、功能释放 |
| 录制协调 | `ScreenshotTool.ScreenRecording/RecordingCoordinator.cs` | 控制会话、录制会话、完成回调 |
| 控制状态机 | `ScreenshotTool.ScreenRecording/RecordingControlSession.cs` | 暂停/继续/停止、控制 UI |
| 底层录制会话 | `ScreenshotTool.ScreenRecording/ScreenRecorderSession.cs` | 编码、音频、帧率、资源释放 |
| 选项与偏好 | `ScreenshotTool.ScreenRecording/ScreenRecordingOptions.cs`、`ScreenRecordingPreferences.cs`、`ScreenRecordingSettingsPage.cs` | 设置宿主、兼容默认值 |
| 目标与存储 | `ScreenshotTool.ScreenRecording/RecordingTarget.cs`、`RecordingStorage.cs` | MP4 命名、保存目录、历史与通知 |
| 捕获保护 | `ScreenshotTool.ScreenRecording/CaptureProtection.cs` | 控制窗、提示窗、录制区域 |
| 帮助进程 | `ScreenshotTool.ScreenRecording.Recorder/Program.cs` | 进程协议、部署资产、退出清理 |
| 实时标注宿主 | `ScreenshotTool/Presentation/LiveAnnotationSession.cs`、`ScreenshotTool.Contracts/ModuleContracts.cs` | 截图编辑标注、录制帧合成 |
| 宿主提示 UI | `ScreenshotTool/Presentation/*Recording*.cs` | 点击穿透、录制排除、窗口生命周期 |
| 使用文档 | `docs/screen-recording-addon.md` | 依赖、能力与限制 |

## 修改规则

- 模块仅依赖 `ScreenshotTool.Contracts`；录屏特有状态留在模块创建的功能/会话中，不塞进 `MainForm` 或 `CaptureOverlayForm`。
- `RecordingCoordinator` 协调状态，底层会话负责媒体资源，UI 只发命令和展示状态；避免交叉持有导致无法释放。
- 状态转换显式且幂等：开始、暂停、继续、停止、取消、故障和关闭均可安全重复处理。
- 编码、帧复制、音频读取不阻塞 UI/绘制回调；限制队列背压，优先避免长期内存增长和音画漂移。
- 帮助进程协议带超时、取消和异常退出处理；宿主退出或模块卸载时不得遗留进程、句柄和临时文件。
- 实时标注通过宿主契约获取；保持坐标一致，并确认控制框/提示层是否应排除在录制画面外。
- MP4 写入使用临时/最终文件边界，失败不发布半成品；保存路径与命名复用产物系统策略。

## 联动照护

- 改录制状态机：同步检查控制 UI、模块会话租约、异常清理、帮助进程和完成通知。
- 改编码/音频：同步检查设置默认值、设备缺失、暂停时间轴、文件可播放性和性能。
- 改选区/坐标：同步检查截图浮层、实时标注、捕获保护、多屏/DPI 与鼠标提示。
- 改输出路径/命名：同步检查保存系统、历史画廊、通知、目录打开和失败回滚。
- 改帮助进程资产或入口：同步检查项目引用、发布脚本、模块包结构和 `docs/project-memory.md`；仅在明确要求时打包。

## 导航完整性维护

- 执行任务时把新发现的核心入口、公共接口、管理器/服务、数据模型、重要调用方/订阅方、稳定脚本/资源和测试/运行入口列为导航候选。
- 新增文件只要属于本系统核心职责、会成为常用入口或扩展点、影响其他系统/被多处调用，或后续同类任务需要快速定位，且具有稳定价值，就更新本 Skill。
- 核心文件新增、删除、移动或重命名时，同一任务内更新“文件导航”和“联动照护”；移除失效路径，不保留误导信息。
- 不逐一记录普通内部实现、临时文件、生成产物或一次性脚本。
- 新系统、职责迁移或常见任务路由变化时，同时更新 `.agents/skills/navigate-cutcut-systems/SKILL.md`。
- 暂时无法确认归属时不要强行归类，写入 `.agents/skills/navigate-cutcut-systems/references/pending-boundaries.md`，待职责或调用证据充分后再处理。

## 验证

- 覆盖开始→停止、暂停→继续、取消、编码异常、音频设备缺失和宿主关闭状态序列。
- 打开真实 MP4 验证时长、尺寸、帧率、音频/视频可用，并确认无半成品或遗留帮助进程。
- 验证实时标注、控制 UI 排除、多屏坐标、模块热卸载和重复录制资源释放。
- 按根目录 `AGENTS.md` 完成格式、测试与 Release 构建；未明确要求时不打包。

## 迭代日志

- 日志位于 `references/iteration-log.md`；格式为 `日期 | 任务 | 触及文件 | 导航缺口/改进`。
- 末尾追加新记录；超过 10 条时删除最旧记录，始终最多 10 条。
- 如果路径、协议或联动规则变更，先更新本 Skill，再记录任务。

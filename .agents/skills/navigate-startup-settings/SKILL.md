---
name: navigate-startup-settings
description: 定位并维护轻截的启动、偏好设置、全局快捷键、软件更新页及其持久化链路。用于修改开机启动、主界面启动行为、截图快捷键、设置读写、设置页面或应用内更新时。
---

# 启动、设置与快捷键导航

## 工作流

1. 先读 `docs/project-memory.md` 中“启动行为”“设置页”“全局快捷键”相关内容，再读 `references/iteration-log.md`。
2. 按下表直接打开入口文件；只有地图失效时才做局部搜索，并在本次任务结束前修正本 Skill。
3. 修改前列出受影响的联动系统，完成后逐项照护并验证。
4. 成功交付后按“迭代日志”规则记录本次使用。

## 文件导航

| 修改目标 | 首要入口 | 常见联动 |
| --- | --- | --- |
| 设置模型与默认值 | `ScreenshotTool/Core/AppSettings.cs`、`UserPreferences.cs`、`UserSettingsDocument.cs` | `Infrastructure/JsonSettingsStore.cs`、设置页、兼容迁移测试 |
| 截图快捷键 | `ScreenshotTool/Core/HotkeyDefinition.cs`、`HotkeyBindings.cs` | `Presentation/HotkeyInputBox.cs`、`Infrastructure/GlobalHotkeyService.cs`、`Presentation/MainForm.cs` |
| 设置页布局 | `ScreenshotTool/Presentation/Pages/*SettingsPage.cs` | `Application/CompositionRoot.cs`、`MainForm.cs`、UI 预览测试 |
| 启动到主界面/托盘 | `ScreenshotTool/Core/StartupWorkspacePolicy.cs`、`Application/StartupWorkspaceService.cs` | `Program.cs`、`MainForm.cs`、托盘与截图入口 |
| 开机启动 | `Application/StartupRegistrationService.cs`、`StartupRegistrationPreferenceService.cs` | `Infrastructure/WindowsRunStartupEntryStore.cs`、通用设置页 |
| 软件内更新 | `Abstractions/IApplicationUpdateService.cs`、`Infrastructure/GitHubReleaseApplicationUpdateService.cs`、`Presentation/Pages/ApplicationUpdatePage.cs` | `CompositionRoot.cs`、发布资产契约、更新页 UI 预览 |
| 抽象边界 | `ScreenshotTool/Abstractions/ISettingsStore.cs`、`IGlobalHotkeyService.cs`、`IStartupRegistrationService.cs` | `Application/CompositionRoot.cs` |

## 修改规则

- 设置值与纯策略放在 `Core`，流程放在 `Application`，系统调用放在 `Infrastructure`，控件交互放在 `Presentation`；`CompositionRoot` 只连接和释放对象。
- 不让页面直接读写注册表、JSON 或全局热键 API；通过最小接口和应用服务协作。
- 设置模型新增字段必须有稳定默认值，并兼容旧 JSON 缺字段、空值和非法值。
- 截图快捷键保持 0 到 3 组语义；清空是合法状态。注册多组快捷键时要原子更新或明确回滚，避免 UI 显示成功但实际未注册。
- 快捷键解析应支持真实按键组合，不用字符大小写推断修饰键；保留系统组合不得被模块无条件吞掉。
- 设置分页保持纵向单列，一行一个设置项；条目增多时使用滚动区域。
- 热路径避免重复磁盘写入和反复注册；仅在值实际变化时持久化或刷新系统状态。

## 联动照护

- 改 `AppSettings` 或序列化结构：同步检查所有设置页、`JsonSettingsStore`、默认值测试和 `docs/project-memory.md`。
- 改快捷键：同步检查输入控件、冲突校验、全局注册/注销、主窗体触发、启动加载以及多绑定测试。
- 改启动行为：同步检查命令行启动、托盘、开机启动、首次启动和窗口关闭策略。
- 改页面入口或导航：同步检查 `CompositionRoot` 注册、`MainForm` 页面切换与 UI 预览。
- 改软件更新：同步检查 Release 资产名/摘要、运行库选包、模块保留策略、更新脚本安全边界和发布约定。

## 导航完整性维护

- 执行任务时把新发现的核心入口、公共接口、管理器/服务、数据模型、重要调用方/订阅方、稳定脚本/资源和测试/运行入口列为导航候选。
- 新增文件只要属于本系统核心职责、会成为常用入口或扩展点、影响其他系统/被多处调用，或后续同类任务需要快速定位，且具有稳定价值，就更新本 Skill。
- 核心文件新增、删除、移动或重命名时，同一任务内更新“文件导航”和“联动照护”；移除失效路径，不保留误导信息。
- 不逐一记录普通内部实现、临时文件、生成产物或一次性脚本。
- 新系统、职责迁移或常见任务路由变化时，同时更新 `.agents/skills/navigate-cutcut-systems/SKILL.md`。
- 暂时无法确认归属时不要强行归类，写入 `.agents/skills/navigate-cutcut-systems/references/pending-boundaries.md`，待职责或调用证据充分后再处理。

## 验证

- 运行覆盖设置兼容、快捷键解析/冲突/多绑定与启动策略的逻辑测试。
- 涉及页面时运行相应 UI 预览或人工检查纵向布局、删除/清空按钮和滚动行为。
- 交付前遵循根目录 `AGENTS.md` 的格式、测试与 Release 构建要求；不要因此自行打包。

## 迭代日志

- 日志文件为 `references/iteration-log.md`，仅记录真正使用本 Skill 完成的任务。
- 每条一行：`日期 | 任务 | 触及文件 | 导航缺口/改进`，内容保持简短。
- 新记录追加在末尾；追加后超过 10 条时删除最顶部的最旧记录，始终最多保留 10 条。
- 若本次发现入口、规则或联动表过时，先更新本 Skill，再写日志。

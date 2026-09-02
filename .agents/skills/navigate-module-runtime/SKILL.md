---
name: navigate-module-runtime
description: 定位并维护轻截的模块契约、发现加载、热更新、租约卸载、模块管理页及模块资源边界。用于新增模块能力或修复插件加载、替换、删除问题时。
---

# 模块运行时与热加载导航

## 工作流

1. 先读 `docs/project-memory.md` 的模块架构与热加载约定，再读 `references/iteration-log.md`。
2. 先判断修改属于公共契约、宿主生命周期、模块实现还是管理 UI，再按下表进入。
3. 改动前列出所有模块消费者；契约变更必须检查每个实现和宿主适配器。
4. 验证加载、替换、删除和活动会话延迟释放后，更新滚动日志。

## 文件导航

| 修改目标 | 首要入口 | 常见联动 |
| --- | --- | --- |
| 稳定公共契约 | `ScreenshotTool.Contracts/ModuleContracts.cs` | 所有 `ScreenshotTool.*` 模块、宿主适配器、测试模块 |
| 模块发现/加载/卸载 | `ScreenshotTool/Infrastructure/Modules/ModuleHost.cs` | `ModuleLoadContext.cs`、文件监听、租约、激活偏好 |
| 程序集加载上下文 | `ScreenshotTool/Infrastructure/Modules/ModuleLoadContext.cs` | 私有依赖解析、流式加载、可回收性 |
| 图片宿主代理 | `ScreenshotTool/Infrastructure/Modules/ModuleImageHostProxy.cs` | `IModuleImageStorageHost`、自定义目录保存、图片所有权、异常隔离 |
| 激活偏好 | `ScreenshotTool/Abstractions/IModuleActivationPreferenceStore.cs`、`Infrastructure/Modules/UserPreferenceModuleActivationStore.cs` | 设置存储、模块管理页 |
| 宿主接口与模型 | `ScreenshotTool/Abstractions/IModuleManager.cs`、`ModuleInfo.cs` | `Application/CompositionRoot.cs`、页面刷新 |
| 会话租约 | `ScreenshotTool/Presentation/CaptureFeatureSession.cs` | `CaptureOverlayForm.cs`、功能 Dispose、延迟卸载 |
| 模块管理与配置 UI | `ScreenshotTool/Presentation/Pages/ModuleManagementPage.cs`、`Presentation/ModuleConfigurationForm.cs` | 按模块包创建设置页租约、启停、错误展示、窗口释放 |
| 截图收藏夹模块 | `ScreenshotTool.Favorites/FavoritesModule.cs`、`FavoritesSettingsPage.cs` | 公共字符串偏好、最终位图、自定义目录异步保存、独立恢复包 |
| 组合入口 | `ScreenshotTool/Application/CompositionRoot.cs` | 启动/关闭顺序、服务释放 |

## 修改规则

- `ScreenshotTool.Contracts` 是唯一稳定边界；模块不得引用主程序程序集、`Presentation`、`Infrastructure` 或内部标注类型。
- 依赖方向保持 `Contracts <- Module`；需要宿主能力时先设计最小、通用、只读优先的契约。
- 模块 ID 与功能 ID 稳定且全局唯一；更新现有模块不得更换 ID。
- 使用可回收 `AssemblyLoadContext` 和流式程序集加载，避免长期锁住 `Modules` 中的 DLL。
- DLL 替换或删除后立即从目录视图移除；活动会话持有租约，全部功能实例释放后才 `Dispose` 模块并卸载上下文。
- 模块必须释放图片、字体、句柄、计时器、线程和事件订阅；静态引用与后台线程会阻止卸载。
- 模块异常隔离到当前模块/功能；文件监听需去抖并能处理复制过程中的短暂不完整文件。
- `CompositionRoot` 只连接、启动与释放对象，不放加载策略或业务规则。

## 联动照护

- 改契约：同步检查所有模块项目、测试模块、宿主代理、版本兼容和打包内容。
- 改加载/卸载：同步检查管理页刷新、激活偏好、活动截图会话、资源释放及 DLL 替换/删除。
- 改设置页扩展：同步检查模块设置契约、每个模块卡片的“管理配置”、独立窗口租约释放和纵向单列布局。
- 改图片/文本结果宿主：同步检查保存系统、OCR/二维码结果窗体、所有权与取消语义。
- 改模块目录或资产命名：同步检查发布脚本、插件文档与 `docs/project-memory.md`；只有明确要求才打包。

## 导航完整性维护

- 执行任务时把新发现的核心入口、公共接口、管理器/服务、数据模型、重要调用方/订阅方、稳定脚本/资源和测试/运行入口列为导航候选。
- 新增文件只要属于本系统核心职责、会成为常用入口或扩展点、影响其他系统/被多处调用，或后续同类任务需要快速定位，且具有稳定价值，就更新本 Skill。
- 核心文件新增、删除、移动或重命名时，同一任务内更新“文件导航”和“联动照护”；移除失效路径，不保留误导信息。
- 不逐一记录普通内部实现、临时文件、生成产物或一次性脚本。
- 新系统、职责迁移或常见任务路由变化时，同时更新 `.agents/skills/navigate-cutcut-systems/SKILL.md`。
- 暂时无法确认归属时不要强行归类，写入 `.agents/skills/navigate-cutcut-systems/references/pending-boundaries.md`，待职责或调用证据充分后再处理。

## 验证

- 至少验证发现与加载、功能实例创建、核心输入/渲染、删除后的目录卸载，以及活动会话延迟释放。
- 用弱引用/GC 或等价检查确认可回收上下文最终卸载，并验证依赖 DLL 不被文件锁占用。
- 模拟损坏 DLL、构造异常、运行异常和重复文件事件，确认宿主继续工作。
- 遵循根目录 `AGENTS.md` 的格式、测试与 Release 构建要求；未明确要求时不发布。

## 迭代日志

- 日志位于 `references/iteration-log.md`；格式为 `日期 | 任务 | 触及文件 | 导航缺口/改进`。
- 新条目追加到末尾；超过 10 条时删除最旧条目，只保留最近 10 条。
- 路径、生命周期或联动规则变化时，先修订本 Skill，再写日志。

---
name: navigate-saved-artifacts
description: 定位并维护轻截的图片保存、文件命名、路径迁移、历史画廊、剪贴板与保存通知链路。用于修改截图落盘、复制、历史记录或文件定位时。
---

# 保存产物与历史导航

## 工作流

1. 先读 `docs/project-memory.md` 中保存目录、命名、历史与通知约定，再读 `references/iteration-log.md`。
2. 按表打开直接入口，先确认修改的是纯策略、文件 IO 还是界面行为。
3. 检查截图图片与录屏视频两类产物的联动差异。
4. 完成验证后更新本 Skill 的滚动日志。

## 文件导航

| 修改目标 | 首要入口 | 常见联动 |
| --- | --- | --- |
| 图片格式与保存 | `ScreenshotTool/Core/ScreenshotImageFormat.cs`、`ScreenshotTool/Abstractions/IImageSaveService.cs`、`ScreenshotTool/Infrastructure/ImageSaveService.cs` | 截图导出、贴图、收藏夹、剪贴板、通知 |
| 模块自定义目录保存 | `ScreenshotTool.Contracts/ModuleContracts.cs` 的 `IModuleImageStorageHost`、`ScreenshotTool/Infrastructure/Modules/ModuleImageHostProxy.cs` | `ScreenshotTool.Favorites/FavoritesModule.cs`、图片内文字命名、异步写盘、模块图片所有权 |
| 已保存截图查询/删除 | `ScreenshotTool/Abstractions/ISavedScreenshotService.cs`、`Infrastructure/SavedScreenshotService.cs` | `Presentation/Pages/ScreenshotGalleryPage.cs`、`ScreenshotGalleryQuery.cs` |
| 文件命名 | `ScreenshotTool/Core/ScreenshotFileNamePolicy.cs`、`ScreenshotFileNameMode.cs` | 设置模型、碰撞处理、测试 |
| 保存目录迁移 | `ScreenshotTool/Core/ScreenshotFolderMigration.cs` | `SavePathSettingsPage.cs`、设置持久化、历史刷新 |
| 保存路径设置 | `ScreenshotTool/Presentation/Pages/SavePathSettingsPage.cs` | `AppSettings.cs`、`JsonSettingsStore.cs` |
| 按日期解析截图与录屏子目录 | `ScreenshotTool/Core/ArtifactOutputFolderPolicy.cs` | `ScreenshotTool/Infrastructure/ImageSaveService.cs`、`CaptureOverlayForm.cs`、录屏产物、保存路径设置、历史画廊 |
| 剪贴板与资源定位 | `ScreenshotTool/Abstractions/IClipboardService.cs`、`IFileLocationService.cs` | `WindowsClipboardService.cs`、`ExplorerFileLocationService.cs` |
| 保存通知 | `ScreenshotTool/Presentation/SavedArtifactNotificationForm.cs` | 主窗体、截图/录屏完成事件、目录打开 |

## 修改规则

- 命名、筛选、迁移决策保持为 `Core` 中的纯逻辑；磁盘、回收站、Explorer 和剪贴板调用放 `Infrastructure`。
- UI 通过接口访问文件，不持有可变全局列表，不在绘制/滚动事件中同步扫描整个目录。
- PNG/JPEG 编码、磁盘写入和剪贴板竞争重试不得占用 WinForms UI 线程；JPEG 必须显式处理透明像素，图库递归扫描与缩略图解码使用可取消后台快照，界面只应用最新完成结果。
- 保存采用明确所有权：编码完成后及时释放流和位图；读取历史缩略图不得长期锁住源文件。
- 文件名必须处理非法字符、重名、时钟精度和用户格式，避免静默覆盖已有产物。
- 删除优先保持可恢复语义；任何永久删除都必须由明确需求授权。
- 大目录查询分页/增量加载并可取消；缩略图缓存有边界，路径比较遵循 Windows 语义。
- 设置页保持纵向单列；路径变更与实际迁移分开建模，失败不得丢失原目录数据。

## 联动照护

- 改导出格式或命名：同步检查剪贴板、画廊过滤、通知、OCR 输入与文档约定。
- 改保存路径：同步检查迁移策略、设置持久化、历史刷新、目录打开和权限失败回滚。
- 改历史查询/删除：同步检查录屏 MP4 是否共用入口、回收站行为、缩略图缓存和空目录状态。
- 改通知：同步检查截图与录屏完成路径、主窗体生命周期和点击后文件是否仍存在。
- 改 `IModuleImageHost` 或 `IModuleImageStorageHost` 保存能力：同步检查模块契约、自定义目录归一化、异步位图所有权与热卸载。

## 导航完整性维护

- 执行任务时把新发现的核心入口、公共接口、管理器/服务、数据模型、重要调用方/订阅方、稳定脚本/资源和测试/运行入口列为导航候选。
- 新增文件只要属于本系统核心职责、会成为常用入口或扩展点、影响其他系统/被多处调用，或后续同类任务需要快速定位，且具有稳定价值，就更新本 Skill。
- 核心文件新增、删除、移动或重命名时，同一任务内更新“文件导航”和“联动照护”；移除失效路径，不保留误导信息。
- 不逐一记录普通内部实现、临时文件、生成产物或一次性脚本。
- 新系统、职责迁移或常见任务路由变化时，同时更新 `.agents/skills/navigate-cutcut-systems/SKILL.md`。
- 暂时无法确认归属时不要强行归类，写入 `.agents/skills/navigate-cutcut-systems/references/pending-boundaries.md`，待职责或调用证据充分后再处理。

## 验证

- 覆盖命名冲突、非法格式、目录不存在/无权限、迁移失败与大目录查询测试。
- 检查保存后的真实文件可解码、没有遗留文件锁，历史删除符合可恢复约定。
- 涉及界面时验证单列布局、空状态、分页/滚动和通知点击行为。
- 按根目录 `AGENTS.md` 完成格式、测试与 Release 构建；用户未要求时不创建发布包。

## 迭代日志

- 日志位于 `references/iteration-log.md`；格式为 `日期 | 任务 | 触及文件 | 导航缺口/改进`。
- 追加后如果超过 10 条，删除顶部最旧记录；只保留最近 10 条。
- 若入口或联动发生变化，先更新本 Skill，再记录任务。

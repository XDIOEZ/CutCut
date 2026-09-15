---
name: navigate-capture-editor
description: 定位并维护轻截的区域截图、选区交互、标注编辑、预览渲染与最终导出链路。用于修改截图浮层、编辑工具、几何算法或导出像素时。
---

# 截图与编辑器导航

## 工作流

1. 先读 `docs/project-memory.md` 中截图、编辑和坐标约定，再读 `references/iteration-log.md`。
2. 从下表进入最小修改面；地图失效时只做局部搜索，并回写本 Skill。
3. 修改前确认 Preview 与 Export、宿主与模块、交互与位图输出的边界。
4. 完成联动照护和验证后更新滚动日志。

## 文件导航

| 修改目标 | 首要入口 | 常见联动 |
| --- | --- | --- |
| 截图浮层与输入 | `ScreenshotTool/Presentation/CaptureOverlayForm.cs`、`CaptureOverlayPresenter.cs`、`CaptureOverlayInteractionLayout.cs`、`CaptureBackgroundRefreshPolicy.cs` | `CaptureBackgroundRefreshHotkeyRegistration.cs`、`LiveAnnotationPointerHook.cs`、`CaptureFeatureSession.cs`、选区/工具栏辅助类、模块功能 |
| 编辑工具栏 | `ScreenshotTool/Presentation/CaptureEditorToolbar.cs` | `Editing/CaptureAnnotationEditor.cs`、设置模型 |
| 标注模型 | `ScreenshotTool/Editing/AnnotationDocument.cs`、`Annotations.cs` | 命中测试、几何、缩放、旋转、对齐策略 |
| 编辑元素复制粘贴 | `ScreenshotTool/Editing/AnnotationClipboard.cs`、`CaptureAnnotationEditor.cs` | `Annotations.cs` 独立副本、`CaptureOverlayForm.cs` 快捷键、通用剪贴板自定义数据；同会话保留对象，外部使用位图 |
| 文本编辑 | `ScreenshotTool/Presentation/TransparentTextEditorControl.cs` | 文本标注、导出渲染、字体资源释放 |
| 实时标注 | `ScreenshotTool/Presentation/LiveAnnotationSession.cs` | 屏幕录制、`ScreenshotTool.Contracts/ModuleContracts.cs` |
| 屏幕采集 | `ScreenshotTool/Infrastructure/ScreenCaptureService.cs`、`Core/DesktopSnapshot.cs`、`Presentation/CaptureBackgroundLayer.cs` | 虚拟桌面坐标、DPI、多屏、覆盖层停放与实时选区采集 |
| 模块截图能力 | `ScreenshotTool/Presentation/CaptureFeatureSession.cs` | `ScreenshotTool.Contracts/ModuleContracts.cs`、模块运行时 |
| 收藏最终截图 | `ScreenshotTool.Favorites/FavoritesModule.cs` | `CaptureOverlayForm.cs` 的 Export 渲染、可见文字命名元数据、字符串偏好快照、保存产物系统 |
| 编辑策略与系数 | `ScreenshotTool/Core/AnnotationLayoutOptions.cs`、`DrawingToolCoefficients.cs` | 编辑设置页、用户偏好 |
| 贴图创建与重新编辑 | `ScreenshotTool.PinnedImage/PinnedImageModule.cs`、`PinnedImageForm.cs`、`PinnedImageWindowLayout.cs` | 模块运行时、剪贴板/保存、`ExistingImageEditLayout.cs` |
| 贴图原图文字选择 | `ScreenshotTool.PinnedImage/PinnedImageTextController.cs`、`ImageTextSelection.cs` | OCR 位置契约、蓝色预览高亮、阅读顺序选择、Alt 移动/缩放、文字剪贴板 |

## 修改规则

- 几何、命中测试、状态机和可测试策略优先放 `Editing`/`Core`；窗体负责输入转发和呈现，不继续堆业务状态。
- 所有预览与导出使用同一套虚拟桌面客户区坐标；显式处理 DPI、负坐标和多屏边界。
- 交互提示只在 `Preview` 绘制；最终内容必须在 `Export` 绘制。改变输出必须验证导出位图，而非只看屏幕。
- 通过 `ICaptureFeatureHost` 和最小契约提供宿主能力，不让模块引用主程序、窗体或内部标注类型。
- 输入返回 `true` 仅表示确实消费事件；不得无条件截获系统保留快捷键。
- 绘制回调不做阻塞 IO、网络请求或昂贵全图重算；缓存可复用资源并准确释放图片、字体、画刷和句柄。
- 最终位图在 UI 线程完成 Export 渲染后，把编码、写盘和剪贴板竞争重试交给后台；完成前由截图会话唯一持有位图并阻止重复保存。
- 新工具优先实现可组合策略/服务，保持 `CaptureOverlayForm` 和 `CaptureAnnotationEditor` 的职责边界。

## 联动照护

- 改坐标或选区：同步检查截图采集、标注几何、模块功能、长截图替换图像和多屏导出。
- 改标注模型/渲染：同步检查实时标注、屏幕录制、序列化/撤销、Preview 与 Export。
- 改输入：同步检查全局快捷键、模块输入消费、文本编辑焦点和系统保留键。
- 改最终位图：同步检查普通保存、收藏夹、剪贴板、OCR/二维码输入源、历史记录与通知。
- 改公共宿主能力：同步检查 `ScreenshotTool.Contracts`、所有模块实现、生命周期和兼容性测试。
- 改贴图：同步检查 `docs/pinned-image-addon.md`、`scripts/Publish-PinnedImageModule.ps1`、模块卸载关闭窗口、复制/保存和重新编辑。

## 导航完整性维护

- 执行任务时把新发现的核心入口、公共接口、管理器/服务、数据模型、重要调用方/订阅方、稳定脚本/资源和测试/运行入口列为导航候选。
- 新增文件只要属于本系统核心职责、会成为常用入口或扩展点、影响其他系统/被多处调用，或后续同类任务需要快速定位，且具有稳定价值，就更新本 Skill。
- 核心文件新增、删除、移动或重命名时，同一任务内更新“文件导航”和“联动照护”；移除失效路径，不保留误导信息。
- 不逐一记录普通内部实现、临时文件、生成产物或一次性脚本。
- 新系统、职责迁移或常见任务路由变化时，同时更新 `.agents/skills/navigate-cutcut-systems/SKILL.md`。
- 暂时无法确认归属时不要强行归类，写入 `.agents/skills/navigate-cutcut-systems/references/pending-boundaries.md`，待职责或调用证据充分后再处理。

## 验证

- 运行几何、命中、撤销/重做、输入和渲染相关逻辑测试。
- 对输出变化做确定性像素或位图断言，并覆盖 Preview/Export 差异。
- 涉及交互时运行截图 UI 预览，覆盖多屏、负坐标、取消和资源释放。
- 非模态截图浮层生命周期变更运行 `ScreenshotTool.UiPreview --capture-dispose-smoke`，覆盖窗口关闭后外层作用域再次释放。
- 遵循根目录 `AGENTS.md` 的格式、测试与 Release 构建要求；未明确要求时不打包。

## 迭代日志

- 日志位于 `references/iteration-log.md`；每条格式为 `日期 | 任务 | 触及文件 | 导航缺口/改进`。
- 新记录追加在末尾；超过 10 条立即删除最旧记录，始终只保留最近 10 条。
- 发现导航或规则失准时，同一任务内先修正本 Skill，再追加日志。

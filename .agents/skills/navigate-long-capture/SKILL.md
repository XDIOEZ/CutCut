---
name: navigate-long-capture
description: 定位并维护轻截的长截图采样、帧匹配、双向拼接、手动拼接、选区预览与长截图设置。用于修改滚动捕获、拼接算法或长截图交互时。
---

# 长截图导航

## 工作流

1. 先读 `docs/project-memory.md` 的长截图与模块约定，再读 `references/iteration-log.md`。
2. 先判断问题属于采样、匹配、拼接状态机还是 UI，再从表中进入最小模块。
3. 修改前确认方向、坐标、图像所有权、截图编辑器回填与热卸载联动。
4. 用确定性帧序列验证后更新滚动日志。

## 文件导航

| 修改目标 | 首要入口 | 常见联动 |
| --- | --- | --- |
| 功能入口 | `ScreenshotTool.LongCapture/LongCaptureFeature.cs`、`LongCaptureModule.cs` | 模块契约、会话释放、设置页 |
| 自动长截图流程 | `ScreenshotTool.LongCapture/LongCaptureEngine.cs` | 稳定采样、匹配器、拼接会话 |
| 稳定帧采样 | `ScreenshotTool.LongCapture/StableFrameSampler.cs` | 捕获频率、抖动容忍、取消 |
| 垂直帧匹配 | `ScreenshotTool.LongCapture/VerticalFrameMatcher.cs`、`BidirectionalVerticalFrameMatcher.cs` | 重叠区域、方向判断、性能 |
| 双向拼接 | `ScreenshotTool.LongCapture/BidirectionalLongCaptureStitchSession.cs` | 上/下滚动、画布增长、内存 |
| 手动模式 | `ScreenshotTool.LongCapture/ManualLongCapture*.cs` | 选帧、拼接预览、确认/取消 |
| 预览与选区 | `ScreenshotTool.LongCapture/LongCapturePreviewForm.cs`、`LongCaptureSelectionFrameForm.cs` | 虚拟桌面坐标、编辑器回填 |
| 设置与偏好 | `ScreenshotTool.LongCapture/LongCaptureOptions.cs`、`LongCapturePreferences.cs`、`LongCaptureSettingsPage.cs` | 模块设置宿主、持久化 |
| 宿主回填 | `ScreenshotTool/Presentation/LongCaptureEditorFrameLayout.cs` | `CaptureOverlayForm.cs`、编辑器坐标 |
| 测试 | `tests/ScreenshotTool.LogicTests/*LongCapture*.cs` | 算法回归、像素断言 |

## 修改规则

- 匹配、采样和拼接算法保持纯逻辑、可注入参数、可用固定帧测试；窗体只负责交互和呈现。
- 模块只依赖 `ScreenshotTool.Contracts`，不引用宿主窗体或内部标注类型。
- 所有区域使用统一虚拟桌面客户区坐标，明确区分屏幕坐标、选区局部坐标和拼接画布坐标。
- 热路径优先性能：避免每帧全图分配、重复像素转换和无界历史缓存；限制画布/缓存尺寸并及时释放旧帧。
- 不在绘制回调阻塞采集或匹配；异步流程支持取消，关闭预览或卸载模块后不再回调 UI。
- 图片所有权明确，替换编辑器底图后旧位图只由拥有者释放，避免双重释放或泄漏。
- 向上/向下、自动/手动使用共享核心而非复制算法分支；阈值集中到选项模型。

## 联动照护

- 改匹配算法：同步检查双向拼接、手动模式、稳定采样和四组长截图逻辑测试。
- 改坐标/选区：同步检查预览窗、选择框、`LongCaptureEditorFrameLayout`、截图编辑和多屏/DPI。
- 改图片生命周期：同步检查模块租约、预览关闭、编辑器底图替换和热卸载。
- 改设置：同步检查模块设置页、默认值/兼容读取、性能上限和文档。
- 改功能契约：同步检查模块运行时、截图会话、其他模块实现和最终导出。

## 导航完整性维护

- 执行任务时把新发现的核心入口、公共接口、管理器/服务、数据模型、重要调用方/订阅方、稳定脚本/资源和测试/运行入口列为导航候选。
- 新增文件只要属于本系统核心职责、会成为常用入口或扩展点、影响其他系统/被多处调用，或后续同类任务需要快速定位，且具有稳定价值，就更新本 Skill。
- 核心文件新增、删除、移动或重命名时，同一任务内更新“文件导航”和“联动照护”；移除失效路径，不保留误导信息。
- 不逐一记录普通内部实现、临时文件、生成产物或一次性脚本。
- 新系统、职责迁移或常见任务路由变化时，同时更新 `.agents/skills/navigate-cutcut-systems/SKILL.md`。
- 暂时无法确认归属时不要强行归类，写入 `.agents/skills/navigate-cutcut-systems/references/pending-boundaries.md`，待职责或调用证据充分后再处理。

## 验证

- 用固定帧覆盖向下、向上、无重叠、重复纹理、静止帧、轻微动画和停止条件。
- 对拼接结果做尺寸和关键像素断言，同时验证取消、预览关闭、内存上限与资源释放。
- 涉及宿主回填时验证继续标注及最终 Export，不只验证预览。
- 遵循根目录 `AGENTS.md` 的格式、测试与 Release 构建要求；未明确要求时不打包。

## 迭代日志

- 日志位于 `references/iteration-log.md`；格式为 `日期 | 任务 | 触及文件 | 导航缺口/改进`。
- 新记录放末尾；超过 10 条时移除最顶部最旧记录，只保留最近 10 条。
- 本次若发现算法入口或联动关系变化，先更新本 Skill，再写日志。

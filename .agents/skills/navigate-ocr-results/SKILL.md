---
name: navigate-ocr-results
description: 定位并维护轻截的 OCR、PaddleOCR、二维码识别、文本结果展示与翻译链路。用于修改识别模块、模型资源、识别进度、结果窗口或翻译行为时。
---

# OCR、二维码与文本结果导航

## 工作流

1. 先读 `docs/project-memory.md` 的 OCR/模块约定，再读 `references/iteration-log.md`。
2. 根据识别引擎、宿主结果展示或翻译职责选择入口，不跨边界直接调用窗体。
3. 修改前确认输入图像来源、取消/进度、模型资源和模块热卸载联动。
4. 完成识别与结果链路验证后更新滚动日志。

## 文件导航

| 修改目标 | 首要入口 | 常见联动 |
| --- | --- | --- |
| Windows OCR 模块 | `ScreenshotTool.Ocr/` | 模块契约、文本结果宿主、模块打包 |
| PaddleOCR 公共实现 | `ScreenshotTool.PaddleOcr/` | Tiny/Small 模型模块、原生/模型资源释放 |
| PaddleOCR Tiny | `ScreenshotTool.PaddleOcr.Tiny/` | 公共 Paddle 实现、模块 ID、发布资产 |
| PaddleOCR Small | `ScreenshotTool.PaddleOcr.Small/` | 公共 Paddle 实现、模型下载体积、发布资产 |
| 二维码识别 | `ScreenshotTool.QrCode/` | 图片来源、文本结果或命令宿主 |
| 模块结果契约 | `ScreenshotTool.Contracts/ModuleContracts.cs` | `ICaptureFeatureHost`、文本/命令/进度接口 |
| 文本结果 UI | `ScreenshotTool/Presentation/CaptureTextResultForm.cs` | 复制、翻译、窗口生命周期 |
| 翻译抽象/实现 | `ScreenshotTool/Abstractions/ITextTranslationService.cs`、`Infrastructure/MyMemoryTextTranslationService.cs` | 网络取消、错误提示、结果窗体 |
| 使用文档 | `docs/ocr-addon.md`、`docs/paddle-ocr-addon.md`、`docs/qr-code-addon.md` | 模块目录、依赖与限制 |

## 修改规则

- 识别模块只依赖 `ScreenshotTool.Contracts`；不得引用主程序、具体窗体或内部编辑类型。
- OCR/二维码使用截图的原始未标注图像，除非需求明确要求识别标注结果；统一虚拟桌面客户区坐标。
- 模型加载、推理和网络翻译不在绘制回调或 UI 线程执行；支持取消并避免重复初始化大型模型。
- 进度无法准确估算时使用不确定进度，不伪造百分比；异常隔离到当前功能并给用户可理解反馈。
- 图片、推理会话、原生句柄、模型流和后台任务有明确所有权，功能释放后不得阻止模块热卸载。
- 翻译属于宿主服务，通过最小接口注入；识别模块不直接绑定第三方 HTTP 实现。
- 模块 ID/功能 ID 和现有结果语义保持兼容；模型资源路径不依赖当前工作目录。

## 联动照护

- 改识别输入：同步检查截图 Export/Preview、选区裁剪、二维码和多屏坐标。
- 改结果契约：同步检查结果窗体、复制、翻译、所有识别模块与模块运行时。
- 改 Paddle 模型或依赖：同步检查 Tiny/Small 两个包装模块、包体积、资源复制和卸载文件锁。
- 改取消/进度：同步检查功能会话关闭、热替换、异常隔离和 UI 状态恢复。
- 改翻译：同步检查网络超时/取消、隐私提示、语言选择与结果窗体关闭。

## 导航完整性维护

- 执行任务时把新发现的核心入口、公共接口、管理器/服务、数据模型、重要调用方/订阅方、稳定脚本/资源和测试/运行入口列为导航候选。
- 新增文件只要属于本系统核心职责、会成为常用入口或扩展点、影响其他系统/被多处调用，或后续同类任务需要快速定位，且具有稳定价值，就更新本 Skill。
- 核心文件新增、删除、移动或重命名时，同一任务内更新“文件导航”和“联动照护”；移除失效路径，不保留误导信息。
- 不逐一记录普通内部实现、临时文件、生成产物或一次性脚本。
- 新系统、职责迁移或常见任务路由变化时，同时更新 `.agents/skills/navigate-cutcut-systems/SKILL.md`。
- 暂时无法确认归属时不要强行归类，写入 `.agents/skills/navigate-cutcut-systems/references/pending-boundaries.md`，待职责或调用证据充分后再处理。

## 验证

- 覆盖无文字、中文/英文、多行、旋转或低分辨率样本，以及二维码成功/失败样本。
- 验证取消、重复运行、模型缺失、损坏资源和模块替换后无文件锁。
- 结果 UI 验证复制、翻译错误和关闭期间异步回调；输出源变化需验证真实位图。
- 按根目录 `AGENTS.md` 完成格式、测试与 Release 构建；未明确要求时不打包。

## 迭代日志

- 日志位于 `references/iteration-log.md`；格式为 `日期 | 任务 | 触及文件 | 导航缺口/改进`。
- 追加后超过 10 条就删除最旧条目，永远只保留最近 10 条。
- 发现新入口、资源约定或联动风险时，先更新本 Skill，再记录本次任务。

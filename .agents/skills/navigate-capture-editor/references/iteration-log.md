# 迭代日志

按时间从旧到新排列。仅保留最近 10 条，写入第 11 条前删除最旧一条。

- 2026-08-03 | 文字框选中缩放并在编辑模式关闭缩放 | `Editing/Annotations.cs`、`CaptureAnnotationEditor.cs`、逻辑测试与已有图片 UI 冒烟 | 现有标注模型、文字编辑和验证入口准确，无导航缺口
- 2026-08-04 | 单选图片时 Ctrl+C 复制图片对象 | `Editing/CaptureAnnotationEditor.cs`、`Presentation/CaptureOverlayForm.cs`、逻辑测试与已有图片 UI 冒烟 | 现有输入、标注渲染与测试入口准确，无导航缺口
- 2026-08-04 | 编辑态框外鼠标穿透与 Ctrl+左键重新框选 | `CaptureOverlayForm.cs`、`CaptureOverlayInteractionLayout.cs`、`LiveAnnotationPointerHook.cs`、逻辑/UI 冒烟 | 补录稳定交互区域策略与全局指针入口
- 2026-08-05 | 框选完成后框外继续操作既有贴图 | `CaptureOverlayPresenter.cs`、`MainForm.cs`、框外交互 UI 冒烟 | 补录非模态截图会话呈现入口
- 2026-08-05 | Ctrl+R 原位刷新普通截图底图 | `CaptureBackgroundLayer.cs`、`CaptureBackgroundRefreshPolicy.cs`、`CaptureBackgroundRefreshHotkeyRegistration.cs`、`CaptureOverlayForm.cs`、逻辑/UI 冒烟 | 补录底图层与条件式刷新快捷键入口
- 2026-08-05 | 修复非模态截图浮层关闭后的重复释放 | `CaptureOverlayForm.cs`、`CaptureFeatureSession.cs`、逻辑测试与 `--capture-dispose-smoke` | 补录非模态浮层重复释放的稳定 UI 验证入口
- 2026-08-31 | 截图最终导出后的异步保存生命周期 | `CaptureOverlayForm.cs`、`IImageSaveService.cs`、`IClipboardService.cs` | 补录最终位图所有权与重复保存防护规则
- 2026-09-01 | 将最终批注截图交给收藏夹模块保存 | `CaptureOverlayForm.cs`、`ModuleContracts.cs`、`ScreenshotTool.Favorites` | 补录收藏命令、可见文字命名元数据、字符串偏好快照和 Export 位图所有权入口
- 2026-09-01 | 最终截图按宿主 PNG/JPEG 偏好异步保存 | `CaptureOverlayForm.cs`、`IImageSaveService.cs`、`ImageSaveService.cs` | Export 位图不变，只把格式偏好传给后台编码边界
- 2026-09-01 | 修复拖动截图框或编辑元素时框外黑屏 | `CaptureOverlayForm.cs`、截图框外交互 UI 冒烟 | 现有交互区域与拖动裁剪入口准确，无导航缺口

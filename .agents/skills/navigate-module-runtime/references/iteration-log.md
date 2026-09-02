# 迭代日志

按时间从旧到新排列。仅保留最近 10 条，写入第 11 条前删除最旧一条。

- 2026-08-05 | 核对截图会话中的既有贴图窗口交互 | `PinnedImageForm.cs`、`PinnedImageModule.cs`、`CaptureOverlayPresenter.cs` | 模块契约与生命周期无需变更，现有入口准确
- 2026-08-05 | 核对插件启停状态写入用户偏好 | `ModuleHost.cs`、`UserPreferenceModuleActivationStore.cs`、模块逻辑测试 | 激活偏好入口准确，无导航缺口
- 2026-09-01 | 新增截图收藏夹模块与按模块包配置窗口 | `ScreenshotTool.Favorites`、`ModuleManagementPage.cs`、`ModuleConfigurationForm.cs`、`ModuleContracts.cs` | 补录自定义目录图片宿主、配置页按需租约与独立窗口释放入口

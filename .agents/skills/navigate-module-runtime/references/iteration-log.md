# 迭代日志

按时间从旧到新排列。仅保留最近 10 条，写入第 11 条前删除最旧一条。

- 2026-08-05 | 核对截图会话中的既有贴图窗口交互 | `PinnedImageForm.cs`、`PinnedImageModule.cs`、`CaptureOverlayPresenter.cs` | 模块契约与生命周期无需变更，现有入口准确
- 2026-08-05 | 核对插件启停状态写入用户偏好 | `ModuleHost.cs`、`UserPreferenceModuleActivationStore.cs`、模块逻辑测试 | 激活偏好入口准确，无导航缺口

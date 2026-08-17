# 迭代日志

按时间从旧到新排列。仅保留最近 10 条，写入第 11 条前删除最旧一条。

- 2026-08-04 | 单选图片时 Ctrl+C 写入对象级图片 | `IClipboardService` 既有接口、`CaptureOverlayForm.cs`、已有图片 UI 冒烟 | 现有剪贴板入口准确，无导航缺口
- 2026-08-05 | 核对截图框外贴图右键复制 | `IModuleImageHost` 既有接口、`PinnedImageForm.cs`、框外交互 UI 冒烟 | 复制与保存入口无需变更，无导航缺口
- 2026-08-11 | 截图按本地日期自动创建并复用子文件夹 | `ScreenshotOutputFolderPolicy.cs`、`PngImageSaveService.cs`、`SavePathSettingsPage.cs`、画廊与安全校验 | 补录日期目录策略入口
- 2026-08-11 | 日期分类支持独立绑定父文件夹 | `UserPreferences.cs`、`SavePathSettingsPage.cs`、`MainForm.cs`、画廊 | 既有入口完整，无导航缺口

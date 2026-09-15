# 迭代日志

按时间从旧到新排列。仅保留最近 10 条，写入第 11 条前删除最旧一条。

- 2026-08-04 | 单选图片时 Ctrl+C 写入对象级图片 | `IClipboardService` 既有接口、`CaptureOverlayForm.cs`、已有图片 UI 冒烟 | 现有剪贴板入口准确，无导航缺口
- 2026-08-05 | 核对截图框外贴图右键复制 | `IModuleImageHost` 既有接口、`PinnedImageForm.cs`、框外交互 UI 冒烟 | 复制与保存入口无需变更，无导航缺口
- 2026-08-11 | 截图按本地日期自动创建并复用子文件夹 | `ScreenshotOutputFolderPolicy.cs`、`PngImageSaveService.cs`、`SavePathSettingsPage.cs`、画廊与安全校验 | 补录日期目录策略入口
- 2026-08-11 | 日期分类支持独立绑定父文件夹 | `UserPreferences.cs`、`SavePathSettingsPage.cs`、`MainForm.cs`、画廊 | 既有入口完整，无导航缺口
- 2026-08-31 | 截图与录屏共用日期分类目录 | `ArtifactOutputFolderPolicy.cs`、`CaptureOverlayForm.cs`、`SavePathSettingsPage.cs` | 日期目录入口已泛化为全部保存产物
- 2026-08-31 | 截图保存与图库刷新移出 UI 阻塞链路 | `IImageSaveService.cs`、`WindowsClipboardService.cs`、`CaptureOverlayForm.cs`、`ScreenshotGalleryPage.cs` | 补录后台 PNG、STA 剪贴板重试及最新图库快照规则
- 2026-09-01 | 新增收藏夹模块独立目录异步保存 | `IModuleImageStorageHost`、`ModuleImageHostProxy.cs`、`ScreenshotTool.Favorites` | 补录显式目录、图片内文字命名元数据与异步位图边界，普通日期目录和收藏目录保持分离
- 2026-09-01 | 保存页新增 PNG/JPEG 格式选择 | `ScreenshotImageFormat.cs`、`ImageSaveService.cs`、`SavePathSettingsPage.cs`、图片保存调用方 | PNG/JPEG 共用宿主策略，补录格式入口与透明像素规则
- 2026-09-14 | 贴图文字复制复用宿主剪贴板 | ModuleImageHostProxy.cs、PinnedImageTextController.cs | 补录选中文字复制边界，图片不含高亮
- 2026-09-15 | Ctrl+C 复制编辑元素，Ctrl+Shift+C 复制整图 | navigate-capture-editor、navigate-saved-artifacts；AnnotationClipboard、CaptureOverlayForm、IClipboardService | 补录同会话对象快照与通用剪贴板数据入口；未运行测试

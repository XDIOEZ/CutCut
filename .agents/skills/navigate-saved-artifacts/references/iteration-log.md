# 迭代日志

按时间从旧到新排列。仅保留最近 10 条，写入第 11 条前删除最旧一条。

- 2026-08-31 | 截图与录屏共用日期分类目录 | `ArtifactOutputFolderPolicy.cs`、`CaptureOverlayForm.cs`、`SavePathSettingsPage.cs` | 日期目录入口已泛化为全部保存产物
- 2026-08-31 | 截图保存与图库刷新移出 UI 阻塞链路 | `IImageSaveService.cs`、`WindowsClipboardService.cs`、`CaptureOverlayForm.cs`、`ScreenshotGalleryPage.cs` | 补录后台 PNG、STA 剪贴板重试及最新图库快照规则
- 2026-09-01 | 新增收藏夹模块独立目录异步保存 | `IModuleImageStorageHost`、`ModuleImageHostProxy.cs`、`ScreenshotTool.Favorites` | 补录显式目录、图片内文字命名元数据与异步位图边界，普通日期目录和收藏目录保持分离
- 2026-09-01 | 保存页新增 PNG/JPEG 格式选择 | `ScreenshotImageFormat.cs`、`ImageSaveService.cs`、`SavePathSettingsPage.cs`、图片保存调用方 | PNG/JPEG 共用宿主策略，补录格式入口与透明像素规则
- 2026-09-14 | 贴图文字复制复用宿主剪贴板 | ModuleImageHostProxy.cs、PinnedImageTextController.cs | 补录选中文字复制边界，图片不含高亮
- 2026-09-15 | Ctrl+C 复制编辑元素，Ctrl+Shift+C 复制整图 | navigate-capture-editor、navigate-saved-artifacts；AnnotationClipboard、CaptureOverlayForm、IClipboardService | 补录同会话对象快照与通用剪贴板数据入口；未运行测试
- 2026-10-05 | 图库直接拖出与外部文件拖入查看 | ScreenshotGalleryPage、WindowsArtifactDragDrop | 补录 Windows 文件/位图复制数据、临时 PNG 保留与双向原生 OLE 验证
- 2026-10-06 | 悬浮贴图移除拖出入口 | PinnedImageDragController、ScreenshotGalleryPage | 文件发送留在图库，贴图只接收拖入；同步文档与交互验证
- 2026-10-10 | 录屏文件日期时间在前 | RecordingOutputPathReservation、ScreenRecordingFeature | 补录录屏命名与并发占位入口，重名编号延续；未运行验证
- 2026-10-10 | 左侧立即截图下方添加打开保存目录 | AppShellControl、MainForm | 复用既有目录处理，日期分类打开父目录；按用户要求未运行验证

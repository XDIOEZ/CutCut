# 迭代日志

按时间从旧到新排列。仅保留最近 10 条，写入第 11 条前删除最旧一条。

- 2026-09-01 | 将最终批注截图交给收藏夹模块保存 | `CaptureOverlayForm.cs`、`ModuleContracts.cs`、`ScreenshotTool.Favorites` | 补录收藏命令、可见文字命名元数据、字符串偏好快照和 Export 位图所有权入口
- 2026-09-01 | 最终截图按宿主 PNG/JPEG 偏好异步保存 | `CaptureOverlayForm.cs`、`IImageSaveService.cs`、`ImageSaveService.cs` | Export 位图不变，只把格式偏好传给后台编码边界
- 2026-09-01 | 修复拖动截图框或编辑元素时框外黑屏 | `CaptureOverlayForm.cs`、截图框外交互 UI 冒烟 | 现有交互区域与拖动裁剪入口准确，无导航缺口
- 2026-09-14 | 贴图原位拖选与蓝色高亮 | PinnedImageTextController.cs、ImageTextSelection.cs、PinnedImageForm.cs | 新增独立文字交互入口；手动交互由用户验收
- 2026-09-15 | Ctrl+C 复制编辑元素，Ctrl+Shift+C 复制整图 | navigate-capture-editor、navigate-saved-artifacts；AnnotationClipboard、CaptureOverlayForm、IClipboardService | 补录同会话对象快照与通用剪贴板数据入口；未运行测试
- 2026-09-18 | 副屏录制失败联动检查 | 只读复核 `LiveAnnotationSession.cs`、`CaptureEditorToolbar.cs` | 编辑层无修改，故障定位至录屏帮助进程；现有导航覆盖调用链
- 2026-10-05 | 截图/录屏工具栏拖动与会话位置保留 | CaptureToolbarDragController.cs、CaptureEditorToolbar.cs、CaptureOverlayForm.cs、LiveAnnotationSession.cs、Program.ToolbarDrag.cs | 补录共用拖动入口；真实鼠标与录制保存通过，全量逻辑测试受旧贴图菜单断言阻断
- 2026-10-05 | 贴图无修饰键把手拖出与拖入新建 | PinnedImageDragController、PinnedImageForm | 补录独立交互控制器，保留原移动/缩放/选字，拖出原始像素 PNG
- 2026-10-06 | 恢复无遮挡贴图 | PinnedImageDragController、PinnedImageForm | 移除拖出把手及发送手势，保留原移动/缩放/选字；更新实际显示验证
- 2026-10-10 | 多个独立截图框与录屏批注菜单 | CaptureSessionCoordinator、CaptureWindowParkingLease、CaptureOverlayForm、LiveAnnotationSession | 补录独立会话、活动后台输入与其他框采集停放；按用户要求未运行验证

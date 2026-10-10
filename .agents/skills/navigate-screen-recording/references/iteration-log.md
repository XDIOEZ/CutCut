# 迭代日志

按时间从旧到新排列。仅保留最近 10 条，写入第 11 条前删除最旧一条。

- 2026-08-31 | 录屏复用宿主日期保存目录 | `ScreenRecordingFeature.cs`、`CaptureOverlayForm.cs`、`ArtifactOutputFolderPolicy.cs` | 移除失效的 `RecordingStorage.cs` 导航并补录真实存储入口
- 2026-09-18 | 修复副屏原生录制进程崩溃导致保存失败 | `Program.cs`、`Test-ScreenRecordingHelper.ps1` | 补录显式捕获 API 策略与多屏回归入口，修正 RecordingOptions 导航；主副屏保存及副屏视频解码通过
- 2026-10-05 | 录制中拖动共用工具栏 | CaptureEditorToolbar.cs、CaptureToolbarDragController.cs、LiveAnnotationSession.cs、Program.ToolbarDrag.cs | 宿主统一管理拖动，录屏模块契约不变；真实开始/拖动/暂停/继续/画笔/停止保存通过，MP4 解码为 640×360
- 2026-10-05 | 自定义成片尺寸与超级压缩 | RecordingOutputResolution.cs、RecordingOptions.cs、ScreenRecordingPreferences.cs、ScreenRecordingSettingsPage.cs、ScreenRecordingStorageEstimator.cs、ScreenRecorderSession.cs、Recorder/Program.cs、Program.RecordingCompression.cs、RecordingCompressionTests.cs、Test-ScreenRecordingHelper.ps1 | 补录缩放/留边、AAC 96 kbps 下限和软件低规格编码；主副屏、静音/有声、暂停继续、真实解码及偏好恢复通过，全量测试仍受旧贴图菜单断言阻断
- 2026-10-10 | 录屏元素右键与短按/长按工具 | LiveAnnotationSession、LiveAnnotationContextMenu、LiveAnnotationKeyboardHook、LiveAnnotationToolShortcutState、RecordingOutputPathReservation | 补录临时绘图提交、Oem3选择、多框输入归属与文件名占位；按用户要求未运行验证
- 2026-10-10 | 录屏文件名改为时间在前 | RecordingOutputPathReservation、ScreenRecordingFeature、LogicTests/Program | 两个入口统一 yyyyMMdd_HHmmss_录屏，同步既有断言与文档；未运行验证

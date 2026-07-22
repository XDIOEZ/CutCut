# 用户偏好 JSON

轻截将本地用户配置保存为带版本号的 JSON 文档：

```text
%LocalAppData%\LightShotCN\Profiles\local.json
```

当前默认配置身份为 `local`。以后接入账号时，可以使用账号对应的 `ProfileId` 创建独立配置存储；文件名由配置身份稳定生成，截图和编辑模块不需要感知账号系统。

## 文档结构

```json
{
  "schemaVersion": 1,
  "profileId": "local",
  "settings": {
    "outputFolder": "C:\\Users\\User\\Pictures\\轻截",
    "hotkeyModifiers": "control, shift",
    "hotkeyVirtualKey": 88,
    "startMinimized": false,
    "preferences": {
      "stickerSelectionMoveMode": "followSelection",
      "minimumToolWidth": 2,
      "maximumToolWidth": 8,
      "lastToolWidth": 4,
      "annotationRotationStepDegrees": 5,
      "drawingCursorShape": "circle",
      "annotationSnappingEnabled": true,
      "annotationSnapThresholdPixels": 8,
      "ctrlDragStepPixels": 10,
      "screenRecordingCaptureSystemAudio": true,
      "screenRecordingCaptureMicrophone": true,
      "screenRecordingShowMouseClickIndicator": true,
      "screenRecordingFramesPerSecond": 30,
      "screenRecordingVideoBitrate": 8000000,
      "recordingRegionIndicatorStyle": "dashed",
      "moduleBooleanPreferences": {
        "screenshot-tool.long-capture.safety-checks": false,
        "screenshot-tool.screen-recording.capture-system-audio": true,
        "screenshot-tool.screen-recording.capture-microphone": true,
        "screenshot-tool.screen-recording.show-mouse-click-indicator": true
      },
      "moduleIntegerPreferences": {
        "screenshot-tool.screen-recording.frames-per-second": 30,
        "screenshot-tool.screen-recording.video-bitrate": 8000000,
        "screenshot-tool.screen-recording.region-indicator-style": 1
      },
      "moduleStringPreferences": {},
      "screenshotFileNameMode": "dateTime",
      "dismissSaveNotificationBeforeCapture": true,
      "hideMainWindowDuringCapture": false,
      "longCaptureSafetyChecksEnabled": false
    }
  }
}
```

`hotkeyModifiers`、`hotkeyVirtualKey` 和 `startMinimized` 统一在“截图设置”分页中配置。快捷键输入框获得焦点时会暂时取消全局监听，保存新组合键失败时恢复原快捷键；`startMinimized` 为 `true` 时，程序启动后直接进入系统托盘。

`stickerSelectionMoveMode` 支持：

- `followSelection`：图片、粘贴文字和工具栏文字随截图框移动。
- `keepScreenPosition`：贴纸保持屏幕坐标，越界内容仅临时隐藏。

粗细范围会在读取时归一化到程序支持的安全范围，异常枚举值会恢复为默认模式。`lastToolWidth` 会在每次截图编辑结束后记录最后使用的粗细，并在下次打开编辑器时恢复；若粗细范围后来发生变化，该值会自动限制到新范围内。当前粗细除了控制绘图工具线宽，也会按默认粗细 `4` 对应 `18px` 的比例控制新建文字元素字号；极小字号保留 `8px` 的可读下限。

`annotationRotationStepDegrees` 是 `Alt + 鼠标滚轮` 每格旋转的角度，默认为 `5`，读取时会限制在 `1` 至 `90` 度。滚轮向上为顺时针，向下为逆时针。

`drawingCursorShape` 控制画笔和马赛克的笔刷轮廓光标，支持 `circle`（圆形，默认）和 `square`（正方形）。轮廓尺寸按当前粗细和绘制系数计算；编辑长图时还会跟随当前视图缩放。该光标只在编辑预览中显示，不会写入导出图片。

`annotationSnappingEnabled` 控制新截图或录屏批注会话是否默认开启元素吸附，默认为 `true`。共享工具栏中的“吸附”按钮和快速双击 `Ctrl` 只切换当前会话，不会覆盖设置页保存的默认值。

`annotationSnapThresholdPixels` 是元素边缘或中心参考线触发吸附的最大距离，默认为 `8`，读取时限制在 `1` 至 `48` 像素。`ctrlDragStepPixels` 是元素移动或手柄缩放期间按住 `Ctrl` 使用的固定步长，默认为 `10`，读取时限制在 `1` 至 `100` 像素。

`moduleBooleanPreferences`、`moduleIntegerPreferences` 和 `moduleStringPreferences` 是模块通用键值存储。宿主只负责持久化，不解释具体键；模块自带的设置页负责默认值、参数校验和写入。删除模块不会删除其偏好，因此重新安装后会恢复用户上次的选择，但未安装期间宿主不会创建对应设置 UI 或加载模块程序集。

旧字段 `screenRecordingCaptureSystemAudio`、`screenRecordingCaptureMicrophone`、`screenRecordingShowMouseClickIndicator`、`screenRecordingFramesPerSecond`、`screenRecordingVideoBitrate`、`recordingRegionIndicatorStyle` 和 `longCaptureSafetyChecksEnabled` 暂时保留用于兼容迁移。读取旧配置时会把它们复制到对应的通用模块键；新设置页和截图功能均以通用模块键为准。

录屏模块的系统声音与默认麦克风均默认开启。左键黄色半透明圆圈默认开启；开启时用户在录制现场即可看到圆圈，按住左键期间圆圈持续跟随鼠标移动，松开后短暂保留，编码器同时将对应效果写入最终 MP4；关闭时两处都不显示。录制帧率支持 `30` 或 `60` FPS，视频码率支持 2、4、8、12、20 Mbps；异常数值会归一化到最接近的支持档位。这些参数统一由模块自带的“录屏设置”分页保存，点击“录屏”后直接生效。

模块整数键 `screenshot-tool.screen-recording.region-indicator-style` 控制录屏期间选区边缘的范围提示：`0` 为实线、`1` 为虚线（默认）、`2` 为不显示。该选项同样位于“录屏设置”分页；提示线绘制在 Windows 捕获排除的输入层，只用于提示用户，不会进入最终 MP4。异常值会恢复为虚线。

`screenshotFileNameMode` 控制 PNG 文件名，支持：

- `dateTime`：默认规则，使用 `截图_年-月-日_时-分-秒-毫秒.png`；
- `sequence`：读取当前保存目录中的纯数字 PNG，从已有最大数字继续递增；目录中没有数字文件时从 `0.png` 开始；
- `imageText`：按输入顺序组合最终图片范围内的工具栏文字和粘贴文字，连续空白与 Windows 文件名非法字符会替换为分隔符，最长保留 80 个字符。没有可用文字时自动回退到日期时间；文件重名时追加 `_1`、`_2` 等后缀。

异常命名枚举值会恢复为 `dateTime`。图片命名规则只影响 PNG；录屏继续使用独立的 `录屏_日期时间.mp4` 规则。

`dismissSaveNotificationBeforeCapture` 位于“截图设置”分页，默认为 `true`。下一次截图启动时，如果右下角的图片或录屏保存提示仍在显示，程序会先关闭提示，再抓取桌面，避免提示进入截图或遮挡连续操作。设为 `false` 后提示按原来的约 6 秒时限显示，可用于演示点击通知后打开目录并选中文件的跳转功能。

`hideMainWindowDuringCapture` 同样位于“截图设置”分页，默认为 `false`，此时从轻截主界面启动截图会保留工作台，方便制作软件宣传图。设为 `true` 后，主窗口会先通过 Windows 捕获排除阻止进入抓屏，再立即设为透明并隐藏；即使系统启用了窗口淡出动画，动画帧也不会写入桌面快照。截图会话结束后主窗口恢复显示。

`longCaptureSafetyChecksEnabled` 默认为 `false`：

- `false`：宽松拼接。低置信度、多个近似接缝或固定悬浮内容不会主动终止长截图；程序选择最高分接缝，完全无法定位时跳过该帧继续。
- `true`：安全截图。使用严格重叠校验，连续无法确认接缝时停止并询问是否保留已验证部分。

两种模式都会保留尺寸、像素数、帧队列和内存上限，这些限制用于避免程序崩溃，不属于接缝可信度校验。

## 兼容迁移

旧版本的 `%LocalAppData%\LightShotCN\settings.json` 仍可读取。首次加载时会转换为 `Profiles\local.json`，原文件不会删除，便于回退和人工恢复。

保存采用临时文件替换方式，避免程序意外退出时留下只写入一半的 JSON。

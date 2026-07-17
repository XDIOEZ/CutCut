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
      "longCaptureSafetyChecksEnabled": false
    }
  }
}
```

`stickerSelectionMoveMode` 支持：

- `followSelection`：图片、粘贴文字和工具栏文字随截图框移动。
- `keepScreenPosition`：贴纸保持屏幕坐标，越界内容仅临时隐藏。

粗细范围会在读取时归一化到程序支持的安全范围，异常枚举值会恢复为默认模式。

`longCaptureSafetyChecksEnabled` 默认为 `false`：

- `false`：宽松拼接。低置信度、多个近似接缝或固定悬浮内容不会主动终止长截图；程序选择最高分接缝，完全无法定位时跳过该帧继续。
- `true`：安全截图。使用严格重叠校验，连续无法确认接缝时停止并询问是否保留已验证部分。

两种模式都会保留尺寸、像素数、帧队列和内存上限，这些限制用于避免程序崩溃，不属于接缝可信度校验。

## 兼容迁移

旧版本的 `%LocalAppData%\LightShotCN\settings.json` 仍可读取。首次加载时会转换为 `Profiles\local.json`，原文件不会删除，便于回退和人工恢复。

保存采用临时文件替换方式，避免程序意外退出时留下只写入一半的 JSON。

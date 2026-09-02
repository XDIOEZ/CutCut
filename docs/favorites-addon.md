# 截图收藏夹模块

此安装包为轻截提供可独立装卸的截图收藏能力，模块版本为 `1.0.0`，需要轻截 `1.11.8` 或更高版本。解压后，将包内的 `Modules` 文件夹合并到
轻截程序目录；也可以直接把 `Modules\Favorites` 复制到程序旁的 `Modules` 中。

安装后，在设置工作台打开“插件模块”，找到“截图收藏夹”并点击“管理配置”。在独立配置窗口中
选择收藏文件夹并保存。下一次截图完成框选和标注后，工具栏会出现“收藏”按钮；点击后会把包含
全部批注的最终图片按“保存路径”页选择的 PNG 或 JPEG 格式直接保存到收藏文件夹，并结束当前截图会话。

- 收藏目录与普通截图/录屏保存目录相互独立。
- 收藏目录不套用普通保存页中的按日期自动分组。
- 图片格式与文件名继续使用轻截当前选择的保存规则，包括最终选区中的图片内文字。
- 配置目录不存在时，第一次收藏会自动创建。

模块目录：

```text
Modules/
  Favorites/
    ScreenshotTool.Favorites.dll
```

收藏夹模块使用稳定模块 ID `screenshot-tool.favorites`、功能 ID
`screenshot-tool.favorites.feature` 和命令 ID `screenshot-tool.favorites.save`。模块只通过
`ScreenshotTool.Contracts` 请求最终导出位图、字符串偏好和图片写盘能力，不引用主程序窗体或保存服务。

删除整个 `Modules\Favorites` 文件夹即可卸载；也可以在“插件模块”页禁用或永久删除。需要恢复时，
重新下载 `favorites-addon-win-x64.zip` 并放回同名目录。

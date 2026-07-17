# Modules

将基于 `ScreenshotTool.Contracts` 编译的模块 DLL 及其私有依赖放入此目录。

程序会自动检测 DLL 的新增、替换和删除，也可以在托盘菜单点击“重新加载模块”。新版本从下一次截图开始生效；已经打开的截图会安全地继续使用旧版本，直到该截图窗口关闭。

正式构建和发布会自动把第一方长截图模块 `ScreenshotTool.LongCapture.dll` 放入此目录，同时保持它与主程序解耦，以便热替换和拆卸。

模块在主程序进程内以完全信任方式运行，请只加载可信来源的 DLL。开发说明见项目的 `docs/modular-architecture.md`。

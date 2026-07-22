# Modules

每个模块使用此目录下的一个独立一级子文件夹。将基于 `ScreenshotTool.Contracts`
编译的模块入口 DLL、私有依赖和资源全部放进该模块自己的文件夹；`Modules` 根目录不直接放模块 DLL。

程序会自动检测模块文件夹及其内部文件的新增、替换和删除，也可以在托盘菜单点击“重新加载模块”。
整个文件夹复制进来即可安装，整个文件夹删除即可卸载。模块自带的设置页也会随安装立即加入左侧导航，卸载后立即消失；新版本从下一次截图开始生效，已经打开的截图会安全地继续使用旧版本，直到该截图窗口关闭。

```text
Modules/
  LongCapture/
    ScreenshotTool.LongCapture.dll
  ScreenRecording/
    ScreenshotTool.ScreenRecording.dll
    Recorder/
      ScreenshotTool.ScreenRecording.Recorder.exe
      ScreenRecorderLib.dll
```

正式构建和发布会自动把第一方长截图模块放入 `Modules\LongCapture`，同时保持它与主程序解耦，以便热替换和拆卸。

录屏模块默认预装在完整包中，也保留独立的可选下载包；基础版不会携带录屏编码器，也不会显示“录屏设置”。录屏模块和基础程序必须使用兼容版本（录屏模块 1.7.0 需要基础程序 1.10.0 或更高版本）；安装或升级时，将包内 `Modules\ScreenRecording` 文件夹复制到对应版本的程序旁。模块入口、自带的录屏设置页与 `Recorder` 编码器子目录都属于同一个 `ScreenRecording` 模块。录屏批注、专属“选择”入口和菜单栏仍由基础程序通过公共契约提供，模块不携带重复批注或编辑按钮 UI。删除整个 `Modules\ScreenRecording` 文件夹即可同时卸载录屏能力与录屏设置，不影响截图、核心批注和长截图。

模块在主程序进程内以完全信任方式运行，请只加载可信来源的 DLL。开发说明见项目的 `docs/modular-architecture.md`。

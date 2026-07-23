# Modules

每个模块使用此目录下的一个独立一级子文件夹。将基于 `ScreenshotTool.Contracts`
编译的模块入口 DLL、私有依赖和资源全部放进该模块自己的文件夹；`Modules` 根目录不直接放模块 DLL。

程序会自动检测模块文件夹及其内部文件的新增、替换和删除，也可以在托盘菜单点击“重新加载模块”。
整个文件夹复制进来即可安装，整个文件夹删除即可卸载。模块自带的设置页也会随安装立即加入左侧导航，卸载后立即消失；新版本从下一次截图开始生效，已经打开的截图会安全地继续使用旧版本，直到该截图窗口关闭。

```text
Modules/
  PinnedImage/
    ScreenshotTool.PinnedImage.dll
  LongCapture/
    ScreenshotTool.LongCapture.dll
  Ocr/
    ScreenshotTool.Ocr.dll
  PaddleOcrTiny/
    ScreenshotTool.PaddleOcr.Tiny.dll
    ScreenshotTool.PaddleOcr.dll
    RapidOcrNet.dll
    ... ONNX Runtime 与 SkiaSharp 私有依赖
    Models/
      PP-OCRv6_det_tiny.onnx
      PP-OCRv6_rec_tiny.onnx
      ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx
      ppocrv6_tiny_dict.txt
  PaddleOcrSmall/
    ScreenshotTool.PaddleOcr.Small.dll
    ScreenshotTool.PaddleOcr.dll
    RapidOcrNet.dll
    ... ONNX Runtime 与 SkiaSharp 私有依赖
    Models/
      PP-OCRv6_det_small.onnx
      PP-OCRv6_rec_small.onnx
      ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx
      ppocrv6_dict.txt
  QrCode/
    ScreenshotTool.QrCode.dll
    zxing.dll
    LICENSE-ZXING.NET.txt
  ScreenRecording/
    ScreenshotTool.ScreenRecording.dll
    Recorder/
      ScreenshotTool.ScreenRecording.Recorder.exe
      ScreenRecorderLib.dll
```

正式构建和完整包会自动把第一方贴图悬浮窗、长截图、本地 OCR 与二维码扫描模块分别放入
`Modules\PinnedImage`、`Modules\LongCapture`、`Modules\Ocr` 和 `Modules\QrCode`，同时保持它们与主程序解耦，以便热替换和拆卸。
贴图模块把包含全部批注的最终选区固定为置顶无边框窗口；左键拖动内部可移动，从四边或四角拖动时
默认等比缩放，按住 `Shift` 可自由改变宽高比。右键菜单提供删除、复制、保存和重新编辑。模块禁用、
删除或替换时会关闭并释放它创建的全部贴图窗口。
本地 OCR 使用 Windows 自带的离线文字识别能力，对原图、高清放大、灰度对比度增强和 Otsu 二值化结果
分别识别后择优，不携带云端密钥或大型模型文件；本地 OCR 模块 1.1.0 需要轻截 1.11.0 或更高版本。

`PaddleOcrTiny` 与 `PaddleOcrSmall` 是两个额外下载、互不依赖的 PP-OCRv6 本机 ONNX 模块，不会进入
轻量或便携完整包。Tiny 更偏向体积和速度，Small 更偏向复杂背景、小字和中英混排精度；二者都需要
轻截 1.11.0 或更高版本，并且必须完整保留入口 DLL、共享识别程序集、原生运行库、`Models` 与许可文件。
通常只启用本地、Tiny、Small 三者之一；如果同时安装，可在“插件模块”页独立启用、禁用或永久删除。

二维码扫描模块 1.0.0 同样需要轻截 1.11.0 或更高版本。扫描在本机离线完成，结果交给宿主的
通用侧边小窗显示；模块不会自动打开网址或执行二维码内容。`zxing.dll` 与许可文本是该模块的
私有文件，安装、升级或恢复时必须与入口 DLL 一起保留在 `Modules\QrCode` 中。

录屏模块默认预装在完整包中，也保留独立的可选下载包；基础版不会携带录屏编码器，也不会显示“录屏设置”。录屏模块和基础程序必须使用兼容版本（录屏模块 1.7.0 需要基础程序 1.10.0 或更高版本）；安装或升级时，将包内 `Modules\ScreenRecording` 文件夹复制到对应版本的程序旁。模块入口、自带的录屏设置页与 `Recorder` 编码器子目录都属于同一个 `ScreenRecording` 模块。录屏批注、专属“选择”入口和菜单栏仍由基础程序通过公共契约提供，模块不携带重复批注或编辑按钮 UI。删除整个 `Modules\ScreenRecording` 文件夹即可同时卸载录屏能力与录屏设置，不影响截图、核心批注、OCR 和长截图。

删除整个 `Modules\PinnedImage`、`Modules\Ocr`、`Modules\PaddleOcrTiny`、`Modules\PaddleOcrSmall` 或 `Modules\QrCode`
文件夹即可卸载对应功能；也可以在设置工作台的“插件模块”页禁用或永久删除。需要恢复时，重新下载
对应独立模块包并放回同名目录。

模块在主程序进程内以完全信任方式运行，请只加载可信来源的 DLL。开发说明见项目的 `docs/modular-architecture.md`。

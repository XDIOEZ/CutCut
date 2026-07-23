# PP-OCR 本地文字识别模块

PP-OCR Tiny 与 PP-OCR Small 是两个彼此独立的可选模块。二者都完全在本机使用
PP-OCRv6 ONNX 模型识别截图选区，不上传图片，也不要求 Python；需要轻截 `1.11.0`
或更高版本。

- Tiny 的模型和安装包更小、启动与识别更快，适合一般界面文字。
- Small 的模型更大、识别精度更高，适合小字、复杂背景和中英混排。

当前固定模型文件的未压缩体积分别约为 Tiny `7.02 MiB`、Small `30.76 MiB`。独立模块还必须包含
ONNX Runtime、SkiaSharp 和 RapidOcrNet 等本机依赖，因此完整安装目录与下载 ZIP 会比模型本身更大。

建议在本地 OCR、PP-OCR Tiny 和 PP-OCR Small 中只启用一个，避免截图工具栏同时出现多个 OCR 按钮。也可以同时安装后，
在轻截的“插件模块”页面按需启用或禁用。

安装时保持 ZIP 内的目录结构，将其中的 `Modules` 文件夹合并到轻截程序旁。Tiny 与
Small 分别安装到：

```text
Modules\PaddleOcrTiny
Modules\PaddleOcrSmall
```

模块目录中的 DLL、`Models` 文件夹和第三方许可文件都属于同一个模块，不要只复制入口
DLL。安装后重新打开截图界面即可看到对应的 `OCR Tiny` 或 `OCR Small` 按钮。

两个模块的稳定下载文件名分别为：

```text
paddle-ocr-tiny-addon-win-x64.zip
paddle-ocr-small-addon-win-x64.zip
```

卸载时可在“插件模块”页面禁用或永久删除；也可以在轻截完全退出后删除相应模块目录。

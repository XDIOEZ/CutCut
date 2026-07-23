namespace ScreenshotTool.PaddleOcr.Tiny;

public sealed class PaddleOcrTinyModule : PaddleOcrModuleBase
{
    public override string Id => "screenshot-tool.paddle-ocr.tiny";

    public override string DisplayName => "PP-OCR Tiny 文字识别";

    protected override PaddleOcrVariant Variant => PaddleOcrVariant.Tiny;

    protected override string FeatureId => "screenshot-tool.paddle-ocr.tiny.feature";

    protected override string CommandId => "screenshot-tool.paddle-ocr.tiny.recognize";

    protected override string CommandText => "OCR Tiny";

    protected override string CommandToolTip => "使用本地 PP-OCRv6 Tiny 模型识别当前选区";

    protected override string ResultTitle => "PP-OCR Tiny 识别结果";

    protected override int FeatureOrder => 551;
}

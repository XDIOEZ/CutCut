namespace ScreenshotTool.PaddleOcr.Small;

public sealed class PaddleOcrSmallModule : PaddleOcrModuleBase
{
    public override string Id => "screenshot-tool.paddle-ocr.small";

    public override string DisplayName => "PP-OCR Small 文字识别";

    protected override PaddleOcrVariant Variant => PaddleOcrVariant.Small;

    protected override string FeatureId => "screenshot-tool.paddle-ocr.small.feature";

    protected override string CommandId => "screenshot-tool.paddle-ocr.small.recognize";

    protected override string CommandText => "OCR Small";

    protected override string CommandToolTip => "使用本地 PP-OCRv6 Small 模型识别当前选区";

    protected override string ResultTitle => "PP-OCR Small 识别结果";

    protected override int FeatureOrder => 552;
}

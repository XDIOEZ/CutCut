using RapidOcrNet;

namespace ScreenshotTool.PaddleOcr;

internal sealed record PaddleOcrModelFiles(
    string DetectorPath,
    string ClassifierPath,
    string RecognizerPath,
    string DictionaryPath)
{
    private const string ClassifierFileName =
        "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx";

    public static PaddleOcrModelFiles Resolve(
        string moduleDirectory,
        PaddleOcrVariant variant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleDirectory);
        var modelDirectory = Path.Combine(moduleDirectory, "Models");
        var suffix = variant switch
        {
            PaddleOcrVariant.Tiny => "tiny",
            PaddleOcrVariant.Small => "small",
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };
        var dictionaryName = variant == PaddleOcrVariant.Tiny
            ? "ppocrv6_tiny_dict.txt"
            : "ppocrv6_dict.txt";
        return new PaddleOcrModelFiles(
            Path.Combine(modelDirectory, $"PP-OCRv6_det_{suffix}.onnx"),
            Path.Combine(modelDirectory, ClassifierFileName),
            Path.Combine(modelDirectory, $"PP-OCRv6_rec_{suffix}.onnx"),
            Path.Combine(modelDirectory, dictionaryName));
    }

    public RapidOcrModelSet CreateModelSet(PaddleOcrVariant variant)
    {
        EnsurePresent();
        var preset = variant switch
        {
            PaddleOcrVariant.Tiny => RapidOcrModelSet.PPOCRv6Tiny,
            PaddleOcrVariant.Small => RapidOcrModelSet.PPOCRv6Small,
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };
        return preset with
        {
            DetModelPath = DetectorPath,
            ClsModelPath = ClassifierPath,
            RecModelPath = RecognizerPath,
            KeysPath = DictionaryPath
        };
    }

    public IReadOnlyList<string> GetMissingFiles() =>
        new[]
        {
            DetectorPath,
            ClassifierPath,
            RecognizerPath,
            DictionaryPath
        }.Where(path => !File.Exists(path)).ToArray();

    internal void EnsurePresent()
    {
        var missingFiles = GetMissingFiles();
        if (missingFiles.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "PP-OCR 模块文件不完整，缺少：" +
            string.Join("、", missingFiles.Select(Path.GetFileName)) +
            "。请重新下载并完整解压对应模块。");
    }
}

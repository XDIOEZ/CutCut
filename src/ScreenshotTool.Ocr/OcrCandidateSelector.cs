namespace ScreenshotTool.Ocr;

internal static class OcrCandidateSelector
{
    // Keeps plain-text consumers on the same candidate ranking as spatial recognition.
    public static string SelectBest(IReadOnlyList<OcrWorkerResult> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return OcrTextNormalizer.Normalize(SelectBestResult(candidates)?.Text ?? string.Empty);
    }

    // Selects the same winning candidate while retaining its spatial recognition data.
    public static OcrWorkerResult? SelectBestResult(IReadOnlyList<OcrWorkerResult> candidates) =>
        candidates.Where(candidate => !string.IsNullOrWhiteSpace(OcrTextNormalizer.Normalize(candidate.Text)))
            .OrderByDescending(Score).FirstOrDefault();

    private static double Score(OcrWorkerResult candidate)
    {
        var text = candidate.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return double.MinValue;
        }

        var content = 0;
        var suspicious = 0;
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character) || IsCjk(character))
            {
                content++;
            }
            else if (character is '\uFFFD' or '\0' || char.IsControl(character) && character != '\n')
            {
                suspicious++;
            }
        }

        var contentRatio = content / (double)Math.Max(1, text.Length);
        return (content * 4D) +
               (candidate.WordCount * 1.5D) +
               Math.Min(12, candidate.LineCount) +
               (contentRatio * 20D) -
               (suspicious * 30D);
    }

    private static bool IsCjk(char character) =>
        character is >= '\u3400' and <= '\u9fff' or
        >= '\uf900' and <= '\ufaff' or
        >= '\u3040' and <= '\u30ff' or
        >= '\uac00' and <= '\ud7af';
}

internal sealed record OcrWorkerResult(
    string Name, string Text, int LineCount, int WordCount, OcrWorkerWord[]? Words = null);

internal sealed record OcrWorkerWord(string Text, int LineIndex, float X, float Y,
    float Width, float Height, string? LeadingText = null);

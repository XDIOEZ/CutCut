using System.Text;

namespace ScreenshotTool.PaddleOcr;

internal static class PaddleOcrTextNormalizer
{
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
        if (normalized.Length < 3)
        {
            return normalized;
        }

        var builder = new StringBuilder(normalized.Length);
        for (var index = 0; index < normalized.Length; index++)
        {
            var current = normalized[index];
            if (current == ' ' &&
                index > 0 &&
                index + 1 < normalized.Length &&
                IsCjk(normalized[index - 1]) &&
                IsCjk(normalized[index + 1]))
            {
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static bool IsCjk(char character) =>
        character is >= '\u3400' and <= '\u9fff' or
        >= '\uf900' and <= '\ufaff' or
        >= '\u3040' and <= '\u30ff' or
        >= '\uac00' and <= '\ud7af';
}

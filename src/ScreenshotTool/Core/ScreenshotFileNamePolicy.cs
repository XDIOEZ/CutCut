using System.Text;

namespace ScreenshotTool.Core;

internal static class ScreenshotFileNamePolicy
{
    private const int MaximumTextStemLength = 80;
    private static readonly HashSet<string> ReservedWindowsNames = new(
        [
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static string CreateFileName(
        ScreenshotFileNameMode mode,
        DateTime timestamp,
        IEnumerable<string> existingFileNames,
        IEnumerable<string>? imageTexts = null)
    {
        ArgumentNullException.ThrowIfNull(existingFileNames);
        var existingStems = existingFileNames
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(stem => !string.IsNullOrWhiteSpace(stem))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stem = mode switch
        {
            ScreenshotFileNameMode.Sequence => CreateSequenceStem(existingStems),
            ScreenshotFileNameMode.ImageText => CreateImageTextStem(imageTexts) is { } textStem
                ? MakeUnique(textStem, existingStems)
                : MakeUnique(CreateDateTimeStem(timestamp), existingStems),
            _ => MakeUnique(CreateDateTimeStem(timestamp), existingStems)
        };
        return stem + ".png";
    }

    internal static string? CreateImageTextStem(IEnumerable<string>? imageTexts)
    {
        if (imageTexts is null)
        {
            return null;
        }

        var combined = string.Join(
            '_',
            imageTexts
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text.Trim()));
        if (combined.Length == 0)
        {
            return null;
        }

        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(Math.Min(combined.Length, MaximumTextStemLength));
        var separatorPending = false;
        foreach (var character in combined)
        {
            if (character == '_' ||
                char.IsWhiteSpace(character) ||
                char.IsControl(character) ||
                invalidCharacters.Contains(character))
            {
                separatorPending = builder.Length > 0;
                continue;
            }

            if (separatorPending && builder[^1] != '_')
            {
                builder.Append('_');
            }
            separatorPending = false;
            builder.Append(character);
            if (builder.Length >= MaximumTextStemLength)
            {
                break;
            }
        }

        var stem = builder.ToString().Trim(' ', '.', '_');
        if (stem.Length > 0 && char.IsHighSurrogate(stem[^1]))
        {
            stem = stem[..^1];
        }
        if (stem.Length == 0)
        {
            return null;
        }

        var deviceName = stem.Split('.', 2)[0];
        return ReservedWindowsNames.Contains(deviceName)
            ? $"截图_{stem}"
            : stem;
    }

    private static string CreateDateTimeStem(DateTime timestamp) =>
        $"截图_{timestamp:yyyy-MM-dd_HH-mm-ss-fff}";

    private static string CreateSequenceStem(IReadOnlySet<string> existingStems)
    {
        long maximum = -1;
        foreach (var stem in existingStems)
        {
            if (long.TryParse(stem, out var value) && value >= 0)
            {
                maximum = Math.Max(maximum, value);
            }
        }

        if (maximum < long.MaxValue)
        {
            return (maximum + 1).ToString();
        }

        for (long candidate = 0; candidate < long.MaxValue; candidate++)
        {
            var stem = candidate.ToString();
            if (!existingStems.Contains(stem))
            {
                return stem;
            }
        }
        throw new IOException("截图目录中的数字序号已经用尽。");
    }

    private static string MakeUnique(string stem, IReadOnlySet<string> existingStems)
    {
        if (!existingStems.Contains(stem))
        {
            return stem;
        }

        for (var suffix = 1; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{stem}_{suffix}";
            if (!existingStems.Contains(candidate))
            {
                return candidate;
            }
        }
        throw new IOException("无法为截图生成不重复的文件名。");
    }
}

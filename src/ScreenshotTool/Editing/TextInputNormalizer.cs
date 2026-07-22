namespace ScreenshotTool.Editing;

internal static class TextInputNormalizer
{
    public static string ToHalfWidthLatin(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return string.Create(value.Length, value, static (characters, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                var character = source[index];
                characters[index] = character switch
                {
                    >= '０' and <= '９' => (char)(character - 0xFEE0),
                    >= 'Ａ' and <= 'Ｚ' => (char)(character - 0xFEE0),
                    >= 'ａ' and <= 'ｚ' => (char)(character - 0xFEE0),
                    _ => character
                };
            }
        });
    }
}

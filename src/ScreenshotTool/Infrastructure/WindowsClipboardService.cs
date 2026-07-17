using System.Runtime.InteropServices;
using ScreenshotTool.Abstractions;

namespace ScreenshotTool.Infrastructure;

internal sealed class WindowsClipboardService : IClipboardService
{
    private const int MaximumAttempts = 5;
    private static readonly HashSet<string> ImageFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".jpeg", ".jpg", ".png", ".tif", ".tiff"
    };

    public void SetImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);

        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            try
            {
                Clipboard.SetImage(image);
                return;
            }
            catch (ExternalException) when (attempt < MaximumAttempts - 1)
            {
                // Another application may briefly own the clipboard. A short bounded retry
                // avoids making an otherwise successful save feel unreliable.
                Thread.Sleep(20 * (attempt + 1));
            }
        }
    }

    public Bitmap? GetImage() => ReadClipboard(() =>
    {
        if (Clipboard.ContainsImage())
        {
            using var image = Clipboard.GetImage();
            return image is null ? null : new Bitmap(image);
        }

        if (!Clipboard.ContainsFileDropList())
        {
            return null;
        }

        foreach (var file in Clipboard.GetFileDropList().Cast<string>())
        {
            if (!File.Exists(file) || !ImageFileExtensions.Contains(Path.GetExtension(file)))
            {
                continue;
            }

            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var image = Image.FromStream(stream);
                return new Bitmap(image);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Continue to the next copied file when one entry is unavailable or invalid.
            }
        }

        return null;
    });

    public string? GetText() => ReadClipboard(() =>
        Clipboard.ContainsText(TextDataFormat.UnicodeText)
            ? Clipboard.GetText(TextDataFormat.UnicodeText)
            : null);

    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            try
            {
                Clipboard.SetText(text, TextDataFormat.UnicodeText);
                return;
            }
            catch (ExternalException) when (attempt < MaximumAttempts - 1)
            {
                Thread.Sleep(20 * (attempt + 1));
            }
        }
    }

    private static T ReadClipboard<T>(Func<T> read)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return read();
            }
            catch (ExternalException) when (attempt < MaximumAttempts - 1)
            {
                Thread.Sleep(20 * (attempt + 1));
            }
        }
    }
}

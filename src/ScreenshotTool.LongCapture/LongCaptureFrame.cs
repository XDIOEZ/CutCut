using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScreenshotTool.LongCapture;

internal sealed class LongCaptureFrame : IDisposable
{
    private const int ExcludedRightEdgeWidth = 12;
    private readonly int[] _pixels;
    private readonly ulong[,] _rowHashes;
    private readonly uint[,] _rowSignatures;
    private readonly ushort[,] _rowTextures;

    public LongCaptureFrame(Bitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Image = Normalize(image);
        try
        {
            Width = Image.Width;
            Height = Image.Height;
            TileCount = Math.Clamp(Width / 160, 4, 10);
            if (Width < 80)
            {
                TileCount = 2;
            }

            _pixels = ReadPixels(Image);
            _rowHashes = new ulong[Height, TileCount];
            _rowSignatures = new uint[Height, TileCount];
            _rowTextures = new ushort[Height, TileCount];
            BuildRowDescriptors();
        }
        catch
        {
            Image.Dispose();
            throw;
        }
    }

    public Bitmap Image { get; }

    public int Width { get; }

    public int Height { get; }

    public int TileCount { get; }

    public ulong GetRowHash(int y, int tile) => _rowHashes[y, tile];

    public uint GetRowSignature(int y, int tile) => _rowSignatures[y, tile];

    public int GetRowTexture(int y, int tile) => _rowTextures[y, tile];

    public int GetPixel(int x, int y) => _pixels[y * Width + x];

    public Rectangle GetTileBounds(int tile)
    {
        var usableWidth = Math.Max(1, Width - Math.Min(ExcludedRightEdgeWidth, Width / 12));
        var left = tile * usableWidth / TileCount;
        var right = (tile + 1) * usableWidth / TileCount;
        return Rectangle.FromLTRB(left, 0, Math.Max(left + 1, right), Height);
    }

    public void Dispose() => Image.Dispose();

    private void BuildRowDescriptors()
    {
        Span<int> lumaSums = stackalloc int[4];
        Span<int> lumaCounts = stackalloc int[4];
        for (var tile = 0; tile < TileCount; tile++)
        {
            var bounds = GetTileBounds(tile);
            var step = Math.Max(1, bounds.Width / 28);
            for (var y = 0; y < Height; y++)
            {
                lumaSums.Clear();
                lumaCounts.Clear();
                var hash = 1469598103934665603UL;
                var texture = 0;
                var previousLuma = -1;
                for (var x = bounds.Left; x < bounds.Right; x += step)
                {
                    var pixel = GetPixel(x, y);
                    var blue = pixel & 0xFF;
                    var green = (pixel >> 8) & 0xFF;
                    var red = (pixel >> 16) & 0xFF;
                    var quantized = (uint)((red >> 3) << 10 | (green >> 3) << 5 | (blue >> 3));
                    hash ^= quantized;
                    hash *= 1099511628211UL;

                    var luma = (77 * red + 150 * green + 29 * blue) >> 8;
                    var bucket = Math.Min(
                        3,
                        (x - bounds.Left) * 4 / Math.Max(1, bounds.Width));
                    lumaSums[bucket] += luma;
                    lumaCounts[bucket]++;
                    if (previousLuma >= 0)
                    {
                        texture += Math.Abs(luma - previousLuma);
                    }
                    previousLuma = luma;
                }

                _rowHashes[y, tile] = hash;
                uint signature = 0;
                for (var bucket = 0; bucket < 4; bucket++)
                {
                    var average = lumaCounts[bucket] == 0
                        ? 0
                        : lumaSums[bucket] / lumaCounts[bucket];
                    signature |= (uint)(average >> 3) << (bucket * 5);
                }
                _rowSignatures[y, tile] = signature;
                _rowTextures[y, tile] = (ushort)Math.Min(ushort.MaxValue, texture);
            }
        }
    }

    private static Bitmap Normalize(Bitmap source)
    {
        if (source.PixelFormat == PixelFormat.Format32bppPArgb)
        {
            return source;
        }

        Bitmap? normalized = null;
        try
        {
            normalized = new Bitmap(
                source.Width,
                source.Height,
                PixelFormat.Format32bppPArgb);
            using var graphics = Graphics.FromImage(normalized);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImageUnscaled(source, Point.Empty);
            return normalized;
        }
        catch
        {
            normalized?.Dispose();
            throw;
        }
        finally
        {
            source.Dispose();
        }
    }

    private static int[] ReadPixels(Bitmap bitmap)
    {
        var bounds = new Rectangle(Point.Empty, bitmap.Size);
        var data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var pixels = new int[checked(bitmap.Width * bitmap.Height)];
            if (data.Stride == bitmap.Width * sizeof(int))
            {
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                return pixels;
            }

            for (var y = 0; y < bitmap.Height; y++)
            {
                var row = IntPtr.Add(data.Scan0, y * data.Stride);
                Marshal.Copy(row, pixels, y * bitmap.Width, bitmap.Width);
            }
            return pixels;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}

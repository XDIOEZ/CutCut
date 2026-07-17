using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ScreenshotTool.LongCapture;

internal enum LongCaptureAppendDecision
{
    Started,
    Accepted,
    NoMotion,
    Rejected,
    LimitReached
}

internal sealed record LongCaptureAppendResult(
    LongCaptureAppendDecision Decision,
    VerticalFrameMatch? Match,
    int AcceptedFrameCount,
    int EstimatedHeight,
    string Diagnostic);

internal sealed class LongCaptureStitchSession : IDisposable
{
    private readonly LongCaptureOptions _options;
    private readonly VerticalFrameMatcher _matcher;
    private readonly List<Bitmap> _segments = [];
    private LongCaptureFrame? _previous;
    private int _fixedTop;
    private int _fixedBottom;
    private int _acceptedFrameCount;
    private int _segmentHeight;
    private bool _initialized;
    private bool _disposed;

    public LongCaptureStitchSession(LongCaptureOptions? options = null)
    {
        _options = options ?? new LongCaptureOptions();
        _matcher = new VerticalFrameMatcher(_options);
    }

    public int AcceptedFrameCount => _acceptedFrameCount;

    public int EstimatedHeight => _initialized
        ? checked(_segmentHeight + _fixedBottom)
        : _previous?.Height ?? 0;

    public LongCaptureAppendResult AddFrame(Bitmap image)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var current = new LongCaptureFrame(image);
        var keepCurrent = false;
        try
        {
            if (_previous is null)
            {
                ValidateSize(current.Width, current.Height);
                _previous = current;
                keepCurrent = true;
                _acceptedFrameCount = 1;
                return Result(LongCaptureAppendDecision.Started, null, "已记录长截图首帧。");
            }

            var match = _initialized
                ? _matcher.Match(_previous, current, _fixedTop, _fixedBottom)
                : _matcher.Match(_previous, current);
            if (match.Decision == FrameMatchDecision.NoMotion)
            {
                _previous.Dispose();
                _previous = current;
                keepCurrent = true;
                return Result(LongCaptureAppendDecision.NoMotion, match, match.Diagnostic);
            }

            if (match.Decision != FrameMatchDecision.Accepted)
            {
                return Result(LongCaptureAppendDecision.Rejected, match, match.Diagnostic);
            }

            var fixedTop = _initialized ? _fixedTop : match.FixedTopHeight;
            var fixedBottom = _initialized ? _fixedBottom : match.FixedBottomHeight;
            var dynamicBottom = current.Height - fixedBottom;
            var stripTop = dynamicBottom - match.ShiftY;
            if (stripTop < fixedTop || match.ShiftY <= 0)
            {
                var invalidStrip = match with
                {
                    Decision = FrameMatchDecision.Ambiguous,
                    Diagnostic = "新内容条带超出已验证的动态区域。"
                };
                return Result(
                    LongCaptureAppendDecision.Rejected,
                    invalidStrip,
                    invalidStrip.Diagnostic);
            }

            var initialHeight = _initialized ? 0 : _previous.Height - fixedBottom;
            var prospectiveHeight = checked(
                _segmentHeight + initialHeight + match.ShiftY + fixedBottom);
            if (prospectiveHeight > _options.MaximumHeight ||
                (long)_previous.Width * prospectiveHeight > _options.MaximumPixels)
            {
                return Result(
                    LongCaptureAppendDecision.LimitReached,
                    match,
                    "长截图已达到安全尺寸上限。");
            }

            Bitmap? initialSegment = null;
            Bitmap? newSegment = null;
            try
            {
                if (!_initialized)
                {
                    initialSegment = Crop(_previous.Image, new Rectangle(
                        0,
                        0,
                        _previous.Width,
                        _previous.Height - fixedBottom));
                }

                newSegment = Crop(current.Image, new Rectangle(
                    0,
                    stripTop,
                    current.Width,
                    match.ShiftY));

                if (initialSegment is not null)
                {
                    _fixedTop = fixedTop;
                    _fixedBottom = fixedBottom;
                    AddSegment(initialSegment);
                    initialSegment = null;
                    _initialized = true;
                }

                AddSegment(newSegment);
                newSegment = null;
            }
            finally
            {
                initialSegment?.Dispose();
                newSegment?.Dispose();
            }

            _previous.Dispose();
            _previous = current;
            keepCurrent = true;
            _acceptedFrameCount++;
            return Result(LongCaptureAppendDecision.Accepted, match, match.Diagnostic);
        }
        finally
        {
            if (!keepCurrent)
            {
                current.Dispose();
            }
        }
    }

    public Bitmap BuildResult()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_previous is null)
        {
            throw new InvalidOperationException("尚未添加长截图帧。");
        }

        if (!_initialized)
        {
            return Clone(_previous.Image);
        }

        var finalHeight = checked(_segmentHeight + _fixedBottom);
        ValidateSize(_previous.Width, finalHeight);
        var result = new Bitmap(
            _previous.Width,
            finalHeight,
            PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(result);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            var destinationY = 0;
            foreach (var segment in _segments)
            {
                graphics.DrawImageUnscaled(segment, 0, destinationY);
                destinationY += segment.Height;
            }

            if (_fixedBottom > 0)
            {
                using var footer = Crop(_previous.Image, new Rectangle(
                    0,
                    _previous.Height - _fixedBottom,
                    _previous.Width,
                    _fixedBottom));
                graphics.DrawImageUnscaled(footer, 0, destinationY);
            }
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _previous?.Dispose();
        _previous = null;
        foreach (var segment in _segments)
        {
            segment.Dispose();
        }
        _segments.Clear();
    }

    private void AddSegment(Bitmap segment)
    {
        var nextHeight = checked(_segmentHeight + segment.Height);
        _segments.Add(segment);
        _segmentHeight = nextHeight;
    }

    private void ValidateSize(int width, int height)
    {
        if (width <= 0 || height <= 0 ||
            height > _options.MaximumHeight ||
            (long)width * height > _options.MaximumPixels)
        {
            throw new InvalidOperationException("长截图尺寸超过安全限制。");
        }
    }

    private LongCaptureAppendResult Result(
        LongCaptureAppendDecision decision,
        VerticalFrameMatch? match,
        string diagnostic) => new(
        decision,
        match,
        _acceptedFrameCount,
        EstimatedHeight,
        diagnostic);

    private static Bitmap Crop(Bitmap source, Rectangle bounds) =>
        source.Clone(bounds, PixelFormat.Format32bppPArgb);

    private static Bitmap Clone(Bitmap source) =>
        source.Clone(new Rectangle(Point.Empty, source.Size), PixelFormat.Format32bppPArgb);
}

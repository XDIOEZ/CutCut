using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ScreenshotTool.LongCapture;

internal sealed record BidirectionalLongCaptureAppendResult(
    LongCaptureAppendDecision Decision,
    VerticalScrollDirection Direction,
    bool ContentAppended,
    VerticalFrameMatch? Match,
    int AcceptedFrameCount,
    int EstimatedHeight,
    string Diagnostic);

/// <summary>
/// Stitches a manually scrolled viewport in either vertical direction. Page coordinates are kept
/// independently from screen coordinates, so revisiting an already captured range does not append
/// duplicate pixels and can later continue beyond either end.
/// </summary>
internal sealed class BidirectionalLongCaptureStitchSession : IDisposable
{
    private readonly LongCaptureOptions _options;
    private readonly BidirectionalVerticalFrameMatcher _matcher;
    private readonly List<PositionedSegment> _segments = [];
    private LongCaptureFrame? _currentFrame;
    private Bitmap? _header;
    private Bitmap? _footer;
    private int _fixedTop;
    private int _fixedBottom;
    private int _dynamicHeight;
    private int _currentOrigin;
    private int _minimumOrigin;
    private int _maximumOrigin;
    private int _acceptedFrameCount;
    private bool _initialized;
    private bool _disposed;

    public BidirectionalLongCaptureStitchSession(LongCaptureOptions? options = null)
    {
        _options = options ?? new LongCaptureOptions();
        _matcher = new BidirectionalVerticalFrameMatcher(_options);
    }

    public int AcceptedFrameCount => _acceptedFrameCount;

    public int EstimatedWidth => _currentFrame?.Width ?? 0;

    public int EstimatedHeight => _currentFrame is null
        ? 0
        : _initialized
            ? checked(_fixedTop + (_maximumOrigin - _minimumOrigin) + _fixedBottom)
            : _currentFrame.Height;

    public BidirectionalLongCaptureAppendResult AddFrame(Bitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var candidate = new LongCaptureFrame(image);
        var keepCandidate = false;
        try
        {
            if (_currentFrame is null)
            {
                ValidateSize(candidate.Width, candidate.Height);
                _currentFrame = candidate;
                keepCandidate = true;
                _acceptedFrameCount = 1;
                return Result(
                    LongCaptureAppendDecision.Started,
                    VerticalScrollDirection.None,
                    contentAppended: true,
                    match: null,
                    "已记录双向长截图首帧。");
            }

            var bidirectional = _initialized
                ? _matcher.Match(_currentFrame, candidate, _fixedTop, _fixedBottom)
                : _matcher.Match(_currentFrame, candidate);
            var match = bidirectional.Match;
            if (match.Decision == FrameMatchDecision.NoMotion)
            {
                ReplaceCurrent(candidate);
                keepCandidate = true;
                return Result(
                    LongCaptureAppendDecision.NoMotion,
                    VerticalScrollDirection.None,
                    contentAppended: false,
                    match,
                    "画面未移动，继续等待用户滚动。");
            }

            if (match.Decision != FrameMatchDecision.Accepted)
            {
                return Result(
                    LongCaptureAppendDecision.Rejected,
                    VerticalScrollDirection.None,
                    contentAppended: false,
                    match,
                    match.Diagnostic);
            }

            var fixedTop = _initialized ? _fixedTop : match.FixedTopHeight;
            var fixedBottom = _initialized ? _fixedBottom : match.FixedBottomHeight;
            var dynamicHeight = candidate.Height - fixedTop - fixedBottom;
            if (dynamicHeight <= match.ShiftY || dynamicHeight <= 0)
            {
                var invalid = match with
                {
                    Decision = FrameMatchDecision.Ambiguous,
                    Diagnostic = "新画面的动态内容区域不足以建立连续覆盖。"
                };
                return Result(
                    LongCaptureAppendDecision.Rejected,
                    VerticalScrollDirection.None,
                    contentAppended: false,
                    invalid,
                    invalid.Diagnostic);
            }

            var currentOrigin = _initialized ? _currentOrigin : 0;
            var nextOrigin = bidirectional.Direction == VerticalScrollDirection.Down
                ? checked(currentOrigin + match.ShiftY)
                : checked(currentOrigin - match.ShiftY);
            var minimumOrigin = _initialized ? _minimumOrigin : 0;
            var maximumOrigin = _initialized ? _maximumOrigin : dynamicHeight;
            var nextMinimum = Math.Min(minimumOrigin, nextOrigin);
            var nextMaximum = Math.Max(maximumOrigin, checked(nextOrigin + dynamicHeight));
            var prospectiveHeight = checked(
                fixedTop + (nextMaximum - nextMinimum) + fixedBottom);
            if (!CanUseSize(candidate.Width, prospectiveHeight))
            {
                return Result(
                    LongCaptureAppendDecision.LimitReached,
                    bidirectional.Direction,
                    contentAppended: false,
                    match,
                    "长截图已达到安全尺寸上限。");
            }

            if (!_initialized)
            {
                InitializeSegments(fixedTop, fixedBottom, dynamicHeight);
                minimumOrigin = _minimumOrigin;
                maximumOrigin = _maximumOrigin;
            }

            var contentAppended = AddUncoveredContent(
                candidate,
                nextOrigin,
                minimumOrigin,
                maximumOrigin);
            _minimumOrigin = nextMinimum;
            _maximumOrigin = nextMaximum;
            _currentOrigin = nextOrigin;
            ReplaceFooter(candidate);
            ReplaceCurrent(candidate);
            keepCandidate = true;
            if (contentAppended)
            {
                _acceptedFrameCount++;
            }

            return Result(
                LongCaptureAppendDecision.Accepted,
                bidirectional.Direction,
                contentAppended,
                match,
                contentAppended
                    ? match.Diagnostic
                    : $"{match.Diagnostic} 当前范围已经捕获，未重复追加。"
            );
        }
        finally
        {
            if (!keepCandidate)
            {
                candidate.Dispose();
            }
        }
    }

    public Bitmap BuildResult()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_currentFrame is null)
        {
            throw new InvalidOperationException("尚未添加长截图帧。");
        }

        if (!_initialized)
        {
            return Clone(_currentFrame.Image);
        }

        var result = new Bitmap(
            _currentFrame.Width,
            EstimatedHeight,
            PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(result);
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            if (_header is not null)
            {
                graphics.DrawImageUnscaled(_header, 0, 0);
            }

            foreach (var segment in _segments.OrderBy(segment => segment.Start))
            {
                var destinationY = checked(_fixedTop + segment.Start - _minimumOrigin);
                graphics.DrawImageUnscaled(segment.Image, 0, destinationY);
            }

            if (_footer is not null)
            {
                graphics.DrawImageUnscaled(
                    _footer,
                    0,
                    EstimatedHeight - _fixedBottom);
            }
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Builds a bounded preview directly from the captured segments. Unlike <see cref="BuildResult"/>,
    /// this never allocates the full long image before scaling it down.
    /// </summary>
    public Bitmap BuildPreview(int maximumWidth, int maximumHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (maximumWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        }
        if (maximumHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumHeight));
        }
        if (_currentFrame is null)
        {
            throw new InvalidOperationException("尚未添加长截图帧。");
        }

        var sourceWidth = _currentFrame.Width;
        var sourceHeight = EstimatedHeight;
        var scale = Math.Min(
            1d,
            Math.Min(
                (double)maximumWidth / sourceWidth,
                (double)maximumHeight / sourceHeight));
        var targetWidth = Math.Clamp(
            (int)Math.Round(sourceWidth * scale),
            1,
            maximumWidth);
        var targetHeight = Math.Clamp(
            (int)Math.Round(sourceHeight * scale),
            1,
            maximumHeight);
        if (targetWidth == sourceWidth && targetHeight == sourceHeight)
        {
            return BuildResult();
        }

        var preview = new Bitmap(
            targetWidth,
            targetHeight,
            PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(preview);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;

            if (!_initialized)
            {
                DrawScaledPart(
                    graphics,
                    _currentFrame.Image,
                    new Rectangle(0, 0, targetWidth, targetHeight));
                return preview;
            }

            if (_header is not null)
            {
                var headerBottom = MapY(_fixedTop, sourceHeight, targetHeight);
                DrawScaledPart(
                    graphics,
                    _header,
                    Rectangle.FromLTRB(0, 0, targetWidth, headerBottom));
            }

            foreach (var segment in _segments.OrderBy(segment => segment.Start))
            {
                var sourceTop = checked(_fixedTop + segment.Start - _minimumOrigin);
                var sourceBottom = checked(sourceTop + segment.Image.Height);
                var destinationTop = MapY(sourceTop, sourceHeight, targetHeight);
                var destinationBottom = MapY(sourceBottom, sourceHeight, targetHeight);
                DrawScaledPart(
                    graphics,
                    segment.Image,
                    Rectangle.FromLTRB(
                        0,
                        destinationTop,
                        targetWidth,
                        destinationBottom));
            }

            if (_footer is not null)
            {
                var footerTop = MapY(
                    sourceHeight - _fixedBottom,
                    sourceHeight,
                    targetHeight);
                DrawScaledPart(
                    graphics,
                    _footer,
                    Rectangle.FromLTRB(0, footerTop, targetWidth, targetHeight));
            }
            return preview;
        }
        catch
        {
            preview.Dispose();
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
        _currentFrame?.Dispose();
        _currentFrame = null;
        _header?.Dispose();
        _header = null;
        _footer?.Dispose();
        _footer = null;
        foreach (var segment in _segments)
        {
            segment.Image.Dispose();
        }
        _segments.Clear();
    }

    private void InitializeSegments(int fixedTop, int fixedBottom, int dynamicHeight)
    {
        if (_currentFrame is null)
        {
            throw new InvalidOperationException("首帧不存在。");
        }

        _fixedTop = fixedTop;
        _fixedBottom = fixedBottom;
        _dynamicHeight = dynamicHeight;
        _currentOrigin = 0;
        _minimumOrigin = 0;
        _maximumOrigin = dynamicHeight;
        if (fixedTop > 0)
        {
            _header = Crop(_currentFrame.Image, new Rectangle(
                0,
                0,
                _currentFrame.Width,
                fixedTop));
        }
        _segments.Add(new PositionedSegment(
            0,
            Crop(_currentFrame.Image, new Rectangle(
                0,
                fixedTop,
                _currentFrame.Width,
                dynamicHeight))));
        if (fixedBottom > 0)
        {
            _footer = Crop(_currentFrame.Image, new Rectangle(
                0,
                _currentFrame.Height - fixedBottom,
                _currentFrame.Width,
                fixedBottom));
        }
        _initialized = true;
    }

    private bool AddUncoveredContent(
        LongCaptureFrame candidate,
        int candidateOrigin,
        int previousMinimum,
        int previousMaximum)
    {
        var appended = false;
        if (candidateOrigin < previousMinimum)
        {
            var height = previousMinimum - candidateOrigin;
            _segments.Add(new PositionedSegment(
                candidateOrigin,
                Crop(candidate.Image, new Rectangle(
                    0,
                    _fixedTop,
                    candidate.Width,
                    height))));
            appended = true;
        }

        var candidateBottom = checked(candidateOrigin + _dynamicHeight);
        if (candidateBottom > previousMaximum)
        {
            var sourceOffset = previousMaximum - candidateOrigin;
            var height = candidateBottom - previousMaximum;
            _segments.Add(new PositionedSegment(
                previousMaximum,
                Crop(candidate.Image, new Rectangle(
                    0,
                    _fixedTop + sourceOffset,
                    candidate.Width,
                    height))));
            appended = true;
        }
        return appended;
    }

    private void ReplaceFooter(LongCaptureFrame candidate)
    {
        if (_fixedBottom <= 0)
        {
            return;
        }

        var footer = Crop(candidate.Image, new Rectangle(
            0,
            candidate.Height - _fixedBottom,
            candidate.Width,
            _fixedBottom));
        _footer?.Dispose();
        _footer = footer;
    }

    private void ReplaceCurrent(LongCaptureFrame candidate)
    {
        _currentFrame?.Dispose();
        _currentFrame = candidate;
    }

    private bool CanUseSize(int width, int height) =>
        width > 0 &&
        height > 0 &&
        height <= _options.MaximumHeight &&
        (long)width * height <= _options.MaximumPixels;

    private void ValidateSize(int width, int height)
    {
        if (!CanUseSize(width, height))
        {
            throw new InvalidOperationException("长截图尺寸超过安全限制。");
        }
    }

    private BidirectionalLongCaptureAppendResult Result(
        LongCaptureAppendDecision decision,
        VerticalScrollDirection direction,
        bool contentAppended,
        VerticalFrameMatch? match,
        string diagnostic) => new(
        decision,
        direction,
        contentAppended,
        match,
        _acceptedFrameCount,
        EstimatedHeight,
        diagnostic);

    private static Bitmap Crop(Bitmap source, Rectangle bounds) =>
        source.Clone(bounds, PixelFormat.Format32bppPArgb);

    private static Bitmap Clone(Bitmap source) =>
        source.Clone(new Rectangle(Point.Empty, source.Size), PixelFormat.Format32bppPArgb);

    private static int MapY(int sourceY, int sourceHeight, int targetHeight) =>
        Math.Clamp(
            (int)Math.Round(
                sourceY * (double)targetHeight / sourceHeight,
                MidpointRounding.AwayFromZero),
            0,
            targetHeight);

    private static void DrawScaledPart(
        Graphics graphics,
        Bitmap source,
        Rectangle destination)
    {
        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        graphics.DrawImage(
            source,
            destination,
            0,
            0,
            source.Width,
            source.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    private sealed record PositionedSegment(int Start, Bitmap Image);
}

namespace ScreenshotTool.Editing;

internal static class AnnotationScaling
{
    private const double ScalePerWheelStep = 1.1D;
    private const int MinimumSideLength = 8;

    public static double GetWheelScaleFactor(int wheelDelta, int wheelScrollDelta = 120)
    {
        if (wheelDelta == 0)
        {
            return 1D;
        }

        var steps = wheelScrollDelta > 0 ? wheelDelta / wheelScrollDelta : 0;
        if (steps == 0)
        {
            steps = Math.Sign(wheelDelta);
        }

        return Math.Pow(ScalePerWheelStep, steps);
    }

    public static Rectangle ScaleAt(
        Rectangle originalBounds,
        float rotationDegrees,
        Point anchor,
        double requestedFactor,
        Rectangle limits)
    {
        if (originalBounds.IsEmpty ||
            limits.IsEmpty ||
            !double.IsFinite(requestedFactor) ||
            requestedFactor <= 0D ||
            Math.Abs(requestedFactor - 1D) < 0.0001D)
        {
            return originalBounds;
        }

        var factor = ClampScaleFactor(
            originalBounds.Size,
            rotationDegrees,
            requestedFactor,
            limits.Size);
        if (Math.Abs(factor - 1D) < 0.0001D)
        {
            return originalBounds;
        }

        var scaledBounds = ScaleBoundsAt(originalBounds, anchor, factor);
        if (scaledBounds == originalBounds)
        {
            return originalBounds;
        }

        var visualBounds = AnnotationRotation.GetRotatedBounds(
            scaledBounds,
            rotationDegrees);
        scaledBounds.Offset(
            ClampAxis(
                limits.Left - visualBounds.Left,
                limits.Right - visualBounds.Right),
            ClampAxis(
                limits.Top - visualBounds.Top,
                limits.Bottom - visualBounds.Bottom));
        return scaledBounds;
    }

    public static IReadOnlyDictionary<MovableAnnotation, Rectangle> ScaleGroupAt(
        IReadOnlyList<MovableAnnotation> annotations,
        Point anchor,
        double requestedFactor,
        Rectangle limits)
    {
        if (annotations.Count == 0)
        {
            return new Dictionary<MovableAnnotation, Rectangle>();
        }

        var originalBounds = annotations.ToDictionary(
            annotation => annotation,
            annotation => annotation.Bounds);
        if (limits.IsEmpty ||
            !double.IsFinite(requestedFactor) ||
            requestedFactor <= 0D ||
            Math.Abs(requestedFactor - 1D) < 0.0001D)
        {
            return originalBounds;
        }

        var groupVisualBounds = GetVisualBounds(annotations, originalBounds);
        var factor = ClampGroupScaleFactor(
            annotations,
            groupVisualBounds,
            requestedFactor,
            limits.Size);
        if (Math.Abs(factor - 1D) < 0.0001D)
        {
            return originalBounds;
        }

        var scaledBounds = annotations.ToDictionary(
            annotation => annotation,
            annotation => ScaleBoundsAt(annotation.Bounds, anchor, factor));
        var scaledVisualBounds = GetVisualBounds(annotations, scaledBounds);
        var offset = new Point(
            ClampAxis(
                limits.Left - scaledVisualBounds.Left,
                limits.Right - scaledVisualBounds.Right),
            ClampAxis(
                limits.Top - scaledVisualBounds.Top,
                limits.Bottom - scaledVisualBounds.Bottom));
        if (!offset.IsEmpty)
        {
            foreach (var annotation in annotations)
            {
                var bounds = scaledBounds[annotation];
                bounds.Offset(offset);
                scaledBounds[annotation] = bounds;
            }
        }
        return scaledBounds;
    }

    public static float ScaleFontSize(
        float fontSize,
        Rectangle originalBounds,
        Rectangle resizedBounds)
    {
        if (originalBounds.IsEmpty ||
            resizedBounds.IsEmpty ||
            originalBounds.Size == resizedBounds.Size)
        {
            return fontSize;
        }

        var scale = Math.Sqrt(
            resizedBounds.Width / (double)originalBounds.Width *
            (resizedBounds.Height / (double)originalBounds.Height));
        return Math.Max(1F, (float)(fontSize * scale));
    }

    private static double ClampScaleFactor(
        Size originalSize,
        float rotationDegrees,
        double requestedFactor,
        Size limits)
    {
        var minimumFactor = GetMinimumScaleFactor(originalSize);
        if (requestedFactor < 1D)
        {
            return Math.Max(requestedFactor, minimumFactor);
        }

        var radians = rotationDegrees * Math.PI / 180D;
        var cosine = Math.Abs(Math.Cos(radians));
        var sine = Math.Abs(Math.Sin(radians));
        var rotatedWidth = originalSize.Width * cosine + originalSize.Height * sine;
        var rotatedHeight = originalSize.Width * sine + originalSize.Height * cosine;
        var maximumFactor = Math.Min(
            Math.Max(1, limits.Width - 2) / Math.Max(1D, rotatedWidth),
            Math.Max(1, limits.Height - 2) / Math.Max(1D, rotatedHeight));
        return Math.Min(requestedFactor, Math.Max(1D, maximumFactor));
    }

    private static double ClampGroupScaleFactor(
        IReadOnlyList<MovableAnnotation> annotations,
        Rectangle groupVisualBounds,
        double requestedFactor,
        Size limits)
    {
        if (requestedFactor < 1D)
        {
            var minimumFactor = annotations.Max(annotation =>
                GetMinimumScaleFactor(annotation.Bounds.Size));
            return Math.Max(requestedFactor, minimumFactor);
        }

        var maximumFactor = Math.Min(
            Math.Max(1, limits.Width - 2) / (double)Math.Max(1, groupVisualBounds.Width),
            Math.Max(1, limits.Height - 2) / (double)Math.Max(1, groupVisualBounds.Height));
        return Math.Min(requestedFactor, Math.Max(1D, maximumFactor));
    }

    private static double GetMinimumScaleFactor(Size originalSize)
    {
        if (originalSize.Width <= 0 || originalSize.Height <= 0)
        {
            return 1D;
        }

        var minimumWidth = Math.Min(MinimumSideLength, originalSize.Width);
        var minimumHeight = Math.Min(MinimumSideLength, originalSize.Height);
        return Math.Max(
            minimumWidth / (double)originalSize.Width,
            minimumHeight / (double)originalSize.Height);
    }

    private static Rectangle ScaleBoundsAt(Rectangle originalBounds, Point anchor, double factor)
    {
        var width = Math.Max(1, (int)Math.Round(
            originalBounds.Width * factor,
            MidpointRounding.AwayFromZero));
        var height = Math.Max(1, (int)Math.Round(
            originalBounds.Height * factor,
            MidpointRounding.AwayFromZero));
        var originalCenterX = originalBounds.Left + originalBounds.Width / 2D;
        var originalCenterY = originalBounds.Top + originalBounds.Height / 2D;
        var scaledCenterX = anchor.X + factor * (originalCenterX - anchor.X);
        var scaledCenterY = anchor.Y + factor * (originalCenterY - anchor.Y);
        return new Rectangle(
            (int)Math.Round(scaledCenterX - width / 2D, MidpointRounding.AwayFromZero),
            (int)Math.Round(scaledCenterY - height / 2D, MidpointRounding.AwayFromZero),
            width,
            height);
    }

    private static Rectangle GetVisualBounds(
        IReadOnlyList<MovableAnnotation> annotations,
        IReadOnlyDictionary<MovableAnnotation, Rectangle> bounds)
    {
        var visualBounds = AnnotationRotation.GetRotatedBounds(
            bounds[annotations[0]],
            annotations[0].RotationDegrees);
        for (var index = 1; index < annotations.Count; index++)
        {
            var annotation = annotations[index];
            visualBounds = Rectangle.Union(
                visualBounds,
                AnnotationRotation.GetRotatedBounds(
                    bounds[annotation],
                    annotation.RotationDegrees));
        }
        return visualBounds;
    }

    private static int ClampAxis(int minimumOffset, int maximumOffset) =>
        minimumOffset <= maximumOffset
            ? Math.Clamp(0, minimumOffset, maximumOffset)
            : 0;
}

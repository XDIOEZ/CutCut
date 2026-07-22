using ScreenshotTool.Core;

namespace ScreenshotTool.Editing;

internal static class AnnotationRotation
{
    public static float GetWheelDeltaDegrees(
        int wheelDelta,
        int degreesPerStep,
        int wheelScrollDelta = 120)
    {
        if (wheelDelta == 0)
        {
            return 0F;
        }

        var normalizedStep = AnnotationRotationStep.Normalize(degreesPerStep);
        var steps = wheelScrollDelta > 0 ? wheelDelta / wheelScrollDelta : 0;
        if (steps == 0)
        {
            steps = Math.Sign(wheelDelta);
        }

        return steps * normalizedStep;
    }

    public static float NormalizeDegrees(float degrees)
    {
        var normalized = degrees % 360F;
        return normalized < 0F ? normalized + 360F : normalized;
    }

    public static Point ToUnrotatedPoint(Point point, Rectangle bounds, float rotationDegrees)
    {
        if (bounds.IsEmpty || Math.Abs(rotationDegrees) < 0.001F)
        {
            return point;
        }

        var rotated = RotatePoint(point, GetCenter(bounds), -rotationDegrees);
        return Point.Round(rotated);
    }

    public static Rectangle GetRotatedBounds(Rectangle bounds, float rotationDegrees)
    {
        if (bounds.IsEmpty || Math.Abs(rotationDegrees) < 0.001F)
        {
            return bounds;
        }

        var center = GetCenter(bounds);
        var corners = new[]
        {
            RotatePoint(new PointF(bounds.Left, bounds.Top), center, rotationDegrees),
            RotatePoint(new PointF(bounds.Right, bounds.Top), center, rotationDegrees),
            RotatePoint(new PointF(bounds.Right, bounds.Bottom), center, rotationDegrees),
            RotatePoint(new PointF(bounds.Left, bounds.Bottom), center, rotationDegrees)
        };
        return Rectangle.FromLTRB(
            (int)Math.Floor(corners.Min(point => point.X)),
            (int)Math.Floor(corners.Min(point => point.Y)),
            (int)Math.Ceiling(corners.Max(point => point.X)),
            (int)Math.Ceiling(corners.Max(point => point.Y)));
    }

    public static Rectangle PreserveOppositeCorner(
        Rectangle originalBounds,
        Rectangle resizedBounds,
        StickerHitTarget resizedCorner,
        float rotationDegrees)
    {
        if (originalBounds.IsEmpty ||
            resizedBounds.IsEmpty ||
            Math.Abs(rotationDegrees) < 0.001F)
        {
            return resizedBounds;
        }

        var originalAnchor = GetOppositeHandlePoint(originalBounds, resizedCorner);
        var resizedAnchor = GetOppositeHandlePoint(resizedBounds, resizedCorner);
        var originalWorldAnchor = RotatePoint(
            originalAnchor,
            GetCenter(originalBounds),
            rotationDegrees);
        var resizedWorldAnchor = RotatePoint(
            resizedAnchor,
            GetCenter(resizedBounds),
            rotationDegrees);
        return new Rectangle(
            resizedBounds.X + (int)Math.Round(originalWorldAnchor.X - resizedWorldAnchor.X),
            resizedBounds.Y + (int)Math.Round(originalWorldAnchor.Y - resizedWorldAnchor.Y),
            resizedBounds.Width,
            resizedBounds.Height);
    }

    public static void ApplyTransform(Graphics graphics, Rectangle bounds, float rotationDegrees)
    {
        if (bounds.IsEmpty || Math.Abs(rotationDegrees) < 0.001F)
        {
            return;
        }

        var center = GetCenter(bounds);
        graphics.TranslateTransform(center.X, center.Y);
        graphics.RotateTransform(rotationDegrees);
        graphics.TranslateTransform(-center.X, -center.Y);
    }

    private static PointF GetCenter(Rectangle bounds) => new(
        bounds.Left + bounds.Width / 2F,
        bounds.Top + bounds.Height / 2F);

    private static Point GetOppositeHandlePoint(Rectangle bounds, StickerHitTarget corner) => corner switch
    {
        StickerHitTarget.TopLeft => new Point(bounds.Right, bounds.Bottom),
        StickerHitTarget.Top => new Point(bounds.Left + bounds.Width / 2, bounds.Bottom),
        StickerHitTarget.TopRight => new Point(bounds.Left, bounds.Bottom),
        StickerHitTarget.Right => new Point(bounds.Left, bounds.Top + bounds.Height / 2),
        StickerHitTarget.BottomRight => new Point(bounds.Left, bounds.Top),
        StickerHitTarget.Bottom => new Point(bounds.Left + bounds.Width / 2, bounds.Top),
        StickerHitTarget.BottomLeft => new Point(bounds.Right, bounds.Top),
        StickerHitTarget.Left => new Point(bounds.Right, bounds.Top + bounds.Height / 2),
        _ => bounds.Location
    };

    private static PointF RotatePoint(PointF point, PointF center, float rotationDegrees)
    {
        var radians = rotationDegrees * MathF.PI / 180F;
        var cosine = MathF.Cos(radians);
        var sine = MathF.Sin(radians);
        var offsetX = point.X - center.X;
        var offsetY = point.Y - center.Y;
        return new PointF(
            center.X + offsetX * cosine - offsetY * sine,
            center.Y + offsetX * sine + offsetY * cosine);
    }
}

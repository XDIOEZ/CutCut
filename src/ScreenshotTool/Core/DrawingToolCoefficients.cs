namespace ScreenshotTool.Core;

internal sealed class DrawingToolCoefficients
{
    public const decimal Minimum = 0.1M;
    public const decimal Maximum = 10M;

    public decimal Rectangle { get; set; } = 1M;
    public decimal Ellipse { get; set; } = 1M;
    public decimal ArrowBody { get; set; } = 1M;
    public decimal ArrowHeadWidth { get; set; } = 3.2M;
    public decimal ArrowHeadLength { get; set; } = 3.8M;
    public decimal Pen { get; set; } = 1M;
    public decimal Mosaic { get; set; } = 1M;

    public DrawingToolCoefficients Normalize()
    {
        Rectangle = Clamp(Rectangle, 1M);
        Ellipse = Clamp(Ellipse, 1M);
        ArrowBody = Clamp(ArrowBody, 1M);
        ArrowHeadWidth = Clamp(ArrowHeadWidth, 3.2M);
        ArrowHeadLength = Clamp(ArrowHeadLength, 3.8M);
        Pen = Clamp(Pen, 1M);
        Mosaic = Clamp(Mosaic, 1M);
        return this;
    }

    public float ApplyRectangle(float multiplier) => Apply(Rectangle, multiplier);
    public float ApplyEllipse(float multiplier) => Apply(Ellipse, multiplier);
    public float ApplyArrowBody(float multiplier) => Apply(ArrowBody, multiplier);
    public float ApplyPen(float multiplier) => Apply(Pen, multiplier);
    public float ApplyMosaic(float multiplier) => Apply(Mosaic, multiplier);

    private static decimal Clamp(decimal value, decimal fallback) =>
        value < Minimum || value > Maximum ? fallback : value;

    private static float Apply(decimal coefficient, float multiplier) =>
        (float)coefficient * multiplier;
}

using ScreenshotTool.Contracts;

namespace ScreenshotTool.TestModule;

public sealed class TestHotLoadModule : ScreenshotToolModuleBase
{
    public override string Id => "tests.hot-load";
    public override string DisplayName => "热加载测试模块";
    public override Version Version => new(1, 2, 3);

    public override IEnumerable<ICaptureFeature> CreateCaptureFeatures() =>
        [new TestCaptureFeature()];
}

public sealed class TestCaptureFeature : CaptureFeatureBase
{
    private bool _enabled;

    public override string Id => "tests.hot-load.marker";

    public override bool HandleKeyDown(KeyEventArgs e)
    {
        if (!e.Control || !e.Alt || e.KeyCode != Keys.M || !Host.HasSelection)
        {
            return false;
        }

        _enabled = !_enabled;
        Host.Invalidate(Host.Selection);
        return true;
    }

    public override void Render(Graphics graphics, CaptureRenderTarget target)
    {
        if (!_enabled)
        {
            return;
        }

        using var brush = new SolidBrush(Color.Red);
        graphics.FillRectangle(brush, Host.Selection.Left, Host.Selection.Top, 5, 5);
    }
}

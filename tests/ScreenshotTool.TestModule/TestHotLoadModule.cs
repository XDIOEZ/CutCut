using ScreenshotTool.Contracts;

namespace ScreenshotTool.TestModule;

public sealed class TestHotLoadModule : ScreenshotToolModuleBase, IModuleSettingsPageProvider
{
    public override string Id => "tests.hot-load";
    public override string DisplayName => "热加载测试模块";
    public override Version Version => new(1, 2, 3);

    public override IEnumerable<ICaptureFeature> CreateCaptureFeatures() =>
        [new TestCaptureFeature()];

    public IEnumerable<IModuleSettingsPage> CreateSettingsPages(IModuleSettingsHost host) =>
        [new TestSettingsPage(host)];
}

public sealed class TestSettingsPage : UserControl, IModuleSettingsPage
{
    public TestSettingsPage(IModuleSettingsHost host)
    {
        var enabled = host.GetBoolean("tests.hot-load.flag", false);
        Controls.Add(new Label
        {
            Text = enabled ? "测试模块设置：已开启" : "测试模块设置：已关闭",
            AutoSize = true
        });
    }

    public string Id => "tests.hot-load.settings";
    public string Title => "测试模块";
    public string Description => "验证模块设置页热加载";
    public int Order => 250;
    public Control Content => this;
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

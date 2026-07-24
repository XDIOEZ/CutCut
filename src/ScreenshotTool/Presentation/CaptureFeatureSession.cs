using System.Diagnostics;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Contracts;

namespace ScreenshotTool.Presentation;

internal sealed class CaptureFeatureSession : IDisposable
{
    private readonly List<ICaptureFeature> _features = [];
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    public CaptureFeatureSession(ICaptureFeatureCatalog catalog, ICaptureFeatureHost host)
    {
        IReadOnlyList<ICaptureFeature> features;
        try
        {
            features = catalog.CreateCaptureFeatures();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"创建截图模块会话失败：{exception}");
            return;
        }

        foreach (var feature in features)
        {
            try
            {
                feature.Attach(host);
                _features.Add(feature);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"截图模块 {feature.Id} 初始化失败：{exception}");
                feature.Dispose();
            }
        }
    }

    public bool HandleKeyDown(KeyEventArgs e) =>
        Dispatch(feature => feature.HandleKeyDown(e));

    public bool HandleMouseDown(MouseEventArgs e) =>
        Dispatch(feature => feature.HandleMouseDown(e));

    public bool HandleMouseMove(MouseEventArgs e) =>
        Dispatch(feature => feature.HandleMouseMove(e));

    public bool HandleMouseUp(MouseEventArgs e) =>
        Dispatch(feature => feature.HandleMouseUp(e));

    public void Render(Graphics graphics, CaptureRenderTarget target)
    {
        foreach (var feature in _features.ToArray())
        {
            try
            {
                feature.Render(graphics, target);
            }
            catch (Exception exception)
            {
                Disable(feature, exception);
            }
        }
    }

    public IReadOnlyList<CaptureFeatureCommand> GetToolbarCommands()
    {
        var commands = new List<CaptureFeatureCommand>();
        foreach (var feature in _features.ToArray())
        {
            if (feature is not ICaptureToolbarCommandProvider provider)
            {
                continue;
            }

            try
            {
                commands.AddRange(provider.GetToolbarCommands().Select(command =>
                    new CaptureFeatureCommand(
                        feature,
                        provider,
                        command,
                        feature is ICaptureToolbarCommandProgressProvider progressProvider &&
                        progressProvider.UsesIndeterminateProgress(command.Id))));
            }
            catch (Exception exception)
            {
                Disable(feature, exception);
            }
        }
        return commands;
    }

    public async Task<CaptureFeatureCommandExecutionResult> ExecuteToolbarCommandAsync(
        CaptureFeatureCommand command)
    {
        if (!_features.Contains(command.Feature))
        {
            return CaptureFeatureCommandExecutionResult.NotExecuted;
        }

        try
        {
            await command.Provider.ExecuteToolbarCommandAsync(
                command.Command.Id,
                _lifetimeCancellation.Token);
            return CaptureFeatureCommandExecutionResult.Success;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return CaptureFeatureCommandExecutionResult.NotExecuted;
        }
        catch (Exception exception)
        {
            Disable(command.Feature, exception);
            var cause = exception.GetBaseException();
            return new CaptureFeatureCommandExecutionResult(
                false,
                $"“{command.Command.Text}”执行失败，已在本次截图中停用。\n\n{cause.Message}");
        }
    }

    public void Dispose()
    {
        _lifetimeCancellation.Cancel();
        foreach (var feature in _features)
        {
            try
            {
                feature.Dispose();
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"截图模块 {feature.Id} 释放失败：{exception}");
            }
        }
        _features.Clear();
        _lifetimeCancellation.Dispose();
    }

    private bool Dispatch(Func<ICaptureFeature, bool> handler)
    {
        foreach (var feature in _features.ToArray())
        {
            try
            {
                if (handler(feature))
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                Disable(feature, exception);
            }
        }
        return false;
    }

    private void Disable(ICaptureFeature feature, Exception exception)
    {
        Debug.WriteLine($"截图模块 {feature.Id} 运行失败，已对当前截图会话禁用：{exception}");
        _features.Remove(feature);
        try
        {
            feature.Dispose();
        }
        catch (Exception disposeException)
        {
            Debug.WriteLine($"截图模块 {feature.Id} 释放失败：{disposeException}");
        }
    }
}

internal sealed record CaptureFeatureCommand(
    ICaptureFeature Feature,
    ICaptureToolbarCommandProvider Provider,
    CaptureToolbarCommand Command,
    bool UsesIndeterminateProgress);

internal sealed record CaptureFeatureCommandExecutionResult(bool Succeeded, string? ErrorMessage)
{
    public static CaptureFeatureCommandExecutionResult Success { get; } = new(true, null);

    public static CaptureFeatureCommandExecutionResult NotExecuted { get; } = new(false, null);
}

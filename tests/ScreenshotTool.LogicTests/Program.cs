using ScreenshotTool.Presentation;
using ScreenshotTool.Presentation.Pages;
using ScreenshotTool.Application;
using ScreenshotTool.Infrastructure;
using ScreenshotTool.Infrastructure.Modules;
using ScreenshotTool.Editing;
using ScreenshotTool.Contracts;
using ScreenshotTool.TestModule;
using ScreenshotTool.Core;
using ScreenshotTool;
using ScreenshotTool.Abstractions;
using ScreenshotTool.ScreenRecording;
using ScreenshotTool.LongCapture;
using ScreenshotTool.Ocr;
using ScreenshotTool.PaddleOcr;
using ScreenshotTool.PaddleOcr.Small;
using ScreenshotTool.PaddleOcr.Tiny;
using ScreenshotTool.QrCode;
using ZXing;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;

if (args.Length >= 3 &&
    string.Equals(args[0], "--paddle-ocr-engine-smoke", StringComparison.Ordinal))
{
    var variant = Enum.Parse<PaddleOcrVariant>(args[1], ignoreCase: true);
    using var smokeImage = new Bitmap(1080, 260);
    using (var smokeGraphics = Graphics.FromImage(smokeImage))
    using (var smokeFont = new Font("Microsoft YaHei UI", 48F, FontStyle.Regular))
    {
        smokeGraphics.Clear(Color.FromArgb(30, 34, 42));
        smokeGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        smokeGraphics.DrawString(
            "轻截 Paddle OCR 文字识别 2026",
            smokeFont,
            Brushes.White,
            34F,
            82F);
    }

    using var recognizer = new PaddleOcrRecognizer(
        Path.GetFullPath(args[2]),
        variant);
    var smokeText = await recognizer.RecognizeAsync(
        smokeImage,
        CancellationToken.None);
    Console.WriteLine($"PP-OCR {variant} result: {smokeText}");
    AssertTrue(smokeText.Contains("文字", StringComparison.Ordinal), $"PP-OCR {variant} 识别中文");
    AssertTrue(smokeText.Contains("2026", StringComparison.Ordinal), $"PP-OCR {variant} 识别数字");
    return;
}
if (args.Length >= 3 &&
    string.Equals(args[0], "--paddle-ocr-module-smoke", StringComparison.Ordinal))
{
    var variant = Enum.Parse<PaddleOcrVariant>(args[1], ignoreCase: true);
    var modulesRoot = Path.GetFullPath(args[2]);
    var commandId = variant == PaddleOcrVariant.Tiny
        ? "screenshot-tool.paddle-ocr.tiny.recognize"
        : "screenshot-tool.paddle-ocr.small.recognize";
    var expectedModuleId = variant == PaddleOcrVariant.Tiny
        ? "screenshot-tool.paddle-ocr.tiny"
        : "screenshot-tool.paddle-ocr.small";

    using var smokeImage = new Bitmap(1080, 260);
    using (var smokeGraphics = Graphics.FromImage(smokeImage))
    using (var smokeFont = new Font("Microsoft YaHei UI", 48F, FontStyle.Regular))
    {
        smokeGraphics.Clear(Color.White);
        smokeGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        smokeGraphics.DrawString(
            "轻截 PP-OCR 模块测试 2026",
            smokeFont,
            Brushes.Black,
            34F,
            82F);
    }

    using var moduleHost = new ModuleHost(modulesRoot);
    var refresh = moduleHost.Refresh();
    AssertEqual(0, refresh.Errors.Count, $"PP-OCR {variant} 模块加载无错误");
    AssertEqual(1, refresh.Modules.Count, $"PP-OCR {variant} 只加载一个独立模块");
    AssertEqual(expectedModuleId, refresh.Modules[0].Id, $"PP-OCR {variant} 模块 ID");
    var features = moduleHost.CreateCaptureFeatures();
    AssertEqual(1, features.Count, $"PP-OCR {variant} 创建截图功能");
    using var feature = features[0];
    var resultHost = new TestOcrCaptureHost(smokeImage);
    feature.Attach(resultHost);

    var packageDirectory = Directory.GetDirectories(modulesRoot).Single();
    Directory.Delete(packageDirectory, recursive: true);
    var removed = moduleHost.Refresh();
    AssertEqual(0, removed.Modules.Count, $"PP-OCR {variant} 原生依赖加载前即可删除模块目录");

    await ((ICaptureToolbarCommandProvider)feature).ExecuteToolbarCommandAsync(
        commandId,
        CancellationToken.None);
    AssertTrue(
        resultHost.ResultText?.Contains("2026", StringComparison.Ordinal) == true,
        $"PP-OCR {variant} 删除源模块后仍从活动会话快照执行真实识别");
    Console.WriteLine($"PP-OCR {variant} module result: {resultHost.ResultText}");
    return;
}
if (args.Length >= 1 && string.Equals(args[0], "--ocr-engine-smoke", StringComparison.Ordinal))
{
    using var smokeImage = new Bitmap(3600, 800);
    using (var smokeGraphics = Graphics.FromImage(smokeImage))
    using (var smokeFont = new Font("Microsoft YaHei UI", 168F, FontStyle.Bold))
    {
        smokeGraphics.Clear(Color.White);
        smokeGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        smokeGraphics.DrawString("轻截文字识别 2026", smokeFont, Brushes.Black, 72F, 220F);
    }
    if (args.Length >= 2)
    {
        smokeImage.Save(Path.GetFullPath(args[1]), System.Drawing.Imaging.ImageFormat.Png);
    }

    var smokeText = await new WindowsOcrRecognizer().RecognizeAsync(
        smokeImage,
        CancellationToken.None);
    Console.WriteLine($"OCR result: {smokeText}");
    AssertTrue(smokeText.Contains("文字", StringComparison.Ordinal), "Windows 离线 OCR 识别实际图片");
    return;
}
if (args.Length >= 1 && string.Equals(args[0], "--qr-engine-smoke", StringComparison.Ordinal))
{
    const string smokePayload = "https://example.com/轻截?q=2026";
    using var smokeImage = CreateQrCodeBitmap(smokePayload, 480);
    if (args.Length >= 2)
    {
        smokeImage.Save(Path.GetFullPath(args[1]), System.Drawing.Imaging.ImageFormat.Png);
    }

    var smokeResults = await new ZxingQrCodeScanner().ScanAsync(
        smokeImage,
        CancellationToken.None);
    Console.WriteLine($"QR result: {string.Join(" | ", smokeResults)}");
    AssertTrue(smokeResults.Contains(smokePayload), "ZXing 离线扫描实际二维码图片");
    return;
}
var selection = new Rectangle(100, 100, 400, 300);
const int tolerance = 6;

AssertTrue(
    !SingleInstanceLaunchPolicy.RequiresRestartConfirmation(ownsMutex: true),
    "旧实例未运行时直接正常启动且不显示特殊启动提示");
AssertTrue(
    SingleInstanceLaunchPolicy.RequiresRestartConfirmation(ownsMutex: false),
    "检测到旧实例时显示关闭旧实例并重新启动的特殊提示");
AssertTrue(
    ApplicationLaunchOptions.Parse(["--BACKGROUND"]).StartInBackground,
    "开机启动参数不区分大小写并请求后台启动");
AssertTrue(
    !ApplicationLaunchOptions.Parse(["--unknown"]).StartInBackground,
    "普通启动参数不会误判为后台启动");
AssertTrue(
    GitHubReleaseVersion.TryParse("v1.12.3", out var parsedReleaseVersion) &&
    parsedReleaseVersion == new Version(1, 12, 3),
    "GitHub Release 标签可解析为三段产品版本");
AssertTrue(
    !GitHubReleaseVersion.TryParse("nightly-2026-07-23", out _),
    "非正式版本标签不会被误判为可安装更新");
AssertTrue(
    GitHubAssetDigest.TryParseSha256(
        $"sha256:{new string('A', 64)}",
        out var normalizedReleaseDigest) &&
    normalizedReleaseDigest == new string('a', 64),
    "GitHub 资产 SHA-256 摘要会验证格式并统一大小写");
AssertTrue(
    !GitHubAssetDigest.TryParseSha256("sha256:not-a-digest", out _),
    "无效 GitHub 资产摘要会阻止更新");

const string updateReleaseJson = """
    {
      "tag_name": "v1.12.0",
      "name": "轻截 v1.12.0",
      "html_url": "https://github.com/XDIOEZ/CutCut/releases/tag/v1.12.0",
      "published_at": "2026-07-23T02:00:00Z",
      "assets": [
        {
          "name": "complete-lightweight-win-x64.zip",
          "browser_download_url": "https://github.com/XDIOEZ/CutCut/releases/download/v1.12.0/complete-lightweight-win-x64.zip",
          "size": 1250000,
          "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        },
        {
          "name": "complete-portable-win-x64.zip",
          "browser_download_url": "https://github.com/XDIOEZ/CutCut/releases/download/v1.12.0/complete-portable-win-x64.zip",
          "size": 62000000,
          "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
        }
      ]
    }
    """;
using (var lightweightClient = new HttpClient(
           new StaticJsonHttpMessageHandler(updateReleaseJson)))
using (var lightweightUpdateService = new GitHubReleaseApplicationUpdateService(
           new Version(1, 11, 0),
           Path.GetTempPath(),
           Path.Combine(Path.GetTempPath(), "ScreenshotTool.exe"),
           lightweightClient,
           desktopRuntimeAvailable: () => true))
{
    var updateCheck = await lightweightUpdateService.CheckForUpdatesAsync(
        CancellationToken.None);
    AssertEqual(new Version(1, 12, 0), updateCheck.LatestVersion,
        "更新检查读取最新正式版本");
    AssertTrue(updateCheck.AvailableUpdate is not null,
        "GitHub 版本高于当前版本时提供更新");
    AssertEqual(
        ApplicationUpdatePackageKind.Lightweight,
        updateCheck.AvailableUpdate!.PackageKind,
        "已安装桌面运行库时选择轻量更新包");
    AssertEqual(
        new string('a', 64),
        updateCheck.AvailableUpdate.PackageSha256,
        "更新检查使用 GitHub 提供的轻量包 SHA-256");
}
using (var portableClient = new HttpClient(
           new StaticJsonHttpMessageHandler(updateReleaseJson)))
using (var portableUpdateService = new GitHubReleaseApplicationUpdateService(
           new Version(1, 11, 0),
           Path.GetTempPath(),
           Path.Combine(Path.GetTempPath(), "ScreenshotTool.exe"),
           portableClient,
           desktopRuntimeAvailable: () => false))
{
    var updateCheck = await portableUpdateService.CheckForUpdatesAsync(
        CancellationToken.None);
    AssertEqual(
        ApplicationUpdatePackageKind.Portable,
        updateCheck.AvailableUpdate!.PackageKind,
        "未安装桌面运行库时选择自带运行库的便携更新包");
    AssertEqual(62_000_000L, updateCheck.AvailableUpdate.PackageSize,
        "便携更新包使用 Release 中的真实文件大小");
}

var updateArchiveTestRoot = Path.Combine(
    Path.GetTempPath(),
    $"LightShotUpdateArchiveTests-{Guid.NewGuid():N}");
Directory.CreateDirectory(updateArchiveTestRoot);
try
{
    var validArchivePath = Path.Combine(updateArchiveTestRoot, "valid.zip");
    using (var archive = ZipFile.Open(validArchivePath, ZipArchiveMode.Create))
    {
        var executableEntry = archive.CreateEntry("ScreenshotTool.exe");
        await using (var writer = new StreamWriter(
                         executableEntry.Open(),
                         new UTF8Encoding(false),
                         leaveOpen: false))
        {
            await writer.WriteAsync("test executable");
        }
        var moduleEntry = archive.CreateEntry(
            "Modules/LongCapture/ScreenshotTool.LongCapture.dll");
        await using (var writer = new StreamWriter(
                         moduleEntry.Open(),
                         new UTF8Encoding(false),
                         leaveOpen: false))
        {
            await writer.WriteAsync("test module");
        }
    }

    var extractedDirectory = Path.Combine(updateArchiveTestRoot, "valid-payload");
    await UpdateArchiveExtractor.ExtractAsync(
        validArchivePath,
        extractedDirectory,
        maximumEntries: 20,
        maximumExtractedBytes: 1024 * 1024,
        CancellationToken.None);
    AssertTrue(
        File.Exists(Path.Combine(extractedDirectory, "ScreenshotTool.exe")),
        "更新压缩包会解压根目录程序");
    AssertTrue(
        File.Exists(Path.Combine(
            extractedDirectory,
            "Modules",
            "LongCapture",
            "ScreenshotTool.LongCapture.dll")),
        "更新压缩包会保持模块目录结构");

    var traversalArchivePath = Path.Combine(updateArchiveTestRoot, "traversal.zip");
    using (var archive = ZipFile.Open(traversalArchivePath, ZipArchiveMode.Create))
    {
        var traversalEntry = archive.CreateEntry("../escaped.txt");
        await using var writer = new StreamWriter(
            traversalEntry.Open(),
            new UTF8Encoding(false),
            leaveOpen: false);
        await writer.WriteAsync("must not escape");
    }

    var traversalRejected = false;
    try
    {
        await UpdateArchiveExtractor.ExtractAsync(
            traversalArchivePath,
            Path.Combine(updateArchiveTestRoot, "traversal-payload"),
            maximumEntries: 20,
            maximumExtractedBytes: 1024 * 1024,
            CancellationToken.None);
    }
    catch (ApplicationUpdateException)
    {
        traversalRejected = true;
    }
    AssertTrue(traversalRejected, "更新压缩包目录穿越路径会被拒绝");
    AssertTrue(
        !File.Exists(Path.Combine(updateArchiveTestRoot, "escaped.txt")),
        "恶意更新压缩包不能写出暂存目录");

    var prunePayload = Path.Combine(updateArchiveTestRoot, "prune-payload");
    var installedApplication = Path.Combine(updateArchiveTestRoot, "installed");
    Directory.CreateDirectory(Path.Combine(prunePayload, "Modules", "Ocr"));
    Directory.CreateDirectory(Path.Combine(prunePayload, "Modules", "QrCode"));
    Directory.CreateDirectory(Path.Combine(installedApplication, "Modules", "Ocr"));
    UpdatePayloadPruner.PreserveMissingModuleChoices(prunePayload, installedApplication);
    AssertTrue(
        Directory.Exists(Path.Combine(prunePayload, "Modules", "Ocr")),
        "更新会保留并升级仍然安装的插件");
    AssertTrue(
        !Directory.Exists(Path.Combine(prunePayload, "Modules", "QrCode")),
        "更新不会重新安装用户已永久删除的插件");

    var applyRoot = Path.Combine(updateArchiveTestRoot, "apply");
    var applySource = Path.Combine(applyRoot, "payload");
    var applyTarget = Path.Combine(applyRoot, "installed");
    var applyResultPath = Path.Combine(applyRoot, "result.json");
    var applyScriptPath = Path.Combine(applyRoot, "ApplyUpdate.ps1");
    Directory.CreateDirectory(applySource);
    Directory.CreateDirectory(applyTarget);
    await File.WriteAllTextAsync(
        Path.Combine(applySource, "existing.txt"),
        "new version",
        Encoding.UTF8);
    await File.WriteAllTextAsync(
        Path.Combine(applySource, "created.txt"),
        "created by update",
        Encoding.UTF8);
    await File.WriteAllTextAsync(
        Path.Combine(applyTarget, "existing.txt"),
        "old version",
        Encoding.UTF8);
    var restartExecutablePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "whoami.exe");
    await File.WriteAllTextAsync(
        applyScriptPath,
        PowerShellUpdateScript.Content,
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    var powershellPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");
    var applyStartInfo = new System.Diagnostics.ProcessStartInfo
    {
        FileName = powershellPath,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardError = true,
        RedirectStandardOutput = true
    };
    foreach (var argument in new[]
             {
                 "-NoProfile",
                 "-NonInteractive",
                 "-ExecutionPolicy",
                 "Bypass",
                 "-File",
                 applyScriptPath,
                 "-ProcessId",
                 int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                 "-SourceDirectory",
                 applySource,
                 "-TargetDirectory",
                 applyTarget,
                 "-ExecutablePath",
                 restartExecutablePath,
                 "-ResultPath",
                 applyResultPath,
                 "-Version",
                 "1.12.0"
             })
    {
        applyStartInfo.ArgumentList.Add(argument);
    }
    using (var applyProcess = System.Diagnostics.Process.Start(applyStartInfo) ??
                              throw new InvalidOperationException("更新脚本测试进程未启动"))
    {
        using var applyTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await applyProcess.WaitForExitAsync(applyTimeout.Token);
        var applyError = await applyProcess.StandardError.ReadToEndAsync(
            applyTimeout.Token);
        AssertEqual(
            0,
            applyProcess.ExitCode,
            $"退出后更新脚本可执行（{applyError.Trim()}）");
    }

    AssertEqual(
        "new version",
        await File.ReadAllTextAsync(Path.Combine(applyTarget, "existing.txt")),
        "退出后更新脚本会覆盖已有程序文件");
    AssertTrue(
        File.Exists(Path.Combine(applyTarget, "created.txt")),
        "退出后更新脚本会安装新增程序文件");
    using (var applyResultDocument = JsonDocument.Parse(
               await File.ReadAllTextAsync(applyResultPath)))
    {
        AssertTrue(
            applyResultDocument.RootElement.GetProperty("succeeded").GetBoolean(),
            "退出后更新脚本会启动目标程序并记录成功结果供界面显示");
    }
}
finally
{
    Directory.Delete(updateArchiveTestRoot, recursive: true);
}
var runEntryStore = new TestStartupEntryStore();
var startupRegistration = new StartupRegistrationService(
    runEntryStore,
    @"C:\Program Files\LightShotCN\ScreenshotTool.exe");
AssertTrue(!startupRegistration.IsEnabled, "没有当前用户启动项时默认关闭开机启动");
startupRegistration.SetEnabled(enabled: true);
AssertEqual("LightShotCN", runEntryStore.Name ?? string.Empty, "开机启动项使用稳定名称");
AssertEqual(
    "\"C:\\Program Files\\LightShotCN\\ScreenshotTool.exe\" --background",
    runEntryStore.Value ?? string.Empty,
    "开机启动命令正确引用带空格路径并请求后台运行");
AssertTrue(startupRegistration.IsEnabled, "写入匹配命令后识别为开机启动已开启");
runEntryStore.Value = "\"C:\\OldLocation\\ScreenshotTool.exe\" --background";
AssertTrue(!startupRegistration.IsEnabled, "程序移动后不会把旧路径误认为当前开机启动项");
startupRegistration.SetEnabled(enabled: false);
AssertTrue(runEntryStore.Value is null, "关闭开机启动会永久移除当前用户启动项");

var savedScreenshotTestRoot = Path.Combine(
    Path.GetTempPath(),
    $"LightShotSavedScreenshotTests-{Guid.NewGuid():N}");
var outsideScreenshotTestRoot = Path.Combine(
    Path.GetTempPath(),
    $"LightShotOutsideScreenshotTests-{Guid.NewGuid():N}");
Directory.CreateDirectory(savedScreenshotTestRoot);
Directory.CreateDirectory(outsideScreenshotTestRoot);
try
{
    var savedScreenshotPath = Path.Combine(savedScreenshotTestRoot, "可编辑截图.png");
    using (var savedScreenshot = new Bitmap(48, 32))
    {
        savedScreenshot.SetPixel(10, 12, Color.CornflowerBlue);
        savedScreenshot.Save(
            savedScreenshotPath,
            System.Drawing.Imaging.ImageFormat.Png);
    }

    string? recycledScreenshotPath = null;
    var savedScreenshotService = new SavedScreenshotService(path =>
    {
        recycledScreenshotPath = path;
        File.Delete(path);
    });
    AssertTrue(
        savedScreenshotService.IsSupportedImage(savedScreenshotPath),
        "截图管理识别 PNG 图片");
    AssertTrue(
        !savedScreenshotService.IsSupportedImage(
            Path.Combine(savedScreenshotTestRoot, "说明.txt")),
        "截图管理拒绝非图片文件");
    using (var editableScreenshot = savedScreenshotService.LoadForEditing(
               savedScreenshotTestRoot,
               savedScreenshotPath))
    {
        AssertEqual(new Size(48, 32), editableScreenshot.Size, "编辑已有截图保留原始像素尺寸");
        AssertEqual(
            Color.CornflowerBlue.ToArgb(),
            editableScreenshot.GetPixel(10, 12).ToArgb(),
            "编辑已有截图保留原始像素内容");
    }
    using (File.Open(
               savedScreenshotPath,
               FileMode.Open,
               FileAccess.ReadWrite,
               FileShare.None))
    {
    }

    var outsideScreenshotPath = Path.Combine(outsideScreenshotTestRoot, "目录外截图.png");
    File.Copy(savedScreenshotPath, outsideScreenshotPath);
    var outsidePathRejected = false;
    try
    {
        using var _ = savedScreenshotService.LoadForEditing(
            savedScreenshotTestRoot,
            outsideScreenshotPath);
    }
    catch (InvalidOperationException)
    {
        outsidePathRejected = true;
    }
    AssertTrue(outsidePathRejected, "编辑拒绝截图保存目录之外的文件");

    savedScreenshotService.MoveToRecycleBin(
        savedScreenshotTestRoot,
        savedScreenshotPath);
    AssertEqual(
        Path.GetFullPath(savedScreenshotPath),
        recycledScreenshotPath ?? string.Empty,
        "删除把经过校验的截图路径交给回收站");
    AssertTrue(!File.Exists(savedScreenshotPath), "删除后截图不再留在保存目录");
}
finally
{
    Directory.Delete(savedScreenshotTestRoot, recursive: true);
    Directory.Delete(outsideScreenshotTestRoot, recursive: true);
}

var wideEditSelection = CaptureOverlayForm.CalculateInitialEditSelection(
    new Rectangle(0, 0, 1000, 700),
    new Size(1600, 900));
AssertTrue(
    new Rectangle(0, 0, 1000, 700).Contains(wideEditSelection),
    "已有截图编辑画布保持在当前屏幕范围内");
AssertEqual(
    new Point(
        (1000 - wideEditSelection.Width) / 2,
        (700 - wideEditSelection.Height) / 2),
    wideEditSelection.Location,
    "已有截图编辑画布在当前屏幕居中");
AssertEqual(
    new Rectangle(400, 300, 200, 100),
    CaptureOverlayForm.CalculateInitialEditSelection(
        new Rectangle(0, 0, 1000, 700),
        new Size(200, 100)),
    "小尺寸已有截图保持原尺寸并居中");

var currentProductVersion = new Version(1, 10, 0, 0);
AssertEqual(
    StartupWorkspaceReason.FirstRun,
    StartupWorkspacePolicy.DetermineReason(lastLaunchedVersion: null, currentProductVersion),
    "没有启动版本标记时识别为首次运行");
AssertEqual(
    StartupWorkspaceReason.None,
    StartupWorkspacePolicy.DetermineReason("1.10.0", currentProductVersion),
    "同一产品版本不重复打开设置工作台");
AssertEqual(
    StartupWorkspaceReason.VersionChanged,
    StartupWorkspacePolicy.DetermineReason("1.9.3", currentProductVersion),
    "应用更新后识别版本变化");
AssertEqual(
    StartupWorkspaceReason.VersionChanged,
    StartupWorkspacePolicy.DetermineReason("invalid", currentProductVersion),
    "无效版本标记按版本变化处理");
AssertEqual(
    "1.10.0",
    StartupWorkspacePolicy.CreateVersionMarker(currentProductVersion),
    "启动标记使用主次补丁三段版本号");
AssertEqual(
    StartupWorkspaceReason.None,
    StartupWorkspacePolicy.DetermineReason("1.10.0.99", currentProductVersion),
    "程序集修订号变化不误判为产品更新");
AssertTrue(
    !StartupWorkspacePolicy.ShouldStartMinimized(
        startMinimized: true,
        StartupWorkspaceReason.FirstRun),
    "首次运行覆盖启动后最小化并显示设置工作台");
AssertTrue(
    !StartupWorkspacePolicy.ShouldStartMinimized(
        startMinimized: true,
        StartupWorkspaceReason.VersionChanged),
    "版本更新覆盖启动后最小化并显示设置工作台");
AssertTrue(
    StartupWorkspacePolicy.ShouldStartMinimized(
        startMinimized: true,
        StartupWorkspaceReason.None),
    "同版本后续启动恢复用户最小化偏好");
AssertTrue(
    StartupWorkspacePolicy.ShouldStartMinimized(
        startMinimized: false,
        StartupWorkspaceReason.None,
        startInBackground: true),
    "Windows 开机启动始终进入托盘而不打扰用户");
AssertTrue(
    !StartupWorkspacePolicy.ShouldStartMinimized(
        startMinimized: false,
        StartupWorkspaceReason.VersionChanged,
        startInBackground: true),
    "版本更新仍优先打开设置工作台");
AssertEqual(
    TextEditorEnterAction.Commit,
    TextEditorEnterPolicy.Resolve(controlPressed: false),
    "文字输入时普通回车结束编辑");
AssertEqual(
    TextEditorEnterAction.InsertLineBreak,
    TextEditorEnterPolicy.Resolve(controlPressed: true),
    "文字输入时只有 Ctrl+回车插入换行");

using (var textEditorHost = new Panel
{
    Size = new Size(260, 110),
    BackColor = Color.CornflowerBlue
})
using (var textEditor = new TransparentTextEditorControl(
           new Point(12, 18),
           new Size(220, 72),
           Color.Red,
           textEditorHost.ClientRectangle))
{
    textEditorHost.Controls.Add(textEditor);
    AssertEqual(0, textEditor.BackColor.A, "文字输入框使用透明背景");
    textEditor.InsertText("透明文字");
    textEditor.InsertText("\r\n可输入");
    AssertEqual("透明文字\n可输入", textEditor.Text, "透明文字输入框支持多行文字");
    textEditor.InsertText("Ａｂｃ１２３，。！？");
    AssertEqual(
        "透明文字\n可输入Abc123，。！？",
        textEditor.Text,
        "文字输入框自动把全角英文和数字转为半角并保留中文标点");
    AssertEqual(textEditor.Text.Length, textEditor.CaretIndex, "文字输入后光标停在末尾");
    AssertEqual(
        Rectangle.FromLTRB(
            textEditor.Left + 4,
            textEditor.Top + 3,
            textEditor.Right - 4,
            textEditor.Bottom - 3),
        textEditor.TextContentBounds,
        "文字提交使用输入状态的实际内容区域");
    var longCaptureFontSize = TextEditorCommitLayout.CalculateImageFontSize(
        textEditor.TextFontSize,
        0.4D);
    AssertTrue(
        Math.Abs(textEditor.TextFontSize - longCaptureFontSize * 0.4F) < 0.001F,
        "长截图文字提交后保持输入状态的视觉字号");
    using var renderedEditor = new Bitmap(textEditorHost.Width, textEditorHost.Height);
    textEditorHost.DrawToBitmap(renderedEditor, textEditorHost.ClientRectangle);
    AssertEqual(Color.CornflowerBlue.ToArgb(), renderedEditor.GetPixel(220, 78).ToArgb(), "文字输入框透明区域透出父画面");
}

AssertEqual(18F, TextToolSizing.CalculateVisualFontSize(4), "默认粗细保持原有 18 像素文字大小");
AssertEqual(9F, TextToolSizing.CalculateVisualFontSize(2), "较小粗细生成较小文字");
AssertEqual(36F, TextToolSizing.CalculateVisualFontSize(8), "较大粗细生成较大文字");
AssertEqual(8F, TextToolSizing.CalculateVisualFontSize(1), "文字大小保留最低可读字号");
using (var widthDrivenTextEditor = new TransparentTextEditorControl(
           new Point(10, 10),
           new Size(120, 38),
           Color.White,
           new Rectangle(0, 0, 420, 220),
           textFontSize: TextToolSizing.CalculateVisualFontSize(2)))
{
    AssertEqual(9F, widthDrivenTextEditor.TextFontSize, "文字编辑器使用当前粗细对应的初始字号");
    widthDrivenTextEditor.InsertText("粗细字段实时改变文字元素大小");
    widthDrivenTextEditor.SetTextFontSize(TextToolSizing.CalculateVisualFontSize(8));
    AssertEqual(36F, widthDrivenTextEditor.TextFontSize, "编辑文字时调整粗细会立即更新字号");
    AssertTrue(widthDrivenTextEditor.IsTextFullyVisible, "实时放大字号后文字编辑框重新换行并完整显示");
}
using (var pastedTextEditor = new TransparentTextEditorControl(
           new Point(20, 30),
           new Size(160, 54),
           Color.White,
           new Rectangle(0, 0, 420, 220),
           textFontSize: 17F,
           textPadding: new Size(8, 6),
           fontStyle: FontStyle.Regular))
{
    AssertEqual(new Rectangle(28, 36, 144, 42), pastedTextEditor.TextContentBounds, "粘贴文字重新编辑使用原有文本内边距");
    AssertEqual(FontStyle.Regular, pastedTextEditor.Font.Style, "粘贴文字重新编辑保留常规字体样式");
}
AssertEqual(
    90F,
    TextEditorCommitLayout.CalculateImageFontSize(
        TextToolSizing.CalculateVisualFontSize(8),
        0.4D),
    "长截图按缩放比例提交粗细对应的文字字号");
using (var customPastedText = new PastedTextAnnotation(
           new Rectangle(10, 10, 160, 60),
           "粗细控制粘贴文字",
           TextToolSizing.CalculateVisualFontSize(8)))
{
    AssertEqual(36F, customPastedText.FontSize, "粘贴文字元素接受粗细对应的字号");
}

using (var smallTextDocument = new AnnotationDocument())
using (var largeTextDocument = new AnnotationDocument())
using (var textSizeSource = CreateSolidBitmap(new Size(240, 120), Color.Black))
{
    var textBounds = new Rectangle(10, 10, 220, 100);
    smallTextDocument.Add(new TextAnnotation(
        textBounds,
        "文字大小",
        Color.White,
        TextToolSizing.CalculateVisualFontSize(2)));
    largeTextDocument.Add(new TextAnnotation(
        textBounds,
        "文字大小",
        Color.White,
        TextToolSizing.CalculateVisualFontSize(8)));
    using var smallTextExport = RenderDocumentSelection(
        smallTextDocument,
        textSizeSource,
        new Rectangle(Point.Empty, textSizeSource.Size));
    using var largeTextExport = RenderDocumentSelection(
        largeTextDocument,
        textSizeSource,
        new Rectangle(Point.Empty, textSizeSource.Size));
    AssertTrue(
        CountNonBlackPixels(largeTextExport, textBounds) >
        CountNonBlackPixels(smallTextExport, textBounds),
        "最终导出位图中的文字大小受粗细字段影响");
}

using (var textReeditEditor = new CaptureAnnotationEditor())
using (var textReeditSource = CreateSolidBitmap(new Size(320, 220), Color.Black))
using (var hiddenTextPreview = CreateSolidBitmap(new Size(320, 220), Color.Black))
{
    var original = textReeditEditor.AddAndSelect(new TextAnnotation(
        new Rectangle(40, 50, 150, 48),
        "原始文字",
        Color.CornflowerBlue,
        18F));
    original.RotateBy(25F);
    AssertTrue(
        textReeditEditor.TryBeginTextEdit(original, out var descriptor),
        "选中的工具栏文字可进入重新编辑会话");
    AssertTrue(descriptor is not null, "重新编辑返回原文字描述");
    AssertEqual("原始文字", descriptor!.Text, "重新编辑载入原文字内容");
    AssertEqual(Color.CornflowerBlue.ToArgb(), descriptor.ForegroundColor.ToArgb(), "重新编辑载入原文字颜色");
    AssertEqual(18F, descriptor.FontSize, "重新编辑载入原字号");
    AssertEqual(TextAnnotationEditorBoundsMode.Content, descriptor.BoundsMode, "透明文字以内容区域回写边界");

    using (var graphics = Graphics.FromImage(hiddenTextPreview))
    {
        textReeditEditor.Render(graphics, textReeditSource);
    }
    AssertTrue(
        !ContainsPixelDifferentFrom(
            hiddenTextPreview,
            new Rectangle(20, 20, 210, 120),
            Color.Black),
        "重新编辑期间隐藏原文字预览以避免重影");

    var updated = textReeditEditor.EndTextEdit(
        commit: true,
        editorOuterBounds: new Rectangle(56, 66, 188, 60),
        editorContentBounds: new Rectangle(60, 69, 180, 54),
        text: "修改后的文字",
        fontSize: 24F);
    AssertTrue(ReferenceEquals(original, updated), "重新编辑原地更新同一个文字元素");
    AssertEqual(1, textReeditEditor.Document.Count, "重新编辑不会新增重复文字元素");
    AssertEqual("修改后的文字", original.Text, "重新编辑提交新内容");
    AssertEqual(new Rectangle(60, 69, 180, 54), original.Bounds, "透明文字按编辑内容区域更新边界");
    AssertEqual(24F, original.FontSize, "重新编辑提交新字号");
    AssertEqual(25F, original.RotationDegrees, "重新编辑保留文字元素旋转角度");

    AssertTrue(textReeditEditor.TryBeginTextEdit(original, out _), "更新后的文字可再次编辑");
    textReeditEditor.EndTextEdit(
        commit: false,
        editorOuterBounds: new Rectangle(10, 10, 50, 30),
        editorContentBounds: new Rectangle(14, 13, 42, 24),
        text: "不应保存",
        fontSize: 9F);
    AssertEqual("修改后的文字", original.Text, "取消重新编辑会恢复原内容");
    AssertEqual(new Rectangle(60, 69, 180, 54), original.Bounds, "取消重新编辑会恢复原边界");
}

using (var pastedTextReeditEditor = new CaptureAnnotationEditor())
{
    var original = pastedTextReeditEditor.AddAndSelect(new PastedTextAnnotation(
        new Rectangle(30, 40, 180, 62),
        "原粘贴文字",
        17F));
    AssertTrue(
        pastedTextReeditEditor.TryBeginTextEdit(original, out var descriptor),
        "选中的粘贴文字也可重新编辑");
    AssertEqual(TextAnnotationEditorBoundsMode.Outer, descriptor!.BoundsMode, "粘贴文字以外框回写边界");
    AssertEqual(new Size(8, 6), descriptor.TextPadding, "粘贴文字重新编辑保留文本内边距");
    pastedTextReeditEditor.EndTextEdit(
        commit: true,
        editorOuterBounds: new Rectangle(36, 44, 210, 70),
        editorContentBounds: new Rectangle(44, 50, 194, 58),
        text: "修改后的粘贴文字",
        fontSize: 20F);
    AssertEqual("修改后的粘贴文字", original.Text, "粘贴文字提交新内容");
    AssertEqual(new Rectangle(36, 44, 210, 70), original.Bounds, "粘贴文字保留外框语义");
    AssertEqual(20F, original.FontSize, "粘贴文字提交新字号");
}

using (var autoSizeEditor = new TransparentTextEditorControl(
           new Point(240, 20),
           new Size(120, 38),
           Color.Red,
           new Rectangle(0, 0, 360, 220)))
{
    var initialSize = autoSizeEditor.Size;
    autoSizeEditor.InsertText("这是一段触及截图右边界后需要自动换行的长文字内容。");
    AssertTrue(
        autoSizeEditor.Text.Contains('\n'),
        "长文本触及截图右边界后自动插入换行");
    AssertTrue(autoSizeEditor.Height > initialSize.Height, "自动换行后文字框自动增加高度");
    AssertTrue(autoSizeEditor.IsTextFullyVisible, "文字框扩展和换行以完整显示全部文字为准");
    autoSizeEditor.InsertText("\n第二行");
    AssertTrue(autoSizeEditor.Height > initialSize.Height, "Ctrl+回车换行后文字框自动适应高度");
    AssertTrue(autoSizeEditor.IsTextFullyVisible, "主动换行后的全部文字仍完整位于文字框内");
    AssertTrue(new Rectangle(0, 0, 360, 220).Contains(autoSizeEditor.Bounds), "自动扩充后的输入框保持在截图范围内");
}

using (var rightEdgeEditor = new TransparentTextEditorControl(
           new Point(41, 20),
           new Size(120, 38),
           Color.Red,
           new Rectangle(0, 0, 1000, 300)))
{
    rightEdgeEditor.InsertText(string.Concat(Enumerable.Repeat("测试测试测试　", 24)));
    AssertTrue(
        rightEdgeEditor.Right <= 1000 && rightEdgeEditor.Right >= 940,
        "特定位置长文本优先扩大文字框并在截图右边界前保留字形安全余量");
    AssertTrue(rightEdgeEditor.Text.Contains('\n'), "连续中文抵达截图右边界后逐字符自动换行");
    AssertTrue(rightEdgeEditor.IsTextFullyVisible, "连续中文每一行均完整显示且不会被右边框割除");
    AssertTrue(rightEdgeEditor.Height > 38, "连续中文自动换行后文字框同步增加高度");
}

var textClipboard = new TestTextClipboardService();
using (var selectionEditor = new TransparentTextEditorControl(
           Point.Empty,
           new Size(180, 38),
           Color.Red,
           new Rectangle(0, 0, 360, 220),
           textClipboard))
{
    selectionEditor.InsertText("可选择的文字");
    selectionEditor.SelectAllText();
    AssertEqual("可选择的文字", selectionEditor.SelectedText, "Ctrl+A 全选输入框内全部文字");
    selectionEditor.InsertText("替换");
    AssertEqual("替换", selectionEditor.Text, "输入内容会替换当前文字选区");
    selectionEditor.InsertText("并复制");
    selectionEditor.SelectText(2, 3);
    selectionEditor.CopySelectedText();
    AssertEqual("并复制", textClipboard.Text!, "Ctrl+C 复制选中文字");
    selectionEditor.CutSelectedText();
    AssertEqual("替换", selectionEditor.Text, "Ctrl+X 剪切并删除选中文字");
    textClipboard.Text = "粘贴文字";
    selectionEditor.SelectAllText();
    selectionEditor.PasteClipboardText();
    AssertEqual("粘贴文字", selectionEditor.Text, "Ctrl+V 替换选区并粘贴文字");
}

using (var noWrapSource = CreateSolidBitmap(new Size(220, 90), Color.CornflowerBlue))
using (var noWrapGraphics = Graphics.FromImage(noWrapSource))
{
    var noWrapText = new TextAnnotation(
        new Rectangle(10, 10, 90, 60),
        "这是一段超过文字框宽度但不能自动换行的文字",
        Color.Red,
        18F);
    noWrapText.Render(noWrapGraphics, noWrapSource);
    AssertTrue(
        !ContainsPixelDifferentFrom(noWrapSource, new Rectangle(10, 39, 90, 31), Color.CornflowerBlue),
        "提交后的长文字不会在左下角自动折出多余字符");
}

using (var explicitLineBreakSource = CreateSolidBitmap(new Size(220, 90), Color.CornflowerBlue))
using (var explicitLineBreakGraphics = Graphics.FromImage(explicitLineBreakSource))
{
    var multilineText = new TextAnnotation(
        new Rectangle(10, 10, 180, 60),
        "第一行\n第二行",
        Color.Red,
        18F);
    multilineText.Render(explicitLineBreakGraphics, explicitLineBreakSource);
    AssertTrue(
        ContainsPixelDifferentFrom(explicitLineBreakSource, new Rectangle(10, 30, 180, 30), Color.CornflowerBlue),
        "Ctrl+回车产生的显式换行仍会渲染第二行");
}

AssertTrue(TextEditorInteraction.IsMoveBorder(new Size(180, 80), new Point(2, 40), 5), "文字编辑框左边界进入移动区域");
AssertTrue(TextEditorInteraction.IsMoveBorder(new Size(180, 80), new Point(90, 77), 5), "文字编辑框下边界进入移动区域");
AssertTrue(!TextEditorInteraction.IsMoveBorder(new Size(180, 80), new Point(90, 40), 5), "文字编辑框正文区域保持输入操作");
AssertEqual(
    new Rectangle(220, 140, 140, 80),
    TextEditorInteraction.Move(
        new Rectangle(40, 30, 140, 80),
        new Point(500, 500),
        new Rectangle(0, 0, 360, 220)),
    "编辑中的文字框拖动限制在截图范围内");

var redrawGuard = new SelectionRedrawGuard();
AssertTrue(redrawGuard.TryBeginRedraw(hasEdits: false), "没有编辑时允许左键重新框选");
AssertTrue(!redrawGuard.TryBeginRedraw(hasEdits: true), "产生编辑后等待显式启动重新框选");
redrawGuard.RequestRedraw();
AssertTrue(redrawGuard.IsRedrawRequested, "Ctrl+W 启动重新框选");
AssertTrue(redrawGuard.TryBeginRedraw(hasEdits: true), "启动后允许重新框选");
AssertTrue(!redrawGuard.IsRedrawRequested, "开始重新框选后结束启动状态");
AssertTrue(!redrawGuard.TryBeginRedraw(hasEdits: true), "未再次启动时不会误清空编辑");
AssertTrue(
    CaptureSelectionRedrawPolicy.AllowsSelectionRedraw(isLongCaptureEditing: false),
    "普通截图允许主动启动重新框选");
AssertTrue(
    !CaptureSelectionRedrawPolicy.AllowsSelectionRedraw(isLongCaptureEditing: true),
    "长截图编辑模式始终禁止重新框选");

var longCaptureFrame = new Rectangle(200, 120, 360, 500);
var longCaptureBadge = LongCaptureEditorFrameLayout.GetSizeBadgeBounds(
    longCaptureFrame,
    new Size(90, 18),
    new Rectangle(0, 0, 1000, 800));
AssertTrue(
    LongCaptureEditorFrameLayout.IsMoveHandle(
        longCaptureFrame,
        longCaptureBadge,
        new Point(longCaptureBadge.Left + 4, longCaptureBadge.Top + 4),
        6),
    "长截图尺寸标签可拖动编辑框");
AssertTrue(
    LongCaptureEditorFrameLayout.IsMoveHandle(
        longCaptureFrame,
        longCaptureBadge,
        new Point(longCaptureBadge.Right + 3, longCaptureBadge.Top + 4),
        6),
    "长截图尺寸标签边缘保留拖动容错区域");
AssertTrue(
    LongCaptureEditorFrameLayout.IsMoveHandle(
        longCaptureFrame,
        longCaptureBadge,
        new Point(longCaptureFrame.Left, longCaptureFrame.Top + 100),
        6),
    "长截图边框可拖动编辑框");
AssertTrue(
    !LongCaptureEditorFrameLayout.IsMoveHandle(
        longCaptureFrame,
        longCaptureBadge,
        new Point(longCaptureFrame.Left + 80, longCaptureFrame.Top + 100),
        6),
    "长截图框内区域保留元素编辑交互");

AssertEqual(
    CaptureEscapeAction.CompleteTextEditing,
    CaptureEscapePolicy.Resolve(
        isTextEditing: true,
        hasElementSelection: true,
        hasDrawingTool: true,
        isAdjustingSelection: true),
    "Esc 优先完成文字输入");
AssertEqual(
    CaptureEscapeAction.ClearElementSelection,
    CaptureEscapePolicy.Resolve(
        isTextEditing: false,
        hasElementSelection: true,
        hasDrawingTool: false,
        isAdjustingSelection: true),
    "Esc 取消元素选中并结束当前调整");
AssertEqual(
    CaptureEscapeAction.CancelDrawingTool,
    CaptureEscapePolicy.Resolve(
        isTextEditing: false,
        hasElementSelection: false,
        hasDrawingTool: true,
        isAdjustingSelection: false),
    "Esc 关闭当前绘图工具");
AssertEqual(
    CaptureEscapeAction.FinishSelectionAdjustment,
    CaptureEscapePolicy.Resolve(
        isTextEditing: false,
        hasElementSelection: false,
        hasDrawingTool: false,
        isAdjustingSelection: true),
    "Esc 结束截图框调整");
AssertEqual(
    CaptureEscapeAction.CloseCapture,
    CaptureEscapePolicy.Resolve(
        isTextEditing: false,
        hasElementSelection: false,
        hasDrawingTool: false,
        isAdjustingSelection: false),
    "没有编辑状态时 Esc 才关闭截图");

foreach (var tool in new[]
         {
             EditorTool.Rectangle,
             EditorTool.Ellipse,
             EditorTool.Arrow,
             EditorTool.Pen,
             EditorTool.Text,
             EditorTool.Mosaic
         })
{
    AssertEqual(tool, EditorToolSelection.Toggle(EditorTool.None, tool), $"首次点击启用 {tool}");
    AssertEqual(EditorTool.None, EditorToolSelection.Toggle(tool, tool), $"再次点击关闭 {tool}");
    var otherTool = tool == EditorTool.Rectangle ? EditorTool.Ellipse : EditorTool.Rectangle;
    AssertEqual(tool, EditorToolSelection.Toggle(otherTool, tool), $"切换到 {tool}");
}

var shortcutTools = new[]
{
    EditorTool.Rectangle,
    EditorTool.Ellipse,
    EditorTool.Arrow,
    EditorTool.Pen,
    EditorTool.Text,
    EditorTool.Mosaic
};
for (var index = 0; index < shortcutTools.Length; index++)
{
    var mainKey = (Keys)((int)Keys.D1 + index);
    var numPadKey = (Keys)((int)Keys.NumPad1 + index);
    AssertEqual(shortcutTools[index], EditorToolShortcut.Resolve(mainKey), $"主键盘数字 {index + 1} 启用对应编辑工具");
    AssertEqual(shortcutTools[index], EditorToolShortcut.Resolve(numPadKey), $"数字小键盘 {index + 1} 启用对应编辑工具");
    AssertEqual(
        EditorTool.None,
        EditorToolSelection.Toggle(shortcutTools[index], EditorToolShortcut.Resolve(mainKey)),
        $"再次按数字 {index + 1} 关闭当前编辑工具");
}
AssertEqual(EditorTool.None, EditorToolShortcut.Resolve(Keys.D0), "未分配的数字键不切换编辑工具");
AssertTrue(
    !EditorIdleCursorPolicy.UsesDrawingCursor(true, true, EditorTool.None),
    "选择模式在截图区域内使用普通鼠标");
AssertTrue(
    EditorIdleCursorPolicy.UsesDrawingCursor(true, true, EditorTool.Rectangle),
    "启用绘制工具后在截图区域内使用绘制光标");

AssertTrue(
    TemporaryAnnotationMoveMode.ShouldTryMove(EditorTool.Rectangle, altPressed: true),
    "按下 Alt 时方框工具临时进入元素移动模式");
AssertTrue(
    !TemporaryAnnotationMoveMode.ShouldTryMove(EditorTool.Rectangle, altPressed: false),
    "松开 Alt 后方框工具恢复绘制优先级");
AssertTrue(
    TemporaryAnnotationMoveMode.ShouldPreserveTool(EditorTool.Rectangle, altPressed: true),
    "临时移动元素不会关闭方框工具");
AssertTrue(
    TemporaryAnnotationMoveMode.ShouldTryMove(EditorTool.None, altPressed: false),
    "未选择绘图工具时普通左键仍可选中元素");
AssertTrue(
    !TemporaryAnnotationMoveMode.ShouldPreserveTool(EditorTool.None, altPressed: true),
    "没有活动绘图工具时无需恢复工具");
AssertEqual(
    CaptureSelectAllAction.ExpandCaptureSelection,
    CaptureSelectAllPolicy.Resolve(editingElementCount: 0, allEditingElementsSelected: false),
    "没有编辑元素时 Ctrl+A 扩充截图选区");
AssertEqual(
    CaptureSelectAllAction.SelectEditingElements,
    CaptureSelectAllPolicy.Resolve(editingElementCount: 3, allEditingElementsSelected: false),
    "存在未全选编辑元素时 Ctrl+A 优先全选元素");
AssertEqual(
    CaptureSelectAllAction.ExpandCaptureSelection,
    CaptureSelectAllPolicy.Resolve(editingElementCount: 3, allEditingElementsSelected: true),
    "编辑元素已经全选时再次 Ctrl+A 扩充截图选区");
var dualMonitorVirtualDesktop = new Rectangle(-1920, 0, 4480, 1440);
var rightDisplay = new Rectangle(0, 0, 2560, 1440);
var rightDisplayClientBounds = new Rectangle(1920, 0, 2560, 1440);
AssertEqual(
    rightDisplayClientBounds,
    CaptureSelectAllPolicy.ResolveSelectionTarget(
        new Rectangle(2100, 100, 600, 400),
        dualMonitorVirtualDesktop,
        rightDisplay),
    "第一次 Ctrl+A 选择鼠标所在显示器");
AssertEqual(
    new Rectangle(0, 0, 4480, 1440),
    CaptureSelectAllPolicy.ResolveSelectionTarget(
        rightDisplayClientBounds,
        dualMonitorVirtualDesktop,
        rightDisplay),
    "第二次 Ctrl+A 选择虚拟桌面中的全部显示器");
AssertEqual(
    new Rectangle(0, 0, 4480, 1440),
    CaptureSelectAllPolicy.ResolveSelectionTarget(
        new Rectangle(0, 0, 4480, 1440),
        dualMonitorVirtualDesktop,
        rightDisplay),
    "已经选择全部显示器时继续保持全部显示器选区");
AssertEqual(
    new Rectangle(0, 0, 1920, 1080),
    CaptureSelectAllPolicy.ResolveSelectionTarget(
        Rectangle.Empty,
        dualMonitorVirtualDesktop,
        new Rectangle(-1920, 0, 1920, 1080)),
    "负坐标副显示器正确转换为截图客户区坐标");

AssertEqual(SelectionResizeEdges.Left, Hit(102, 250), "左边缘命中");
AssertEqual(SelectionResizeEdges.Right, Hit(498, 250), "右边缘命中");
AssertEqual(SelectionResizeEdges.Top, Hit(300, 102), "上边缘命中");
AssertEqual(SelectionResizeEdges.Bottom, Hit(300, 398), "下边缘命中");
AssertEqual(SelectionResizeEdges.Left | SelectionResizeEdges.Top, Hit(102, 102), "左上角命中");
AssertEqual(SelectionResizeEdges.Right | SelectionResizeEdges.Top, Hit(498, 102), "右上角命中");
AssertEqual(SelectionResizeEdges.Left | SelectionResizeEdges.Bottom, Hit(102, 398), "左下角命中");
AssertEqual(SelectionResizeEdges.Right | SelectionResizeEdges.Bottom, Hit(498, 398), "右下角命中");
AssertEqual(SelectionResizeEdges.None, Hit(300, 250), "选区内部不触发缩放");

var limits = new Rectangle(0, 0, 800, 600);
AssertEqual(new Rectangle(150, 100, 350, 300), Resize(SelectionResizeEdges.Left, 150, 200), "向内调整左边缘");
AssertEqual(Rectangle.FromLTRB(100, 100, 601, 400), Resize(SelectionResizeEdges.Right, 600, 200), "向外调整右边缘");
AssertEqual(Rectangle.FromLTRB(100, 50, 601, 400), Resize(SelectionResizeEdges.Right | SelectionResizeEdges.Top, 600, 50), "调整右上角");
AssertEqual(Rectangle.FromLTRB(488, 100, 500, 400), Resize(SelectionResizeEdges.Left, 700, 200), "限制最小宽度");
AssertEqual(Rectangle.FromLTRB(100, 0, 500, 400), Resize(SelectionResizeEdges.Top, 200, -100), "限制屏幕上边界");
AssertEqual(Rectangle.FromLTRB(100, 100, 800, 600), Resize(SelectionResizeEdges.Right | SelectionResizeEdges.Bottom, 900, 900), "限制屏幕右下边界");

AssertEqual(new Rectangle(150, 125, 400, 300), SelectionMover.Move(selection, new Point(50, 25), limits), "移动选区");
AssertEqual(new Rectangle(0, 0, 400, 300), SelectionMover.Move(selection, new Point(-500, -500), limits), "限制选区左上边界");
AssertEqual(new Rectangle(400, 300, 400, 300), SelectionMover.Move(selection, new Point(900, 900), limits), "限制选区右下边界");
AssertEqual("/select,\"C:\\截图目录\\轻截 01.png\"", ExplorerFileLocationService.BuildSelectArguments("C:\\截图目录\\轻截 01.png"), "资源管理器选中文件参数");
AssertEqual(
    "录屏保存成功",
    SavedArtifactNotificationForm.GetTitle("C:\\截图目录\\录屏_20260721_120000.mp4"),
    "录屏保存后使用录屏成功提示");
AssertEqual(
    "截图保存成功",
    SavedArtifactNotificationForm.GetTitle("C:\\截图目录\\截图_20260721_120000.png"),
    "截图保存后继续使用截图成功提示");
var notificationOpenedPath = string.Empty;
using (var savedArtifactNotification = new SavedArtifactNotificationForm(
           "C:\\截图目录\\录屏_20260721_120000.mp4"))
{
    savedArtifactNotification.OpenRequested += (_, path) => notificationOpenedPath = path;
    savedArtifactNotification.RequestOpen();
}
AssertEqual(
    Path.GetFullPath("C:\\截图目录\\录屏_20260721_120000.mp4"),
    notificationOpenedPath,
    "点击录屏保存提示传递完整文件路径");

var namingTimestamp = new DateTime(2026, 7, 21, 15, 6, 7, 123);
AssertEqual(
    "截图_2026-07-21_15-06-07-123.png",
    ScreenshotFileNamePolicy.CreateFileName(
        ScreenshotFileNameMode.DateTime,
        namingTimestamp,
        []),
    "日期时间命名规则");
AssertEqual(
    "截图_2026-07-21_15-06-07-123_1.png",
    ScreenshotFileNamePolicy.CreateFileName(
        ScreenshotFileNameMode.DateTime,
        namingTimestamp,
        ["截图_2026-07-21_15-06-07-123.png"]),
    "同毫秒日期时间名称自动避免覆盖");
AssertEqual(
    "3.png",
    ScreenshotFileNamePolicy.CreateFileName(
        ScreenshotFileNameMode.Sequence,
        namingTimestamp,
        ["0.png", "2.png", "说明.png"]),
    "目录序号从现有最大数字继续递增");
AssertEqual(
    "项目_首页_按钮_失败.png",
    ScreenshotFileNamePolicy.CreateFileName(
        ScreenshotFileNameMode.ImageText,
        namingTimestamp,
        [],
        ["项目:首页", "按钮?失败"]),
    "图片内多个文字元素组合并清理文件名非法字符");
AssertEqual(
    "截图_CON.png",
    ScreenshotFileNamePolicy.CreateFileName(
        ScreenshotFileNameMode.ImageText,
        namingTimestamp,
        [],
        ["CON"]),
    "图片文字命名避开 Windows 保留设备名");
AssertEqual(
    "截图_2026-07-21_15-06-07-123.png",
    ScreenshotFileNamePolicy.CreateFileName(
        ScreenshotFileNameMode.ImageText,
        namingTimestamp,
        [],
        ["  ", "\r\n"]),
    "图片中没有可用文字时回退日期时间命名");
using (var namingDocument = new AnnotationDocument())
{
    namingDocument.Add(new TextAnnotation(
        new Rectangle(10, 10, 100, 30),
        "可见标题",
        Color.White,
        18F));
    namingDocument.Add(new PastedTextAnnotation(
        new Rectangle(30, 50, 120, 36),
        "可见说明"));
    namingDocument.Add(new TextAnnotation(
        new Rectangle(500, 500, 100, 30),
        "选区外文字",
        Color.White,
        18F));
    AssertEqual(
        "可见标题,可见说明",
        string.Join(',', namingDocument.GetVisibleTextContents(new Rectangle(0, 0, 300, 200))),
        "文字命名只读取最终图片范围内的文字元素");
}

var namingSaveDirectory = Path.Combine(
    Path.GetTempPath(),
    "ScreenshotTool.NamingTests",
    Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(namingSaveDirectory);
    File.WriteAllBytes(Path.Combine(namingSaveDirectory, "0.png"), []);
    File.WriteAllBytes(Path.Combine(namingSaveDirectory, "2.png"), []);
    using var namingBitmap = new Bitmap(8, 8);
    var sequencePath = new PngImageSaveService().SavePng(
        namingBitmap,
        namingSaveDirectory,
        ScreenshotFileNameMode.Sequence);
    AssertEqual("3.png", Path.GetFileName(sequencePath), "序号模式实际保存为下一个数字 PNG");
    var textPath = new PngImageSaveService().SavePng(
        namingBitmap,
        namingSaveDirectory,
        ScreenshotFileNameMode.ImageText,
        ["发布:成功"]);
    AssertEqual("发布_成功.png", Path.GetFileName(textPath), "图片文字模式实际保存为文字 PNG");
    var duplicateTextPath = new PngImageSaveService().SavePng(
        namingBitmap,
        namingSaveDirectory,
        ScreenshotFileNameMode.ImageText,
        ["发布:成功"]);
    AssertEqual("发布_成功_1.png", Path.GetFileName(duplicateTextPath), "重复图片文字名称追加序号");
}
finally
{
    if (Directory.Exists(namingSaveDirectory))
    {
        Directory.Delete(namingSaveDirectory, recursive: true);
    }
}

var folderDropTestDirectory = Path.Combine(
    Path.GetTempPath(),
    "ScreenshotTool.FolderDropTests",
    Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(folderDropTestDirectory);
    AssertTrue(
        FolderDropPathResolver.TryResolve([folderDropTestDirectory], out var droppedFolder),
        "拖入单个文件夹");
    AssertEqual(Path.GetFullPath(folderDropTestDirectory), droppedFolder, "拖入文件夹后引用完整路径");

    var droppedFile = Path.Combine(folderDropTestDirectory, "not-a-folder.txt");
    File.WriteAllText(droppedFile, "test");
    AssertTrue(!FolderDropPathResolver.TryResolve([droppedFile], out _), "拒绝拖入文件");
    AssertTrue(
        !FolderDropPathResolver.TryResolve([folderDropTestDirectory, Path.GetTempPath()], out _),
        "拒绝同时拖入多个文件夹");
}
finally
{
    if (Directory.Exists(folderDropTestDirectory))
    {
        Directory.Delete(folderDropTestDirectory, recursive: true);
    }
}

var folderMigrationTestDirectory = Path.Combine(
    Path.GetTempPath(),
    "ScreenshotTool.FolderMigrationTests",
    Guid.NewGuid().ToString("N"));
try
{
    var previousFolder = Path.Combine(folderMigrationTestDirectory, "previous");
    var newFolder = Path.Combine(folderMigrationTestDirectory, "new");
    Directory.CreateDirectory(previousFolder);
    Directory.CreateDirectory(newFolder);
    var previousPng = Path.Combine(previousFolder, "截图.png");
    var previousJpeg = Path.Combine(previousFolder, "照片.JPEG");
    var unrelatedFile = Path.Combine(previousFolder, "说明.txt");
    File.WriteAllText(previousPng, "previous png");
    File.WriteAllText(previousJpeg, "previous jpeg");
    File.WriteAllText(unrelatedFile, "keep");
    File.WriteAllText(Path.Combine(newFolder, "截图.png"), "existing png");

    var previousImages = ScreenshotFolderMigration.FindImages(previousFolder);
    AssertEqual(2, previousImages.Count, "切换保存路径时只发现旧目录中的图片");
    var migration = ScreenshotFolderMigration.MoveImages(previousImages, newFolder);
    AssertEqual(2, migration.MovedCount, "确认迁移后移动全部旧图片");
    AssertEqual(0, migration.FailedFiles.Count, "可访问图片迁移无失败项");
    AssertTrue(!File.Exists(previousPng) && !File.Exists(previousJpeg), "迁移后旧目录不再保留已移动图片");
    AssertTrue(File.Exists(unrelatedFile), "迁移不会移动旧目录中的非图片文件");
    AssertEqual("existing png", File.ReadAllText(Path.Combine(newFolder, "截图.png")), "迁移不会覆盖新目录同名图片");
    AssertEqual("previous png", File.ReadAllText(Path.Combine(newFolder, "截图_1.png")), "同名图片追加序号后移动");
    AssertEqual("previous jpeg", File.ReadAllText(Path.Combine(newFolder, "照片.JPEG")), "图片扩展名匹配不区分大小写");
}
finally
{
    if (Directory.Exists(folderMigrationTestDirectory))
    {
        Directory.Delete(folderMigrationTestDirectory, recursive: true);
    }
}

var largeSticker = StickerLayout.CreateInitialBounds(new Size(2000, 1000), selection, new Point(300, 250));
AssertEqual(new Rectangle(100, 150, 400, 200), largeSticker, "大图片按三分之二面积缩小");
AssertTrue((long)largeSticker.Width * largeSticker.Height <= (long)selection.Width * selection.Height * 2 / 3, "贴纸初始面积限制");
AssertEqual(new Rectangle(280, 240, 40, 20), StickerLayout.CreateInitialBounds(new Size(40, 20), selection, new Point(300, 250)), "小图片保持原始尺寸");
AssertEqual(new Rectangle(100, 100, 40, 20), StickerLayout.CreateInitialBounds(new Size(40, 20), selection, new Point(100, 100)), "贴纸粘贴位置限制在选区内");
AssertEqual(new Rectangle(400, 350, 100, 50), StickerLayout.Move(new Rectangle(200, 200, 100, 50), new Point(500, 500), selection), "贴纸移动限制在选区内");
AssertEqual(StickerHitTarget.TopLeft, StickerLayout.HitTest(largeSticker, largeSticker.Location, 10), "贴纸左上缩放手柄命中");
var eightHandles = StickerLayout.GetHandles(new Rectangle(200, 200, 100, 80), 10);
AssertEqual(8, eightHandles.Count, "编辑元素显示四角和四边中点共八个缩放手柄");
AssertEqual(
    StickerHitTarget.Top,
    StickerLayout.HitTest(new Rectangle(200, 200, 100, 80), new Point(250, 200), 10),
    "上边居中手柄可命中");
AssertEqual(
    StickerHitTarget.Right,
    StickerLayout.HitTest(new Rectangle(200, 200, 100, 80), new Point(300, 240), 10),
    "右边居中手柄可命中");
AssertEqual(new Rectangle(200, 200, 300, 150), StickerLayout.Resize(new Rectangle(200, 200, 200, 100), StickerHitTarget.BottomRight, new Point(600, 500), selection), "贴纸等比缩放并限制在选区内");
AssertEqual(
    new Rectangle(200, 200, 180, 150),
    AnnotationResizeLayout.Resize(
        new Rectangle(200, 200, 100, 80),
        StickerHitTarget.BottomRight,
        new Point(380, 350),
        selection),
    "绘制元素四角缩放可独立调整宽度和高度");
AssertEqual(
    new Rectangle(200, 200, 8, 8),
    AnnotationResizeLayout.Resize(
        new Rectangle(200, 200, 100, 80),
        StickerHitTarget.BottomRight,
        new Point(201, 201),
        selection),
    "绘制元素缩放限制最小边长");
AssertEqual(
    new Rectangle(200, 200, 300, 200),
    AnnotationResizeLayout.Resize(
        new Rectangle(200, 200, 100, 80),
        StickerHitTarget.BottomRight,
        new Point(900, 900),
        selection),
    "绘制元素缩放限制在截图框内");
AssertEqual(
    new Rectangle(200, 150, 100, 130),
    AnnotationResizeLayout.Resize(
        new Rectangle(200, 200, 100, 80),
        StickerHitTarget.Top,
        new Point(250, 150),
        selection),
    "上边中点手柄只调整顶部边缘");
AssertEqual(
    new Rectangle(200, 200, 160, 80),
    AnnotationResizeLayout.Resize(
        new Rectangle(200, 200, 100, 80),
        StickerHitTarget.Right,
        new Point(360, 240),
        selection),
    "右边中点手柄只调整右侧边缘");
AssertEqual(new Point(10, -20), AnnotationAlignment.QuantizeOffset(new Point(14, -16), 10), "Ctrl 拖动按固定像素步长取整");
AssertEqual(
    new Point(50, 50),
    AnnotationAlignment.SnapMoveOffset(
        new Rectangle(100, 100, 50, 50),
        new Point(43, 47),
        [new Rectangle(200, 200, 80, 60)],
        8),
    "元素移动接近其他元素边缘时自动吸附对齐");
AssertEqual(
    new Point(200, 240),
    AnnotationAlignment.SnapResizePoint(
        new Point(197, 240),
        StickerHitTarget.Right,
        [new Rectangle(200, 100, 80, 60)],
        8),
    "单边缩放接近其他元素边缘时自动吸附");
var controlDoubleTap = new ControlDoubleTapDetector();
AssertTrue(!controlDoubleTap.RegisterKeyDown(Keys.ControlKey, 100), "第一次 Ctrl 记录为吸附快捷键起点");
AssertTrue(!controlDoubleTap.RegisterKeyUp(Keys.ControlKey, 140), "第一次 Ctrl 抬起只记录一次裸按");
AssertTrue(!controlDoubleTap.RegisterKeyDown(Keys.ControlKey, 260), "第二次 Ctrl 按下等待确认没有组合键");
AssertTrue(controlDoubleTap.RegisterKeyUp(Keys.ControlKey, 320), "快速双击 Ctrl 切换元素吸附");
var controlShortcutDoesNotToggle = new ControlDoubleTapDetector();
controlShortcutDoesNotToggle.RegisterKeyDown(Keys.ControlKey, 100);
controlShortcutDoesNotToggle.RegisterKeyDown(Keys.A, 120);
AssertTrue(
    !controlShortcutDoesNotToggle.RegisterKeyUp(Keys.ControlKey, 160),
    "Ctrl+A 等组合键不会误触双击 Ctrl 吸附快捷键");
var controlPointerActionDoesNotToggle = new ControlDoubleTapDetector();
controlPointerActionDoesNotToggle.RegisterKeyDown(Keys.ControlKey, 100);
controlPointerActionDoesNotToggle.CancelCurrentTap();
AssertTrue(
    !controlPointerActionDoesNotToggle.RegisterKeyUp(Keys.ControlKey, 160),
    "Ctrl 拖动和 Ctrl 滚轮不会计入双击 Ctrl 快捷键");
AssertEqual(StickerSelectionMoveMode.FollowSelection, new AppSettings().Preferences.StickerSelectionMoveMode, "贴纸默认跟随截图框");
AssertTrue(!new AppSettings().Preferences.LongCaptureSafetyChecksEnabled, "长截图安全校验默认关闭");
AssertEqual(5, new AppSettings().Preferences.AnnotationRotationStepDegrees, "编辑元素默认每格旋转 5 度");
AssertEqual(DrawingCursorShape.Circle, new AppSettings().Preferences.DrawingCursorShape, "绘制光标默认为圆形");
AssertTrue(new AppSettings().Preferences.AnnotationSnappingEnabled, "编辑元素吸附默认开启");
AssertEqual(8, new AppSettings().Preferences.AnnotationSnapThresholdPixels, "元素吸附距离默认 8 像素");
AssertEqual(10, new AppSettings().Preferences.CtrlDragStepPixels, "Ctrl 拖动默认按 10 像素步进");
AssertEqual(
    RecordingRegionIndicatorStyle.Dashed,
    new AppSettings().Preferences.RecordingRegionIndicatorStyle,
    "录屏范围默认使用虚线提示");
AssertTrue(
    new AppSettings().Preferences.ScreenRecordingCaptureSystemAudio,
    "录屏默认录制系统声音");
AssertTrue(
    new AppSettings().Preferences.ScreenRecordingCaptureMicrophone,
    "录屏默认录制麦克风");
AssertTrue(
    new AppSettings().Preferences.ScreenRecordingShowMouseClickIndicator,
    "录屏默认显示左键黄色圆圈");
AssertEqual(30, new AppSettings().Preferences.ScreenRecordingFramesPerSecond, "录屏默认 30 FPS");
AssertEqual(8_000_000, new AppSettings().Preferences.ScreenRecordingVideoBitrate, "录屏默认 8 Mbps");
AssertEqual(15F, AnnotationRotation.GetWheelDeltaDegrees(120, 15), "滚轮向上按配置步进正向旋转");
AssertEqual(-15F, AnnotationRotation.GetWheelDeltaDegrees(-120, 15), "滚轮向下按配置步进反向旋转");
AssertEqual(30F, AnnotationRotation.GetWheelDeltaDegrees(240, 15), "连续滚轮格数按配置速度累加");
AssertEqual(1, AnnotationRotationStep.Normalize(-20), "旋转速度配置限制最小值");
AssertEqual(90, AnnotationRotationStep.Normalize(200), "旋转速度配置限制最大值");
using (var screenshotSettingsPage = new ScreenshotSettingsPage(
           new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Alt, (int)Keys.Q),
           startMinimized: true,
           startWithWindows: true,
           dismissSaveNotificationBeforeCapture: false,
           hideMainWindowDuringCapture: true))
{
    AssertSingleColumnSettings(screenshotSettingsPage, 5, "截图设置页");
    AssertEqual(
        new HotkeyDefinition(HotkeyModifiers.Control | HotkeyModifiers.Alt, (int)Keys.Q),
        screenshotSettingsPage.Hotkey,
        "截图设置页显示全局快捷键");
    AssertTrue(screenshotSettingsPage.StartMinimized, "截图设置页显示启动后最小化选项");
    AssertTrue(screenshotSettingsPage.StartWithWindows, "截图设置页显示开机自动启动选项");
    AssertTrue(
        !screenshotSettingsPage.DismissSaveNotificationBeforeCapture,
        "截图设置页显示保留保存提示的选择");
    AssertTrue(
        screenshotSettingsPage.HideMainWindowDuringCapture,
        "截图设置页显示隐藏主界面的选择");
    screenshotSettingsPage.DismissSaveNotificationBeforeCapture = true;
    screenshotSettingsPage.HideMainWindowDuringCapture = false;
    screenshotSettingsPage.Hotkey = HotkeyDefinition.Default;
    screenshotSettingsPage.StartMinimized = false;
    screenshotSettingsPage.StartWithWindows = false;
    AssertEqual(HotkeyDefinition.Default, screenshotSettingsPage.Hotkey, "截图设置页可修改快捷键");
    AssertTrue(!screenshotSettingsPage.StartMinimized, "截图设置页可关闭启动后最小化");
    AssertTrue(!screenshotSettingsPage.StartWithWindows, "截图设置页可关闭开机自动启动");
    AssertTrue(
        screenshotSettingsPage.DismissSaveNotificationBeforeCapture,
        "截图设置页可开启截图前关闭提示");
    AssertTrue(
        !screenshotSettingsPage.HideMainWindowDuringCapture,
        "截图设置页可关闭截图时隐藏主界面");
}
using (var editorSettingsPage = new EditorSettingsPage(
           ToolWidthRange.Create(2, 8),
           rotationStepDegrees: 12,
           drawingCursorShape: DrawingCursorShape.Square,
           snappingEnabled: false,
           snapThresholdPixels: 14,
           ctrlDragStepPixels: 20))
{
    AssertSingleColumnSettings(editorSettingsPage, 7, "图片修改设置页");
    AssertEqual(12, editorSettingsPage.RotationStepDegrees, "图片修改页显示旋转速度配置");
    AssertEqual(DrawingCursorShape.Square, editorSettingsPage.DrawingCursorShape, "图片修改页显示方形绘制光标");
    AssertTrue(!editorSettingsPage.SnappingEnabled, "图片修改页显示元素吸附开关");
    AssertEqual(14, editorSettingsPage.SnapThresholdPixels, "图片修改页显示吸附距离");
    AssertEqual(20, editorSettingsPage.CtrlDragStepPixels, "图片修改页显示 Ctrl 拖动步长");
    editorSettingsPage.RotationStepDegrees = 18;
    editorSettingsPage.DrawingCursorShape = DrawingCursorShape.Circle;
    editorSettingsPage.SnappingEnabled = true;
    editorSettingsPage.SnapThresholdPixels = 9;
    editorSettingsPage.CtrlDragStepPixels = 12;
    AssertEqual(18, editorSettingsPage.RotationStepDegrees, "图片修改页可调整旋转速度");
    AssertEqual(DrawingCursorShape.Circle, editorSettingsPage.DrawingCursorShape, "图片修改页可切换圆形绘制光标");
    AssertTrue(editorSettingsPage.SnappingEnabled, "图片修改页可切换元素吸附");
    AssertEqual(9, editorSettingsPage.SnapThresholdPixels, "图片修改页可调节吸附距离");
    AssertEqual(12, editorSettingsPage.CtrlDragStepPixels, "图片修改页可调节 Ctrl 拖动步长");
}
using (var drawingCoefficientsSettingsPage = new DrawingCoefficientsSettingsPage(
           new DrawingToolCoefficients()))
{
    AssertSingleColumnSettings(drawingCoefficientsSettingsPage, 7, "绘制系数设置页");
}
var recordingSettingsHost = new TestModuleSettingsHost();
recordingSettingsHost.SetBoolean(ScreenRecordingPreferences.CaptureSystemAudioId, false);
recordingSettingsHost.SetBoolean(ScreenRecordingPreferences.CaptureMicrophoneId, true);
recordingSettingsHost.SetBoolean(ScreenRecordingPreferences.ShowMouseClickIndicatorId, false);
recordingSettingsHost.SetInteger(ScreenRecordingPreferences.FramesPerSecondId, 60);
recordingSettingsHost.SetInteger(ScreenRecordingPreferences.VideoBitrateId, 12_000_000);
recordingSettingsHost.SetInteger(
    ScreenRecordingPreferences.RegionIndicatorStyleId,
    (int)CaptureRegionIndicatorStyle.Solid);
using (var screenRecordingSettingsPage = new ScreenRecordingSettingsPage(
           recordingSettingsHost))
{
    AssertSingleColumnSettings(screenRecordingSettingsPage, 6, "录屏设置页");
    AssertTrue(!screenRecordingSettingsPage.CaptureSystemAudio, "录屏页显示系统声音设置");
    AssertTrue(screenRecordingSettingsPage.CaptureMicrophone, "录屏页显示麦克风设置");
    AssertTrue(
        !screenRecordingSettingsPage.ShowMouseClickIndicator,
        "录屏页显示左键圆圈开关");
    AssertEqual(60, screenRecordingSettingsPage.FramesPerSecond, "录屏页显示 60 FPS");
    AssertEqual(12_000_000, screenRecordingSettingsPage.VideoBitrate, "录屏页显示 12 Mbps");
    AssertEqual(
        CaptureRegionIndicatorStyle.Solid,
        screenRecordingSettingsPage.RegionIndicatorStyle,
        "录屏页显示实线范围提示");
    AssertEqual("90.96 MB", screenRecordingSettingsPage.OneMinuteEstimate, "录屏页实时估算储存占用");
    screenRecordingSettingsPage.CaptureSystemAudio = true;
    screenRecordingSettingsPage.CaptureMicrophone = false;
    screenRecordingSettingsPage.ShowMouseClickIndicator = true;
    screenRecordingSettingsPage.FramesPerSecond = 30;
    screenRecordingSettingsPage.VideoBitrate = 4_000_000;
    screenRecordingSettingsPage.RegionIndicatorStyle = CaptureRegionIndicatorStyle.None;
    AssertTrue(screenRecordingSettingsPage.CaptureSystemAudio, "录屏页可开启系统声音");
    AssertTrue(!screenRecordingSettingsPage.CaptureMicrophone, "录屏页可关闭麦克风");
    AssertTrue(screenRecordingSettingsPage.ShowMouseClickIndicator, "录屏页可开启左键圆圈");
    AssertEqual(30, screenRecordingSettingsPage.FramesPerSecond, "录屏页可切换帧率");
    AssertEqual(4_000_000, screenRecordingSettingsPage.VideoBitrate, "录屏页可切换码率");
    AssertEqual(
        CaptureRegionIndicatorStyle.None,
        screenRecordingSettingsPage.RegionIndicatorStyle,
        "录屏页可关闭范围提示");
    screenRecordingSettingsPage.Controls
        .OfType<Panel>()
        .SelectMany(panel => panel.Controls.OfType<Button>())
        .Single(button => button.Text == "保存录屏设置")
        .PerformClick();
    AssertEqual(1, recordingSettingsHost.SaveCount, "录屏设置页自行请求保存模块偏好");
    AssertEqual(
        4_000_000,
        recordingSettingsHost.GetInteger(ScreenRecordingPreferences.VideoBitrateId, 0),
        "录屏设置页写回通用键值设置");
}
using (var solidRecordingIndicator = new Bitmap(160, 100))
using (var dashedRecordingIndicator = new Bitmap(160, 100))
using (var hiddenRecordingIndicator = new Bitmap(160, 100))
{
    using (var graphics = Graphics.FromImage(solidRecordingIndicator))
    {
        RecordingRegionIndicator.Draw(
            graphics,
            new Rectangle(Point.Empty, solidRecordingIndicator.Size),
            RecordingRegionIndicatorStyle.Solid);
    }
    using (var graphics = Graphics.FromImage(dashedRecordingIndicator))
    {
        RecordingRegionIndicator.Draw(
            graphics,
            new Rectangle(Point.Empty, dashedRecordingIndicator.Size),
            RecordingRegionIndicatorStyle.Dashed);
    }
    using (var graphics = Graphics.FromImage(hiddenRecordingIndicator))
    {
        RecordingRegionIndicator.Draw(
            graphics,
            new Rectangle(Point.Empty, hiddenRecordingIndicator.Size),
            RecordingRegionIndicatorStyle.None);
    }

    var solidIndicatorPixels = CountNonTransparentPixels(solidRecordingIndicator);
    var dashedIndicatorPixels = CountNonTransparentPixels(dashedRecordingIndicator);
    AssertTrue(
        solidIndicatorPixels > dashedIndicatorPixels && dashedIndicatorPixels > 0,
        "录屏范围实线与虚线使用不同边框样式");
    AssertEqual(0, CountNonTransparentPixels(hiddenRecordingIndicator), "录屏范围提示可完全关闭");
}
using (var recordingIndicatorSession = new LiveAnnotationSessionForm(
           new Rectangle(0, 0, 160, 100),
           new Bitmap(160, 100),
           new TestTextClipboardService(),
           new ToolWidthController(ToolWidthRange.Create(1, 32), 4),
           new DrawingToolCoefficients(),
           AnnotationRotationStep.DefaultDegrees,
           DrawingCursorShape.Circle,
           Color.Red,
           _ => { },
           recordingRegionIndicatorStyle: RecordingRegionIndicatorStyle.Solid))
using (var recordingContent = new Bitmap(160, 100))
using (var recordingContentGraphics = Graphics.FromImage(recordingContent))
{
    recordingIndicatorSession.RenderContent(recordingContentGraphics);
    AssertEqual(
        0,
        CountNonTransparentPixels(recordingContent),
        "录屏范围提示不进入 MP4 内容渲染层");
}
AssertTrue(DrawingCursorIndicator.Supports(EditorTool.Pen), "画笔使用笔刷轮廓光标");
AssertTrue(DrawingCursorIndicator.Supports(EditorTool.Mosaic), "马赛克使用笔刷轮廓光标");
AssertTrue(!DrawingCursorIndicator.Supports(EditorTool.Rectangle), "矩形保留标准绘制光标");
AssertEqual(8, DrawingCursorIndicator.CalculateClientDiameter(8F, 1D), "普通截图光标按实际笔刷尺寸显示");
AssertEqual(4, DrawingCursorIndicator.CalculateClientDiameter(8F, 0.5D), "长图缩小时同步缩小笔刷光标");
AssertEqual(16, DrawingCursorIndicator.CalculateClientDiameter(8F, 2D), "长图放大时同步放大笔刷光标");
AssertEqual(
    new Rectangle(45, 55, 10, 10),
    DrawingCursorIndicator.GetOutlineBounds(new Point(50, 60), 10),
    "笔刷光标以鼠标位置为中心");
using (var circleCursorBitmap = new Bitmap(40, 40))
using (var squareCursorBitmap = new Bitmap(40, 40))
using (var circleCursor = new DrawingCursorIndicator(DrawingCursorShape.Circle))
using (var squareCursor = new DrawingCursorIndicator(DrawingCursorShape.Square))
{
    circleCursor.Update(new Point(20, 20), 12F, 1D);
    squareCursor.Update(new Point(20, 20), 12F, 1D);
    using (var graphics = Graphics.FromImage(circleCursorBitmap))
    {
        circleCursor.Draw(graphics);
    }
    using (var graphics = Graphics.FromImage(squareCursorBitmap))
    {
        squareCursor.Draw(graphics);
    }
    AssertTrue(
        circleCursorBitmap.GetPixel(14, 14).A < squareCursorBitmap.GetPixel(14, 14).A,
        "圆形和正方形笔刷光标使用不同轮廓");
}
var longCaptureSettingsHost = new TestModuleSettingsHost();
using (var longCaptureSettingsPage = new LongCaptureSettingsPage(longCaptureSettingsHost))
{
    AssertTrue(!longCaptureSettingsPage.SafetyChecksEnabled, "长截图设置页显示默认宽松模式");
    longCaptureSettingsPage.SafetyChecksEnabled = true;
    AssertTrue(longCaptureSettingsPage.SafetyChecksEnabled, "长截图设置页可开启安全校验");
    longCaptureSettingsPage.Controls
        .OfType<Panel>()
        .SelectMany(panel => panel.Controls.OfType<Button>())
        .Single(button => button.Text == "保存长截图设置")
        .PerformClick();
    AssertEqual(1, longCaptureSettingsHost.SaveCount, "长截图设置页自行请求保存模块偏好");
    AssertTrue(
        longCaptureSettingsHost.GetBoolean(LongCapturePreferences.SafetyChecksId, false),
        "长截图设置页写回通用键值设置");
}

using (var movableDocument = new AnnotationDocument())
using (var movableImage = new Bitmap(20, 20))
{
    var imageAnnotation = new StickerAnnotation(movableImage, new Rectangle(120, 120, 40, 40));
    var pastedText = new PastedTextAnnotation(new Rectangle(180, 140, 100, 40), "可拖动文字");
    var transparentText = new TextAnnotation(new Rectangle(220, 180, 140, 50), "透明文字", Color.Red, 18F);
    movableDocument.Add(imageAnnotation);
    movableDocument.Add(pastedText);
    movableDocument.Add(transparentText);

    AssertTrue(imageAnnotation.SupportsResize, "图片贴纸保留四角缩放能力");
    AssertTrue(imageAnnotation.PreserveAspectRatioWhenResizing, "图片缩放继续保持宽高比");
    AssertTrue(!pastedText.SupportsResize && !transparentText.SupportsResize, "文字框只显示拖动交互");
    AssertTrue(ReferenceEquals(transparentText, movableDocument.FindTopMovableAt(new Point(230, 190))), "普通文字参与可移动标注命中");
    AssertTrue(ReferenceEquals(pastedText, movableDocument.FindTopMovableAt(new Point(190, 150))), "粘贴文字参与可移动标注命中");
    AssertTrue(!imageAnnotation.CanMove(altPressed: false), "图片不能只用鼠标左键移动");
    AssertTrue(!pastedText.CanMove(altPressed: false), "粘贴文字不能只用鼠标左键移动");
    AssertTrue(!transparentText.CanMove(altPressed: false), "普通文字不能只用鼠标左键移动");
    AssertTrue(imageAnnotation.CanMove(altPressed: true), "图片支持 Alt 加鼠标左键移动");
    AssertTrue(pastedText.CanMove(altPressed: true), "粘贴文字支持 Alt 加鼠标左键移动");
    AssertTrue(transparentText.CanMove(altPressed: true), "普通文字支持 Alt 加鼠标左键移动");
    transparentText.SetBounds(StickerLayout.Move(
        transparentText.Bounds,
        new Point(35, 20),
        selection));
    AssertEqual(new Rectangle(255, 200, 140, 50), transparentText.Bounds, "普通文字可以在截图框内拖动");
}

using (var rotationDocument = new AnnotationDocument())
using (var rotationSource = CreateSolidBitmap(new Size(260, 220), Color.Black))
{
    var rotatedRectangle = new ShapeAnnotation(
        EditorTool.Rectangle,
        new Rectangle(100, 100, 80, 20),
        Color.Red,
        3F);
    rotationDocument.Add(rotatedRectangle);

    rotatedRectangle.RotateBy(90F);
    AssertEqual(90F, rotatedRectangle.RotationDegrees, "编辑元素记录旋转角度");
    AssertEqual(new Rectangle(130, 70, 20, 80), rotatedRectangle.VisualBounds, "旋转后更新可视边界");
    AssertTrue(rotatedRectangle.HitTest(new Point(140, 75), 0), "旋转后按视觉位置命中元素");
    AssertTrue(!rotatedRectangle.HitTest(new Point(105, 110), 0), "旋转后不再命中原始包围框空白区域");
    AssertTrue(ReferenceEquals(
        rotatedRectangle,
        rotationDocument.FindTopMovableAt(new Point(140, 75))),
        "鼠标下元素查找支持旋转后的坐标");
    var rotatedSelection = new AnnotationSelection();
    rotatedSelection.SelectOnly(rotatedRectangle);
    AssertEqual(rotatedRectangle.VisualBounds, rotatedSelection.Bounds, "元素选区跟随旋转后的可视边界");
    var rotatedResizePointer = AnnotationRotation.ToUnrotatedPoint(
        new Point(130, 160),
        rotatedRectangle.Bounds,
        rotatedRectangle.RotationDegrees);
    var rotatedResizeBounds = AnnotationResizeLayout.Resize(
        rotatedRectangle.Bounds,
        StickerHitTarget.BottomRight,
        rotatedResizePointer,
        new Rectangle(0, 0, 260, 220));
    rotatedResizeBounds = AnnotationRotation.PreserveOppositeCorner(
        rotatedRectangle.Bounds,
        rotatedResizeBounds,
        StickerHitTarget.BottomRight,
        rotatedRectangle.RotationDegrees);
    AssertEqual(new Rectangle(95, 105, 90, 20), rotatedResizeBounds, "旋转元素缩放时固定对角手柄");

    using var rotatedExport = RenderDocumentSelection(
        rotationDocument,
        rotationSource,
        new Rectangle(Point.Empty, rotationSource.Size));
    AssertTrue(
        ContainsPixelDifferentFrom(rotatedExport, new Rectangle(128, 68, 25, 25), Color.Black),
        "最终导出位图包含旋转到原始边界外的元素像素");
}

AssertTrue(
    Math.Abs(AnnotationScaling.GetWheelScaleFactor(120) - 1.1D) < 0.0001D,
    "Ctrl 加滚轮向上按每格百分之十放大");
AssertTrue(
    Math.Abs(AnnotationScaling.GetWheelScaleFactor(-120) - 1D / 1.1D) < 0.0001D,
    "Ctrl 加滚轮向下使用相反倍率缩小");
AssertTrue(
    Math.Abs(AnnotationScaling.GetWheelScaleFactor(240) - 1.21D) < 0.0001D,
    "连续滚轮格数累积缩放倍率");
var scalingLimits = new Rectangle(0, 0, 300, 240);
var scaleOrigin = new Rectangle(100, 100, 80, 40);
var enlargedAtPointer = AnnotationScaling.ScaleAt(
    scaleOrigin,
    rotationDegrees: 0F,
    anchor: new Point(120, 110),
    requestedFactor: AnnotationScaling.GetWheelScaleFactor(120),
    scalingLimits);
AssertEqual(new Rectangle(98, 99, 88, 44), enlargedAtPointer, "编辑元素以鼠标位置为锚点放大");
AssertEqual(
    scaleOrigin,
    AnnotationScaling.ScaleAt(
        enlargedAtPointer,
        rotationDegrees: 0F,
        anchor: new Point(120, 110),
        requestedFactor: AnnotationScaling.GetWheelScaleFactor(-120),
        scalingLimits),
    "向上再向下滚动可恢复编辑元素尺寸和位置");
AssertEqual(
    new Rectangle(102, 101, 16, 8),
    AnnotationScaling.ScaleAt(
        new Rectangle(100, 100, 20, 10),
        rotationDegrees: 0F,
        anchor: new Point(110, 105),
        requestedFactor: 0.01D,
        scalingLimits),
    "滚轮缩小保留编辑元素最小可操作尺寸");
var boundaryScaled = AnnotationScaling.ScaleAt(
    new Rectangle(180, 180, 80, 40),
    rotationDegrees: 0F,
    anchor: new Point(200, 200),
    requestedFactor: 2D,
    limits: new Rectangle(0, 0, 260, 220));
AssertEqual(new Rectangle(100, 140, 160, 80), boundaryScaled, "滚轮放大限制在截图编辑区域内");
var rotatedScaled = AnnotationScaling.ScaleAt(
    new Rectangle(100, 100, 80, 20),
    rotationDegrees: 90F,
    anchor: new Point(140, 75),
    requestedFactor: 1.1D,
    scalingLimits);
AssertEqual(new Rectangle(96, 103, 88, 22), rotatedScaled, "已旋转元素沿视觉坐标以鼠标位置缩放");
AssertTrue(
    scalingLimits.Contains(AnnotationRotation.GetRotatedBounds(rotatedScaled, 90F)),
    "已旋转元素缩放后仍限制在截图编辑区域内");

using (var scalingDocument = new AnnotationDocument())
using (var scalingSource = CreateSolidBitmap(new Size(300, 240), Color.Black))
{
    var scaledRectangle = new ShapeAnnotation(
        EditorTool.Rectangle,
        scaleOrigin,
        Color.Red,
        3F);
    var scaledText = new TextAnnotation(
        new Rectangle(30, 30, 100, 40),
        "缩放文字",
        Color.White,
        20F);
    var scaledPastedText = new PastedTextAnnotation(
        new Rectangle(30, 180, 100, 40),
        "缩放粘贴文字");
    scalingDocument.Add(scaledRectangle);
    scalingDocument.Add(scaledText);
    scalingDocument.Add(scaledPastedText);

    scaledRectangle.SetBounds(AnnotationScaling.ScaleAt(
        scaledRectangle.Bounds,
        scaledRectangle.RotationDegrees,
        new Point(140, 120),
        1.5D,
        scalingLimits));
    scaledText.SetBounds(AnnotationScaling.ScaleAt(
        scaledText.Bounds,
        scaledText.RotationDegrees,
        new Point(80, 50),
        1.5D,
        scalingLimits));
    scaledPastedText.SetBounds(AnnotationScaling.ScaleAt(
        scaledPastedText.Bounds,
        scaledPastedText.RotationDegrees,
        new Point(80, 200),
        1.5D,
        scalingLimits));
    AssertTrue(Math.Abs(scaledText.FontSize - 30F) < 0.001F, "普通文字缩放时同步调整字体");
    AssertTrue(
        Math.Abs(scaledPastedText.FontSize - PastedTextAnnotation.DefaultFontSize * 1.5F) < 0.001F,
        "粘贴文字缩放时同步调整字体");

    using var scaledExport = RenderDocumentSelection(
        scalingDocument,
        scalingSource,
        new Rectangle(Point.Empty, scalingSource.Size));
    AssertTrue(
        ContainsPixelDifferentFrom(scaledExport, new Rectangle(78, 88, 10, 65), Color.Black),
        "最终导出位图包含 Ctrl 加滚轮放大到原边界外的元素像素");
}

using (var groupTransformDocument = new AnnotationDocument())
{
    var firstSelected = new ShapeAnnotation(
        EditorTool.Rectangle,
        new Rectangle(40, 40, 40, 20),
        Color.Red,
        2F);
    var secondSelected = new TextAnnotation(
        new Rectangle(140, 80, 60, 30),
        "全选联动",
        Color.White,
        18F);
    var thirdSelected = new ShapeAnnotation(
        EditorTool.Ellipse,
        new Rectangle(230, 160, 30, 20),
        Color.Blue,
        2F);
    var outsideSelection = new ShapeAnnotation(
        EditorTool.Rectangle,
        new Rectangle(10, 180, 20, 20),
        Color.Green,
        2F);
    groupTransformDocument.Add(firstSelected);
    groupTransformDocument.Add(secondSelected);
    groupTransformDocument.Add(thirdSelected);

    var ctrlASelection = new AnnotationSelection();
    ctrlASelection.SelectAll(groupTransformDocument.GetMovableAnnotations());
    var selectedTargets = ctrlASelection.GetTransformTargets(firstSelected);
    AssertEqual(3, selectedTargets.Count, "Ctrl+A 全选状态下命中一个元素会解析全部编辑元素");
    AssertEqual(
        1,
        ctrlASelection.GetTransformTargets(outsideSelection).Count,
        "鼠标下元素不属于多选组时仅操作该元素");

    foreach (var target in selectedTargets)
    {
        target.RotateBy(15F);
    }
    AssertEqual(15F, firstSelected.RotationDegrees, "多选旋转更新鼠标下元素");
    AssertEqual(15F, secondSelected.RotationDegrees, "多选旋转同步更新其他选中元素");
    AssertEqual(15F, thirdSelected.RotationDegrees, "Ctrl+A 旋转同步更新全部编辑元素");
    AssertEqual(0F, outsideSelection.RotationDegrees, "多选旋转不影响未选中元素");

    var groupScaledBounds = AnnotationScaling.ScaleGroupAt(
        selectedTargets,
        new Point(50, 50),
        requestedFactor: 1.1D,
        limits: new Rectangle(0, 0, 320, 240));
    foreach (var target in selectedTargets)
    {
        target.SetBounds(groupScaledBounds[target]);
    }
    AssertEqual(new Size(44, 22), firstSelected.Bounds.Size, "多选缩放更新鼠标下元素尺寸");
    AssertEqual(new Size(66, 33), secondSelected.Bounds.Size, "多选缩放同步更新其他选中元素尺寸");
    AssertEqual(new Size(33, 22), thirdSelected.Bounds.Size, "Ctrl+A 缩放同步更新全部编辑元素尺寸");
    AssertTrue(firstSelected.Bounds.X < 40, "多选缩放以鼠标位置同步调整组内近端元素位置");
    AssertTrue(secondSelected.Bounds.X > 140, "多选缩放保持远端元素的相对布局");
    AssertTrue(Math.Abs(secondSelected.FontSize - 19.8F) < 0.001F, "多选缩放同步调整选中文字字体");
    AssertEqual(new Rectangle(10, 180, 20, 20), outsideSelection.Bounds, "多选缩放不影响未选中元素");
}

using (var drawingMoveDocument = new AnnotationDocument())
{
    var rectangle = new ShapeAnnotation(
        EditorTool.Rectangle,
        new Rectangle(120, 120, 60, 40),
        Color.Red,
        3F);
    var ellipse = new ShapeAnnotation(
        EditorTool.Ellipse,
        new Rectangle(120, 200, 70, 45),
        Color.Green,
        3F);
    var arrow = new ArrowAnnotation(
        new Point(220, 120),
        new Point(300, 170),
        Color.Blue,
        4F);
    var freehand = new FreehandAnnotation(
        [new Point(300, 250), new Point(320, 260), new Point(350, 255)],
        Color.Yellow,
        4F);
    drawingMoveDocument.Add(rectangle);
    drawingMoveDocument.Add(ellipse);
    drawingMoveDocument.Add(arrow);
    drawingMoveDocument.Add(freehand);

    AssertEqual(AnnotationCategory.Drawing, rectangle.Category, "方框移动后仍属于普通绘制标注");
    AssertEqual(AnnotationCategory.Drawing, arrow.Category, "箭头移动后仍属于普通绘制标注");
    AssertTrue(ReferenceEquals(rectangle, drawingMoveDocument.FindTopMovableAt(new Point(140, 140), 6)), "方框支持直接命中移动");
    AssertTrue(ReferenceEquals(ellipse, drawingMoveDocument.FindTopMovableAt(new Point(150, 220), 6)), "椭圆支持直接命中移动");
    AssertTrue(ReferenceEquals(arrow, drawingMoveDocument.FindTopMovableAt(new Point(260, 145), 6)), "箭头按线段精确命中移动");
    AssertTrue(ReferenceEquals(freehand, drawingMoveDocument.FindTopMovableAt(new Point(320, 260), 6)), "画笔线条可在不移动时正常选中");
    foreach (var annotation in new MovableAnnotation[] { rectangle, ellipse, arrow, freehand })
    {
        AssertTrue(annotation.SupportsResize, $"{annotation.GetType().Name} 显示四角缩放手柄");
        AssertTrue(!annotation.PreserveAspectRatioWhenResizing, $"{annotation.GetType().Name} 可独立调整宽度和高度");
        AssertTrue(!annotation.CanMove(altPressed: false), $"{annotation.GetType().Name} 拒绝只用鼠标左键移动");
        AssertTrue(annotation.CanMove(altPressed: true), $"{annotation.GetType().Name} 支持 Alt 加鼠标左键移动");
    }

    rectangle.SetBounds(StickerLayout.Move(rectangle.Bounds, new Point(25, 15), selection));
    AssertEqual(new Rectangle(145, 135, 60, 40), rectangle.Bounds, "方框可以通过 Alt 加鼠标左键拖动");
    ellipse.SetBounds(StickerLayout.Move(ellipse.Bounds, new Point(20, -20), selection));
    AssertEqual(new Rectangle(140, 180, 70, 45), ellipse.Bounds, "椭圆可以拖动");
    arrow.SetBounds(StickerLayout.Move(arrow.Bounds, new Point(15, 10), selection));
    AssertEqual(new Point(235, 130), arrow.Start, "箭头拖动时起点同步移动");
    AssertEqual(new Point(315, 180), arrow.End, "箭头拖动时终点同步移动");
    freehand.SetBounds(StickerLayout.Move(freehand.Bounds, new Point(-20, 15), selection));
    AssertEqual(new Point(280, 265), freehand.Points[0], "Alt 拖动线条时全部轨迹点同步移动");

    using var source = new Bitmap(520, 420);
    using var exported = RenderDocumentSelection(drawingMoveDocument, source, selection);
    AssertTrue(
        ContainsPixelDifferentFrom(exported, new Rectangle(40, 30, 100, 70), Color.Black),
        "最终导出包含移动后的方框绘制结果");
    AssertTrue(
        ContainsPixelDifferentFrom(exported, new Rectangle(35, 75, 90, 60), Color.Black),
        "最终导出包含移动后的椭圆绘制结果");
    AssertTrue(
        ContainsPixelDifferentFrom(exported, new Rectangle(130, 25, 105, 65), Color.Black),
        "最终导出包含移动后的箭头绘制结果");
    AssertTrue(
        ContainsPixelDifferentFrom(exported, new Rectangle(175, 155, 85, 45), Color.Black),
        "最终导出包含 Alt 移动后的画笔线条");

    var multiSelection = new AnnotationSelection();
    multiSelection.Add(rectangle);
    multiSelection.Add(arrow);
    multiSelection.Add(freehand);
    AssertEqual(3, multiSelection.Count, "Ctrl+左键可以累加选中多个编辑元素");
    AssertTrue(multiSelection.RequiresAltToMove, "所有多选组拖动都需要 Alt");
    var groupOrigins = multiSelection.Items.ToDictionary(
        annotation => annotation,
        annotation => annotation.Bounds);
    var groupOffset = GroupMoveLayout.ClampOffset(
        multiSelection.Bounds,
        new Point(10, 10),
        selection);
    foreach (var (annotation, origin) in groupOrigins)
    {
        annotation.SetBounds(new Rectangle(
            origin.X + groupOffset.X,
            origin.Y + groupOffset.Y,
            origin.Width,
            origin.Height));
    }
    AssertEqual(new Rectangle(155, 145, 60, 40), rectangle.Bounds, "多选拖动统一移动方框");
    AssertEqual(new Point(245, 140), arrow.Start, "多选拖动统一移动箭头");
    AssertEqual(new Point(290, 275), freehand.Points[0], "Alt 多选拖动统一移动画笔线条");
    var marqueeSelection = new AnnotationSelection();
    marqueeSelection.SelectIntersecting(
        drawingMoveDocument.GetMovableAnnotations(),
        new Rectangle(150, 130, 190, 45));
    AssertEqual(2, marqueeSelection.Count, "鼠标左键拖拽框选区域内的多个编辑元素");
    AssertTrue(marqueeSelection.Contains(rectangle), "拖拽框选命中相交的方框");
    AssertTrue(marqueeSelection.Contains(arrow), "拖拽框选命中相交的箭头");
    AssertTrue(!marqueeSelection.Contains(ellipse), "拖拽框选不会选中区域外的椭圆");
    AssertTrue(!marqueeSelection.Contains(freehand), "拖拽框选不会选中区域外的画笔线条");
    using (var groupMovedExport = RenderDocumentSelection(drawingMoveDocument, source, selection))
    {
        AssertTrue(
            ContainsPixelDifferentFrom(groupMovedExport, new Rectangle(50, 40, 75, 55), Color.Black),
            "最终导出包含整组移动后的方框");
        AssertTrue(
            ContainsPixelDifferentFrom(groupMovedExport, new Rectangle(140, 35, 105, 65), Color.Black),
            "最终导出包含整组移动后的箭头");
        AssertTrue(
            ContainsPixelDifferentFrom(groupMovedExport, new Rectangle(185, 165, 85, 45), Color.Black),
            "最终导出包含整组移动后的画笔线条");
    }
    AssertTrue(multiSelection.Remove(freehand), "Ctrl+左键可以取消单个元素的选中状态");
    AssertTrue(multiSelection.RequiresAltToMove, "多选组不含线条时仍需要 Alt 才能拖动");
    multiSelection.Add(freehand);
    AssertEqual(3, drawingMoveDocument.Remove(multiSelection.Items), "Delete 一次删除全部多选元素");
    multiSelection.Clear();
    AssertEqual(1, drawingMoveDocument.Count, "Delete 后保留未选中的编辑元素");
    using var deletedExport = RenderDocumentSelection(drawingMoveDocument, source, selection);
    AssertTrue(
        !ContainsPixelDifferentFrom(deletedExport, new Rectangle(140, 35, 105, 65), Color.Black),
        "最终导出不再包含 Delete 删除的多选元素");
    AssertTrue(
        ContainsPixelDifferentFrom(deletedExport, new Rectangle(35, 75, 90, 60), Color.Black),
        "最终导出保留未选中的编辑元素");
}

using (var overlappingDocument = new AnnotationDocument())
{
    var bottom = new ShapeAnnotation(
        EditorTool.Rectangle,
        new Rectangle(40, 40, 80, 80),
        Color.Red,
        3F);
    var middle = new ShapeAnnotation(
        EditorTool.Ellipse,
        new Rectangle(40, 40, 80, 80),
        Color.Green,
        3F);
    var top = new ShapeAnnotation(
        EditorTool.Rectangle,
        new Rectangle(40, 40, 80, 80),
        Color.Blue,
        3F);
    overlappingDocument.Add(bottom);
    overlappingDocument.Add(middle);
    overlappingDocument.Add(top);

    var overlapPoint = new Point(70, 70);
    var candidates = overlappingDocument.FindMovablesAt(overlapPoint, 0);
    AssertEqual(3, candidates.Count, "重叠区域返回全部可选元素");
    AssertTrue(ReferenceEquals(top, candidates[0]), "重叠元素命中顺序从最上层开始");
    AssertTrue(ReferenceEquals(middle, candidates[1]), "重叠元素命中顺序包含中间层");
    AssertTrue(ReferenceEquals(bottom, candidates[2]), "重叠元素命中顺序最后为底层");

    var hitCycle = new AnnotationHitCycle();
    AssertTrue(ReferenceEquals(top, hitCycle.SelectNext(overlapPoint, candidates, 5)), "首次单击选中最上层元素");
    AssertTrue(ReferenceEquals(middle, hitCycle.SelectNext(overlapPoint, candidates, 5)), "同一区域再次单击选中中间层");
    AssertTrue(ReferenceEquals(bottom, hitCycle.SelectNext(overlapPoint, candidates, 5)), "同一区域继续单击选中底层");
    AssertTrue(ReferenceEquals(top, hitCycle.SelectNext(overlapPoint, candidates, 5)), "重叠元素轮换到底后回到最上层");
    AssertTrue(
        ReferenceEquals(middle, hitCycle.SelectNext(new Point(72, 71), candidates, 5)),
        "同一小区域内的轻微鼠标偏移仍继续轮换");
    AssertTrue(
        ReferenceEquals(top, hitCycle.SelectNext(new Point(90, 90), candidates, 5)),
        "鼠标明显移到新位置后从最上层重新开始");

    overlappingDocument.Add(new ShapeAnnotation(
        EditorTool.Rectangle,
        new Rectangle(40, 40, 80, 80),
        Color.White,
        3F));
    var changedCandidates = overlappingDocument.FindMovablesAt(new Point(90, 90), 0);
    AssertTrue(
        ReferenceEquals(changedCandidates[0], hitCycle.SelectNext(new Point(90, 90), changedCandidates, 5)),
        "重叠元素集合变化后从新的最上层重新开始");
    AssertTrue(hitCycle.SelectNext(new Point(200, 200), [], 5) is null, "点击空白区域会重置重叠元素轮换");
}

using (var resizeDocument = new AnnotationDocument())
using (var resizeSource = CreateSolidBitmap(new Size(520, 420), Color.Magenta))
{
    var rectangle = new ShapeAnnotation(
        EditorTool.Rectangle,
        new Rectangle(120, 120, 60, 40),
        Color.Red,
        3F);
    var arrow = new ArrowAnnotation(
        new Point(260, 130),
        new Point(320, 130),
        Color.Blue,
        4F);
    var freehand = new FreehandAnnotation(
        [new Point(150, 250), new Point(180, 250), new Point(210, 250)],
        Color.Yellow,
        4F);
    var mosaic = new MosaicAnnotation([new Point(350, 250)], 4F);
    resizeDocument.Add(rectangle);
    resizeDocument.Add(arrow);
    resizeDocument.Add(freehand);
    resizeDocument.Add(mosaic);

    rectangle.SetBounds(new Rectangle(120, 120, 120, 90));
    AssertEqual(new Rectangle(120, 120, 120, 90), rectangle.Bounds, "方框缩放后更新边长");
    arrow.SetBounds(new Rectangle(240, 150, 121, 61));
    AssertEqual(new Point(240, 150), arrow.Start, "水平箭头缩放后起点映射到新边界");
    AssertEqual(new Point(360, 210), arrow.End, "水平箭头缩放后终点映射到新边界");
    freehand.SetBounds(new Rectangle(150, 230, 121, 61));
    AssertEqual(new Point(150, 230), freehand.Points[0], "水平画笔线条缩放后起点映射到新边界");
    AssertEqual(new Point(270, 290), freehand.Points[^1], "水平画笔线条缩放后终点映射到新边界");
    var resizedMosaicBounds = new Rectangle(
        mosaic.Bounds.X,
        mosaic.Bounds.Y,
        mosaic.Bounds.Width * 2,
        mosaic.Bounds.Height + 20);
    mosaic.SetBounds(resizedMosaicBounds);
    AssertEqual(resizedMosaicBounds, mosaic.Bounds, "马赛克缩放后更新宽度和高度");
    AssertTrue(mosaic.SupportsResize, "马赛克显示四角缩放手柄");

    using var resizedExport = RenderDocumentSelection(resizeDocument, resizeSource, selection);
    AssertTrue(
        ContainsPixelDifferentFrom(resizedExport, new Rectangle(15, 15, 130, 100), Color.Black),
        "最终导出包含缩放后的方框");
    AssertTrue(
        ContainsPixelDifferentFrom(resizedExport, new Rectangle(135, 45, 135, 75), Color.Black),
        "最终导出包含缩放后的箭头");
    AssertTrue(
        ContainsPixelDifferentFrom(resizedExport, new Rectangle(45, 125, 130, 80), Color.Black),
        "最终导出包含缩放后的画笔线条");
    AssertTrue(
        ContainsPixelDifferentFrom(
            resizedExport,
            new Rectangle(
                mosaic.Bounds.X - selection.X,
                mosaic.Bounds.Y - selection.Y,
                mosaic.Bounds.Width,
                mosaic.Bounds.Height),
            Color.Black),
        "最终导出包含缩放后的马赛克");
}

AssertEqual(
    new Point(100, 100),
    GroupMoveLayout.ClampOffset(
        new Rectangle(100, 100, 300, 200),
        new Point(500, 500),
        selection),
    "多选拖动在截图边界处保持整组相对位置");

var reversedRange = ToolWidthRange.Create(9, 3);
AssertEqual(3, reversedRange.Minimum, "粗细范围自动纠正最小值");
AssertEqual(9, reversedRange.Maximum, "粗细范围自动纠正最大值");
var supportedRange = ToolWidthRange.Create(-5, 100);
AssertEqual(ToolWidthRange.SupportedMinimum, supportedRange.Minimum, "粗细范围限制系统下界");
AssertEqual(ToolWidthRange.SupportedMaximum, supportedRange.Maximum, "粗细范围限制系统上界");
var widthController = new ToolWidthController(ToolWidthRange.Create(3, 6));
AssertEqual(4, widthController.Current, "粗细控制器使用范围内初始值");
var rememberedWidthController = new ToolWidthController(ToolWidthRange.Create(2, 8), 7);
AssertEqual(7, rememberedWidthController.Current, "粗细控制器恢复上次使用值");
var clampedRememberedWidthController = new ToolWidthController(ToolWidthRange.Create(3, 6), 20);
AssertEqual(6, clampedRememberedWidthController.Current, "恢复的粗细超出范围时自动夹紧");
var widthPreferences = new UserPreferences
{
    MinimumToolWidth = 3,
    MaximumToolWidth = 6,
    LastToolWidth = 20
};
AssertTrue(
    widthPreferences.DismissSaveNotificationBeforeCapture,
    "默认在下一次截图前关闭保存提示");
AssertTrue(
    !widthPreferences.HideMainWindowDuringCapture,
    "默认截图时保留轻截主界面以便宣传展示");
AssertTrue(
    typeof(AppIcon).Assembly.GetManifestResourceNames().Contains(
        "ScreenshotTool.Assets.LightShotIcon.ico",
        StringComparer.Ordinal),
    "轻截应用图标作为程序集资源内嵌");
using (var appIconBitmap = AppIcon.Shared.ToBitmap())
{
    AssertTrue(appIconBitmap.Width > 0 && appIconBitmap.Height > 0, "轻截应用图标可以在运行时加载");
}
AssertTrue(
    !MainWindowCaptureVisibilityPolicy.ShouldHide(
        hideMainWindowDuringCapture: false,
        isVisible: true,
        FormWindowState.Normal),
    "关闭开关时可截图轻截主界面");
AssertTrue(
    MainWindowCaptureVisibilityPolicy.ShouldHide(
        hideMainWindowDuringCapture: true,
        isVisible: true,
        FormWindowState.Normal),
    "开启开关且主界面可见时隐藏主界面");
AssertTrue(
    !MainWindowCaptureVisibilityPolicy.ShouldHide(
        hideMainWindowDuringCapture: true,
        isVisible: true,
        FormWindowState.Minimized),
    "主界面已最小化时无需重复执行隐藏动画");
AssertEqual(6, widthPreferences.GetLastToolWidth(), "上次粗细始终按当前范围读取");
AssertTrue(widthPreferences.RememberToolWidth(5), "新的粗细值会写入用户偏好");
AssertTrue(!widthPreferences.RememberToolWidth(5), "相同粗细值不会触发重复保存");
AssertTrue(widthController.Adjust(1), "滚轮增大粗细");
AssertEqual(5, widthController.Current, "滚轮增大后的粗细");
AssertTrue(widthController.Adjust(100), "滚轮增大时夹紧到上限");
AssertEqual(6, widthController.Current, "粗细不超过用户上限");
AssertTrue(!widthController.Adjust(1), "达到上限后不再变化");
AssertTrue(widthController.Adjust(-2), "滚轮减小粗细");
AssertEqual(4, widthController.Current, "滚轮减小后的粗细");
var presetController = new ToolWidthController(ToolWidthRange.Create(2, 8));
AssertTrue(presetController.CyclePreset(), "按钮从默认粗细切换到上限");
AssertEqual(8, presetController.Current, "按钮循环保留默认上限行为");
AssertTrue(presetController.CyclePreset(), "按钮从上限切换到下限");
AssertEqual(2, presetController.Current, "按钮循环使用用户下限");
AssertTrue(presetController.CyclePreset(), "按钮从下限切换到常用粗细");
AssertEqual(4, presetController.Current, "按钮循环使用范围内常用粗细");

using (var thinArrowBitmap = RenderArrowForWidth(2F))
using (var thickArrowBitmap = RenderArrowForWidth(10F))
{
    var thinBodyThickness = CountNonBlackPixelsInColumn(thinArrowBitmap, 50);
    var thickBodyThickness = CountNonBlackPixelsInColumn(thickArrowBitmap, 50);
    AssertTrue(
        thickBodyThickness >= thinBodyThickness + 5,
        "箭头粗细会同步改变箭身宽度");
    var headBounds = new Rectangle(105, 0, 55, 80);
    AssertTrue(
        CountNonBlackPixels(thickArrowBitmap, headBounds) >
        CountNonBlackPixels(thinArrowBitmap, headBounds) * 2,
        "箭头粗细会同步改变箭头头部大小");
    var thinHeadToBodyRatio = MaxNonBlackPixelsInColumn(thinArrowBitmap, 105, 140) /
        (double)thinBodyThickness;
    var thickHeadToBodyRatio = MaxNonBlackPixelsInColumn(thickArrowBitmap, 105, 140) /
        (double)thickBodyThickness;
    AssertTrue(
        Math.Abs(thickHeadToBodyRatio - thinHeadToBodyRatio) <= 1.5D,
        "不同粗细下箭头头部与箭身保持稳定比例");
}

var configuredCoefficients = new DrawingToolCoefficients
{
    Rectangle = 1.5M,
    Ellipse = 0.75M,
    ArrowBody = 1.25M,
    ArrowHeadWidth = 4M,
    ArrowHeadLength = 5M,
    Pen = 0.5M,
    Mosaic = 2M
};
using (var coefficientEditor = new CaptureAnnotationEditor(configuredCoefficients))
{
    var rectangleDraft = (ShapeAnnotation)coefficientEditor.BuildDraft(
        EditorTool.Rectangle, new Point(10, 10), new Point(80, 60), [], Color.Red, 4F)!;
    var ellipseDraft = (ShapeAnnotation)coefficientEditor.BuildDraft(
        EditorTool.Ellipse, new Point(10, 10), new Point(80, 60), [], Color.Red, 4F)!;
    var arrowDraft = (ArrowAnnotation)coefficientEditor.BuildDraft(
        EditorTool.Arrow, new Point(10, 10), new Point(80, 60), [], Color.Red, 4F)!;
    var penDraft = (FreehandAnnotation)coefficientEditor.BuildDraft(
        EditorTool.Pen, Point.Empty, new Point(10, 10), [Point.Empty, new Point(10, 10)], Color.Red, 4F)!;
    var mosaicDraft = (MosaicAnnotation)coefficientEditor.BuildDraft(
        EditorTool.Mosaic, Point.Empty, new Point(10, 10), [Point.Empty], Color.Red, 4F)!;
    AssertEqual(6F, rectangleDraft.Width, "矩形基础系数乘以粗细倍率");
    AssertEqual(3F, ellipseDraft.Width, "椭圆基础系数乘以粗细倍率");
    AssertEqual(5F, arrowDraft.Width, "箭身基础系数乘以粗细倍率");
    AssertEqual(16F, arrowDraft.HeadWidth, "箭头宽度基础系数乘以粗细倍率");
    AssertEqual(20F, arrowDraft.HeadLength, "箭头长度基础系数乘以粗细倍率");
    AssertEqual(2F, penDraft.Width, "画笔基础系数乘以粗细倍率");
    AssertEqual(8F, mosaicDraft.Width, "马赛克基础系数乘以粗细倍率");
    AssertEqual(2F, coefficientEditor.GetDrawingCursorDiameter(EditorTool.Pen, 4F), "画笔光标使用系数换算后的粗细");
    AssertEqual(48F, coefficientEditor.GetDrawingCursorDiameter(EditorTool.Mosaic, 4F), "马赛克光标覆盖实际像素化作用半径");
}

var settingsTestDirectory = Path.Combine(
    Path.GetTempPath(),
    "ScreenshotTool.SettingsTests",
    Guid.NewGuid().ToString("N"));
try
{
    var startupStore = new JsonSettingsStore(
        Path.Combine(settingsTestDirectory, "startup"),
        "local");
    var firstLaunch = new StartupWorkspaceService(startupStore, currentProductVersion)
        .PrepareLaunch();
    AssertEqual(
        StartupWorkspaceReason.FirstRun,
        firstLaunch.Reason,
        "首次启动服务要求显示设置工作台");
    AssertEqual(
        "1.10.0",
        firstLaunch.Settings.LastLaunchedVersion ?? string.Empty,
        "首次启动服务记录当前版本");
    AssertEqual(
        "1.10.0",
        startupStore.Load().LastLaunchedVersion ?? string.Empty,
        "首次启动版本标记持久化到用户配置");

    var repeatedLaunch = new StartupWorkspaceService(startupStore, currentProductVersion)
        .PrepareLaunch();
    AssertEqual(
        StartupWorkspaceReason.None,
        repeatedLaunch.Reason,
        "同版本再次启动不重复显示设置工作台");

    var updatedLaunch = new StartupWorkspaceService(
        startupStore,
        new Version(1, 11, 0, 0))
        .PrepareLaunch();
    AssertEqual(
        StartupWorkspaceReason.VersionChanged,
        updatedLaunch.Reason,
        "新版本首次启动要求显示设置工作台");
    AssertEqual(
        "1.11.0",
        startupStore.Load().LastLaunchedVersion ?? string.Empty,
        "更新启动后持久化新的产品版本");

    var profileStore = new JsonSettingsStore(settingsTestDirectory, "account-demo");
    var configuredSettings = new AppSettings
    {
        OutputFolder = Path.Combine(settingsTestDirectory, "captures"),
        StartMinimized = true,
        LastLaunchedVersion = " 1.9.3 ",
        HotkeyModifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt,
        HotkeyVirtualKey = (int)Keys.Q,
        Preferences = new UserPreferences
        {
            StickerSelectionMoveMode = StickerSelectionMoveMode.KeepScreenPosition,
            MinimumToolWidth = 3,
            MaximumToolWidth = 17,
            LastToolWidth = 11,
            AnnotationRotationStepDegrees = 12,
            DrawingCursorShape = DrawingCursorShape.Square,
            AnnotationSnappingEnabled = false,
            AnnotationSnapThresholdPixels = 14,
            CtrlDragStepPixels = 20,
            RecordingRegionIndicatorStyle = RecordingRegionIndicatorStyle.Solid,
            ScreenRecordingCaptureSystemAudio = false,
            ScreenRecordingCaptureMicrophone = true,
            ScreenRecordingShowMouseClickIndicator = false,
            ScreenRecordingFramesPerSecond = 60,
            ScreenRecordingVideoBitrate = 20_000_000,
            LongCaptureSafetyChecksEnabled = true,
            ModuleStringPreferences = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["tests.module.caption"] = "模块自带设置"
            },
            ScreenshotFileNameMode = ScreenshotFileNameMode.ImageText,
            DismissSaveNotificationBeforeCapture = false,
            HideMainWindowDuringCapture = true,
            DrawingToolCoefficients = configuredCoefficients
        }
    };
    profileStore.Save(configuredSettings);
    var savedJson = File.ReadAllText(profileStore.SettingsPath);
    AssertTrue(savedJson.Contains("\"schemaVersion\": 1", StringComparison.Ordinal), "JSON 保存配置版本");
    AssertTrue(savedJson.Contains("\"profileId\": \"account-demo\"", StringComparison.Ordinal), "JSON 保存配置身份");
    AssertTrue(savedJson.Contains("\"lastLaunchedVersion\": \"1.9.3\"", StringComparison.Ordinal), "JSON 保存并规范化上次启动版本");
    AssertTrue(savedJson.Contains("\"preferences\"", StringComparison.Ordinal), "JSON 独立保存用户偏好");
    AssertTrue(savedJson.Contains("\"keepScreenPosition\"", StringComparison.Ordinal), "JSON 使用可读的贴纸模式");
    AssertTrue(savedJson.Contains("\"longCaptureSafetyChecksEnabled\": true", StringComparison.Ordinal), "JSON 保存长截图安全开关");
    AssertTrue(
        savedJson.Contains("\"moduleBooleanPreferences\"", StringComparison.Ordinal) &&
        savedJson.Contains("\"moduleStringPreferences\"", StringComparison.Ordinal),
        "JSON 保存通用模块偏好字典");
    AssertTrue(savedJson.Contains("\"screenshotFileNameMode\": \"imageText\"", StringComparison.Ordinal), "JSON 保存图片命名规则");
    AssertTrue(savedJson.Contains("\"dismissSaveNotificationBeforeCapture\": false", StringComparison.Ordinal), "JSON 保存截图前关闭提示开关");
    AssertTrue(savedJson.Contains("\"hideMainWindowDuringCapture\": true", StringComparison.Ordinal), "JSON 保存截图时隐藏主界面开关");
    AssertTrue(savedJson.Contains("\"annotationSnappingEnabled\": false", StringComparison.Ordinal), "JSON 保存元素吸附开关");
    AssertTrue(savedJson.Contains("\"annotationSnapThresholdPixels\": 14", StringComparison.Ordinal), "JSON 保存元素吸附距离");
    AssertTrue(savedJson.Contains("\"ctrlDragStepPixels\": 20", StringComparison.Ordinal), "JSON 保存 Ctrl 拖动步长");
    AssertTrue(
        savedJson.Contains(
            "\"recordingRegionIndicatorStyle\": \"solid\"",
            StringComparison.Ordinal),
        "JSON 保存录屏范围提示样式");
    AssertTrue(
        savedJson.Contains("\"screenRecordingCaptureSystemAudio\": false", StringComparison.Ordinal),
        "JSON 保存录屏系统声音设置");
    AssertTrue(
        savedJson.Contains("\"screenRecordingCaptureMicrophone\": true", StringComparison.Ordinal),
        "JSON 保存录屏麦克风设置");
    AssertTrue(
        savedJson.Contains(
            "\"screenRecordingShowMouseClickIndicator\": false",
            StringComparison.Ordinal),
        "JSON 保存录屏左键圆圈开关");
    AssertTrue(
        savedJson.Contains("\"screenRecordingFramesPerSecond\": 60", StringComparison.Ordinal),
        "JSON 保存录屏帧率");
    AssertTrue(
        savedJson.Contains("\"screenRecordingVideoBitrate\": 20000000", StringComparison.Ordinal),
        "JSON 保存录屏码率");
    AssertTrue(savedJson.Contains("\"lastToolWidth\": 11", StringComparison.Ordinal), "JSON 保存上次使用的粗细");
    AssertTrue(savedJson.Contains("\"arrowHeadWidth\": 4", StringComparison.Ordinal), "JSON 保存箭头头部基础系数");

    AssertTrue(
        savedJson.Contains("\"annotationRotationStepDegrees\": 12", StringComparison.Ordinal),
        "JSON 保存编辑元素旋转速度");
    AssertTrue(
        savedJson.Contains("\"drawingCursorShape\": \"square\"", StringComparison.Ordinal),
        "JSON 保存绘制光标形状");

    var loadedSettings = profileStore.Load();
    AssertEqual(StickerSelectionMoveMode.KeepScreenPosition,
        loadedSettings.Preferences.StickerSelectionMoveMode,
        "JSON 恢复贴纸移动喜好");
    AssertEqual(3, loadedSettings.Preferences.MinimumToolWidth, "JSON 恢复粗细下限");
    AssertEqual(17, loadedSettings.Preferences.MaximumToolWidth, "JSON 恢复粗细上限");
    AssertEqual(11, loadedSettings.Preferences.LastToolWidth, "JSON 恢复上次使用的粗细");
    AssertTrue(loadedSettings.Preferences.LongCaptureSafetyChecksEnabled, "JSON 恢复长截图安全开关");
    AssertEqual(ScreenshotFileNameMode.ImageText, loadedSettings.Preferences.ScreenshotFileNameMode, "JSON 恢复图片文字命名规则");
    AssertTrue(!loadedSettings.Preferences.DismissSaveNotificationBeforeCapture, "JSON 恢复保留保存提示的选择");
    AssertTrue(loadedSettings.Preferences.HideMainWindowDuringCapture, "JSON 恢复截图时隐藏主界面开关");
    AssertTrue(!loadedSettings.Preferences.AnnotationSnappingEnabled, "JSON 恢复元素吸附开关");
    AssertEqual(14, loadedSettings.Preferences.AnnotationSnapThresholdPixels, "JSON 恢复元素吸附距离");
    AssertEqual(20, loadedSettings.Preferences.CtrlDragStepPixels, "JSON 恢复 Ctrl 拖动步长");
    AssertEqual(
        RecordingRegionIndicatorStyle.Solid,
        loadedSettings.Preferences.RecordingRegionIndicatorStyle,
        "JSON 恢复录屏范围提示样式");
    AssertTrue(!loadedSettings.Preferences.ScreenRecordingCaptureSystemAudio, "JSON 恢复录屏系统声音设置");
    AssertTrue(loadedSettings.Preferences.ScreenRecordingCaptureMicrophone, "JSON 恢复录屏麦克风设置");
    AssertTrue(
        !loadedSettings.Preferences.ScreenRecordingShowMouseClickIndicator,
        "JSON 恢复录屏左键圆圈开关");
    AssertEqual(60, loadedSettings.Preferences.ScreenRecordingFramesPerSecond, "JSON 恢复录屏帧率");
    AssertEqual(20_000_000, loadedSettings.Preferences.ScreenRecordingVideoBitrate, "JSON 恢复录屏码率");
    AssertTrue(
        loadedSettings.Preferences.ModuleBooleanPreferences[
            LongCapturePreferences.SafetyChecksId],
        "旧版长截图偏好迁移到通用模块设置");
    AssertEqual(
        "模块自带设置",
        loadedSettings.Preferences.ModuleStringPreferences["tests.module.caption"],
        "JSON 恢复模块自带字符串设置");
    AssertEqual(1.5M, loadedSettings.Preferences.DrawingToolCoefficients.Rectangle, "JSON 恢复矩形基础系数");
    AssertEqual(1.25M, loadedSettings.Preferences.DrawingToolCoefficients.ArrowBody, "JSON 恢复箭身基础系数");
    AssertEqual(4M, loadedSettings.Preferences.DrawingToolCoefficients.ArrowHeadWidth, "JSON 恢复箭头宽度基础系数");
    AssertTrue(loadedSettings.StartMinimized, "JSON 恢复启动喜好");
    AssertEqual(
        "1.9.3",
        loadedSettings.LastLaunchedVersion ?? string.Empty,
        "JSON 恢复上次启动版本");
    AssertEqual((int)Keys.Q, loadedSettings.HotkeyVirtualKey, "JSON 恢复快捷键");

    AssertEqual(12, loadedSettings.Preferences.AnnotationRotationStepDegrees, "JSON 恢复编辑元素旋转速度");
    AssertEqual(DrawingCursorShape.Square, loadedSettings.Preferences.DrawingCursorShape, "JSON 恢复绘制光标形状");

    var invalidShapeStore = new JsonSettingsStore(settingsTestDirectory, "invalid-shape");
    invalidShapeStore.Save(new AppSettings
    {
        OutputFolder = Path.Combine(settingsTestDirectory, "invalid-shape-captures"),
        Preferences = new UserPreferences
        {
            DrawingCursorShape = (DrawingCursorShape)99,
            RecordingRegionIndicatorStyle = (RecordingRegionIndicatorStyle)99,
            ScreenshotFileNameMode = (ScreenshotFileNameMode)99,
            AnnotationSnapThresholdPixels = -50,
            CtrlDragStepPixels = 500,
            ScreenRecordingFramesPerSecond = -1,
            ScreenRecordingVideoBitrate = int.MaxValue
        }
    });
    AssertEqual(
        DrawingCursorShape.Circle,
        invalidShapeStore.Load().Preferences.DrawingCursorShape,
        "JSON 异常绘制光标形状恢复为圆形");
    AssertEqual(
        RecordingRegionIndicatorStyle.Dashed,
        invalidShapeStore.Load().Preferences.RecordingRegionIndicatorStyle,
        "JSON 异常录屏范围提示恢复为虚线");
    AssertEqual(
        30,
        invalidShapeStore.Load().Preferences.ScreenRecordingFramesPerSecond,
        "JSON 异常录屏帧率恢复到最近档位");
    AssertEqual(
        20_000_000,
        invalidShapeStore.Load().Preferences.ScreenRecordingVideoBitrate,
        "JSON 异常录屏码率恢复到最近档位");
    AssertEqual(
        ScreenshotFileNameMode.DateTime,
        invalidShapeStore.Load().Preferences.ScreenshotFileNameMode,
        "JSON 异常图片命名规则恢复为日期时间");
    AssertEqual(
        AnnotationLayoutOptions.MinimumSnapThresholdPixels,
        invalidShapeStore.Load().Preferences.AnnotationSnapThresholdPixels,
        "JSON 异常吸附距离限制到最小值");
    AssertEqual(
        AnnotationLayoutOptions.MaximumCtrlDragStepPixels,
        invalidShapeStore.Load().Preferences.CtrlDragStepPixels,
        "JSON 异常 Ctrl 拖动步长限制到最大值");

    var legacyRoot = Path.Combine(settingsTestDirectory, "legacy");
    Directory.CreateDirectory(legacyRoot);
    var legacyPath = Path.Combine(legacyRoot, "settings.json");
    File.WriteAllText(legacyPath,
        """
        {
          "OutputFolder": "C:\\LegacyCaptures",
          "HotkeyModifiers": 6,
          "HotkeyVirtualKey": 88,
          "StartMinimized": true,
          "StickerSelectionMoveMode": 1,
          "MinimumToolWidth": 5,
          "MaximumToolWidth": 19
        }
        """);
    var legacyStore = new JsonSettingsStore(legacyRoot, "local");
    var migratedSettings = legacyStore.Load();
    AssertEqual(StickerSelectionMoveMode.KeepScreenPosition,
        migratedSettings.Preferences.StickerSelectionMoveMode,
        "迁移旧 JSON 的贴纸移动喜好");
    AssertEqual(5, migratedSettings.Preferences.MinimumToolWidth, "迁移旧 JSON 的粗细下限");
    AssertEqual(19, migratedSettings.Preferences.MaximumToolWidth, "迁移旧 JSON 的粗细上限");
    AssertEqual(5, migratedSettings.Preferences.LastToolWidth, "旧 JSON 使用范围内默认粗细");
    AssertTrue(!migratedSettings.Preferences.LongCaptureSafetyChecksEnabled, "旧 JSON 默认迁移为宽松长截图");
    AssertEqual(ScreenshotFileNameMode.DateTime, migratedSettings.Preferences.ScreenshotFileNameMode, "旧 JSON 默认迁移为日期时间命名");
    AssertTrue(migratedSettings.Preferences.DismissSaveNotificationBeforeCapture, "旧 JSON 默认在截图前关闭保存提示");
    AssertTrue(!migratedSettings.Preferences.HideMainWindowDuringCapture, "旧 JSON 默认保留轻截主界面");
    AssertTrue(migratedSettings.Preferences.AnnotationSnappingEnabled, "旧 JSON 默认开启元素吸附");
    AssertEqual(8, migratedSettings.Preferences.AnnotationSnapThresholdPixels, "旧 JSON 使用默认吸附距离");
    AssertEqual(10, migratedSettings.Preferences.CtrlDragStepPixels, "旧 JSON 使用默认 Ctrl 拖动步长");
    AssertEqual(DrawingCursorShape.Circle, migratedSettings.Preferences.DrawingCursorShape, "旧 JSON 默认迁移为圆形绘制光标");
    AssertTrue(File.Exists(legacyStore.SettingsPath), "旧 JSON 自动生成版本化配置文件");
    AssertTrue(File.Exists(legacyPath), "迁移后保留旧 JSON 作为兼容备份");
}
finally
{
    if (Directory.Exists(settingsTestDirectory))
    {
        Directory.Delete(settingsTestDirectory, recursive: true);
    }
}

using (var followDocument = new AnnotationDocument())
{
    var drawing = new ShapeAnnotation(EditorTool.Rectangle, new Rectangle(10, 10, 30, 20), Color.Red, 2F);
    var imageSticker = new StickerAnnotation(new Bitmap(20, 20), new Rectangle(20, 20, 20, 20));
    var textSticker = new PastedTextAnnotation(new Rectangle(30, 30, 80, 36), "跟随");
    var transparentText = new TextAnnotation(new Rectangle(40, 40, 90, 40), "普通文字", Color.White, 18F);
    followDocument.Add(drawing);
    followDocument.Add(imageSticker);
    followDocument.Add(textSticker);
    followDocument.Add(transparentText);

    AssertEqual(AnnotationCategory.All, FollowSelectionStrategy.Instance.MovedCategories, "跟随模式声明全部标注随截图框移动");
    AssertEqual(1, followDocument.GetVisualAreas(AnnotationCategory.Drawing).Count, "绘制标注视觉区域可单独查询");
    AssertEqual(3, followDocument.GetVisualAreas(AnnotationCategory.Sticker).Count, "贴纸视觉区域可单独查询");
    FollowSelectionStrategy.Instance.Apply(followDocument, new Point(15, 8));

    AssertEqual(new Rectangle(25, 18, 30, 20), drawing.Bounds, "跟随模式移动普通标注");
    AssertEqual(new Rectangle(35, 28, 20, 20), imageSticker.Bounds, "跟随模式移动图片贴纸");
    AssertEqual(new Rectangle(45, 38, 80, 36), textSticker.Bounds, "跟随模式移动文字贴纸");
    AssertEqual(new Rectangle(55, 48, 90, 40), transparentText.Bounds, "跟随模式移动透明文字");
}

using (var keepDocument = new AnnotationDocument())
{
    var drawing = new ShapeAnnotation(EditorTool.Rectangle, new Rectangle(10, 10, 30, 20), Color.Red, 2F);
    var imageSticker = new StickerAnnotation(new Bitmap(20, 20), new Rectangle(20, 20, 20, 20));
    var textSticker = new PastedTextAnnotation(new Rectangle(30, 30, 80, 36), "保留");
    var transparentText = new TextAnnotation(new Rectangle(40, 40, 90, 40), "普通文字", Color.White, 18F);
    keepDocument.Add(drawing);
    keepDocument.Add(imageSticker);
    keepDocument.Add(textSticker);
    keepDocument.Add(transparentText);

    AssertEqual(AnnotationCategory.Drawing, KeepStickersAtScreenPositionStrategy.Instance.MovedCategories, "固定模式只声明绘制标注随截图框移动");
    KeepStickersAtScreenPositionStrategy.Instance.Apply(keepDocument, new Point(15, 8));

    AssertEqual(new Rectangle(25, 18, 30, 20), drawing.Bounds, "固定模式仍移动普通标注");
    AssertEqual(new Rectangle(20, 20, 20, 20), imageSticker.Bounds, "固定模式保留图片贴纸坐标");
    AssertEqual(new Rectangle(30, 30, 80, 36), textSticker.Bounds, "固定模式保留文字贴纸坐标");
    AssertEqual(new Rectangle(40, 40, 90, 40), transparentText.Bounds, "固定模式保留透明文字坐标");
    AssertEqual(4, keepDocument.Count, "固定模式不会删除越界贴纸");
}

using (var visualAreaDocument = new AnnotationDocument())
{
    var mosaic = new MosaicAnnotation([new Point(70, 60)], 4F);
    var mosaicBounds = mosaic.VisualBounds;
    visualAreaDocument.Add(new ShapeAnnotation(
        EditorTool.Rectangle,
        new Rectangle(20, 20, 30, 20),
        Color.Red,
        4F));
    visualAreaDocument.Add(mosaic);
    visualAreaDocument.Add(new StickerAnnotation(
        CreateSolidBitmap(new Size(10, 10), Color.Blue),
        new Rectangle(90, 30, 10, 10)));

    AssertEqual(2, visualAreaDocument.GetVisualAreas(AnnotationCategory.Drawing).Count, "截图框移动重绘覆盖普通图形和马赛克");
    AssertEqual(1, visualAreaDocument.GetVisualAreas(AnnotationCategory.Sticker).Count, "截图框移动重绘可按策略排除贴纸");
    var selectableElements = visualAreaDocument.GetMovableAnnotations();
    AssertEqual(3, selectableElements.Count, "Ctrl+A 可枚举方框、马赛克和图片等全部编辑元素");
    AssertTrue(ReferenceEquals(mosaic, visualAreaDocument.FindTopMovableAt(new Point(70, 60), 4)), "马赛克支持选中");
    AssertTrue(!mosaic.CanMove(altPressed: false) && mosaic.CanMove(altPressed: true), "马赛克统一使用 Alt 加鼠标左键移动");
    var selectAllElements = new AnnotationSelection();
    selectAllElements.SelectAll(selectableElements);
    AssertTrue(selectAllElements.IsExactSelection(selectableElements), "Ctrl+A 后全部编辑元素处于选中状态");
    selectAllElements.Remove(mosaic);
    AssertTrue(!selectAllElements.IsExactSelection(selectableElements), "缺少任一编辑元素时不视为已经全选");
    FollowSelectionStrategy.Instance.Apply(visualAreaDocument, new Point(12, 9));
    AssertEqual(
        new Rectangle(mosaicBounds.X + 12, mosaicBounds.Y + 9, mosaicBounds.Width, mosaicBounds.Height),
        mosaic.VisualBounds,
        "马赛克视觉区域随截图框同步移动");
}

using (var source = CreateSolidBitmap(new Size(600, 500), Color.Black))
using (var preview = CreateSolidBitmap(new Size(600, 500), Color.Black))
using (var liveMoveDocument = new AnnotationDocument())
{
    var previousSelection = new Rectangle(100, 100, 400, 300);
    var currentSelection = new Rectangle(130, 120, 400, 300);
    var offset = new Point(30, 20);
    var drawing = new ShapeAnnotation(
        EditorTool.Rectangle,
        new Rectangle(150, 150, 20, 20),
        Color.Red,
        2F);
    liveMoveDocument.Add(drawing);

    using (var previewGraphics = Graphics.FromImage(preview))
    {
        previewGraphics.SetClip(previousSelection);
        liveMoveDocument.Render(previewGraphics, source);
    }
    AssertTrue(preview.GetPixel(150, 150).ToArgb() != Color.Black.ToArgb(), "移动前预览包含原位置绘制元素");

    var previousVisualAreas = liveMoveDocument.GetVisualAreas(AnnotationCategory.Drawing);
    FollowSelectionStrategy.Instance.Apply(liveMoveDocument, offset);
    var dirtyAreas = SelectionMoveInvalidation.GetMovedVisualAreas(
        previousSelection,
        currentSelection,
        previousVisualAreas,
        offset);
    AssertTrue(dirtyAreas.Any(area => area.Contains(new Point(150, 150))), "右键移动立即重绘元素旧位置");
    AssertTrue(dirtyAreas.Any(area => area.Contains(new Point(180, 170))), "右键移动立即重绘元素新位置");
    var overlap = Rectangle.Intersect(previousSelection, currentSelection);
    AssertTrue(overlap.Contains(new Point(150, 150)) && overlap.Contains(new Point(180, 170)), "元素新旧位置位于截图框重叠区时仍会重绘");

    using (var previewGraphics = Graphics.FromImage(preview))
    using (var background = new SolidBrush(Color.Black))
    {
        foreach (var dirtyArea in dirtyAreas)
        {
            var state = previewGraphics.Save();
            previewGraphics.SetClip(dirtyArea);
            previewGraphics.FillRectangle(background, dirtyArea);
            previewGraphics.SetClip(currentSelection, System.Drawing.Drawing2D.CombineMode.Intersect);
            liveMoveDocument.Render(previewGraphics, source);
            previewGraphics.Restore(state);
        }
    }
    AssertEqual(Color.Black.ToArgb(), preview.GetPixel(150, 150).ToArgb(), "右键移动后清除绘制元素原位置残影");
    AssertTrue(preview.GetPixel(180, 170).ToArgb() != Color.Black.ToArgb(), "右键拖动过程中立即显示绘制元素新位置");
}

using (var source = new Bitmap(120, 80))
using (var followExportDocument = new AnnotationDocument())
{
    followExportDocument.Add(new StickerAnnotation(
        CreateSolidBitmap(new Size(20, 20), Color.Red),
        new Rectangle(20, 20, 20, 20)));
    FollowSelectionStrategy.Instance.Apply(followExportDocument, new Point(30, 0));
    using var exported = RenderDocumentSelection(
        followExportDocument,
        source,
        new Rectangle(30, 0, 60, 60));
    AssertEqual(Color.Red.ToArgb(), exported.GetPixel(25, 25).ToArgb(), "跟随模式最终导出保持贴纸相对位置");
}

using (var source = new Bitmap(120, 80))
using (var keepExportDocument = new AnnotationDocument())
{
    keepExportDocument.Add(new ShapeAnnotation(
        EditorTool.Rectangle,
        new Rectangle(10, 10, 12, 12),
        Color.Lime,
        2F));
    keepExportDocument.Add(new StickerAnnotation(
        CreateSolidBitmap(new Size(20, 20), Color.Red),
        new Rectangle(20, 20, 20, 20)));
    KeepStickersAtScreenPositionStrategy.Instance.Apply(keepExportDocument, new Point(30, 0));
    using (var movedExport = RenderDocumentSelection(
               keepExportDocument,
               source,
               new Rectangle(30, 0, 60, 60)))
    {
        AssertEqual(Color.Red.ToArgb(), movedExport.GetPixel(5, 25).ToArgb(), "固定模式最终导出只保留框内贴纸部分");
        AssertEqual(Color.Black.ToArgb(), movedExport.GetPixel(15, 25).ToArgb(), "固定模式最终导出裁剪框外贴纸部分");
        AssertTrue(
            ContainsPixelDifferentFrom(movedExport, new Rectangle(8, 8, 18, 8), Color.Black),
            "固定贴纸模式最终导出仍包含随截图框移动的绘制元素");
    }

    using var restoredExport = RenderDocumentSelection(
        keepExportDocument,
        source,
        new Rectangle(0, 0, 60, 60));
    AssertEqual(Color.Red.ToArgb(), restoredExport.GetPixel(25, 25).ToArgb(), "移回截图框后越界贴纸重新出现");
}

using (var source = new Bitmap(60, 60))
using (var stickerImage = new Bitmap(10, 10))
{
    using (var stickerGraphics = Graphics.FromImage(stickerImage))
    {
        stickerGraphics.Clear(Color.Red);
    }
    using var document = new AnnotationDocument();
    document.Add(new StickerAnnotation(stickerImage, new Rectangle(20, 20, 10, 10)));
    using var targetGraphics = Graphics.FromImage(source);
    document.Render(targetGraphics, source);
    AssertEqual(Color.Red.ToArgb(), source.GetPixel(25, 25).ToArgb(), "贴纸写入最终截图");
}

using (var source = CreateSolidBitmap(new Size(120, 70), Color.CornflowerBlue))
using (var transparentTextDocument = new AnnotationDocument())
{
    transparentTextDocument.Add(new TextAnnotation(
        new Rectangle(10, 10, 100, 44),
        "透明",
        Color.White,
        20F));
    using var targetGraphics = Graphics.FromImage(source);
    transparentTextDocument.Render(targetGraphics, source);
    AssertEqual(Color.CornflowerBlue.ToArgb(), source.GetPixel(105, 50).ToArgb(), "最终导出文字框不会填充背景色");
    AssertTrue(ContainsPixelDifferentFrom(source, new Rectangle(10, 10, 70, 35), Color.CornflowerBlue), "最终导出包含文字笔画");
}

using (var ocrModule = new OcrModule())
{
    AssertEqual(new Version(1, 11, 0), OcrModule.MinimumHostVersion, "OCR 模块最低主程序版本");
    AssertEqual("screenshot-tool.ocr", ocrModule.Id, "OCR 模块 ID 保持稳定");
    AssertEqual("本地 OCR 文字识别", ocrModule.DisplayName, "OCR 模块显示名称");
    AssertEqual(new Version(1, 1, 0), ocrModule.Version, "OCR 模块版本");

    var incompatibleOcrModuleRejected = false;
    try
    {
        ocrModule.Initialize(new TestModuleContext(new Version(1, 10, 0)));
    }
    catch (NotSupportedException)
    {
        incompatibleOcrModuleRejected = true;
    }
    AssertTrue(incompatibleOcrModuleRejected, "OCR 模块拒绝缺少文本结果契约的旧版主程序");
    ocrModule.Initialize(new TestModuleContext(OcrModule.MinimumHostVersion));

    var ocrFeatures = ocrModule.CreateCaptureFeatures().ToArray();
    AssertEqual(1, ocrFeatures.Length, "OCR 模块按截图会话创建功能实例");
    using var ocrFeature = ocrFeatures[0];
    AssertEqual("screenshot-tool.ocr.feature", ocrFeature.Id, "OCR 功能 ID 保持稳定");
    AssertTrue(ocrFeature is ICaptureToolbarCommandProvider, "OCR 功能提供截图工具栏入口");
    var ocrCommands = ((ICaptureToolbarCommandProvider)ocrFeature).GetToolbarCommands();
    AssertEqual(1, ocrCommands.Count, "OCR 功能只注册一个工具栏命令");
    AssertEqual("OCR 本地", ocrCommands[0].Text, "OCR 工具栏命令文字");
}

using (var darkSmallText = new Bitmap(420, 90))
{
    using (var graphics = Graphics.FromImage(darkSmallText))
    using (var font = new Font("Microsoft YaHei UI", 18F, FontStyle.Regular))
    {
        graphics.Clear(Color.FromArgb(28, 31, 38));
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.DrawString("轻截 OCR small 2026", font, Brushes.White, 8F, 24F);
    }

    var candidates = OcrImagePreprocessor.CreateCandidates(darkSmallText);
    try
    {
        AssertEqual(4, candidates.Count, "本地 OCR 生成原图、放大、对比度和黑白四路候选");
        AssertTrue(candidates[1].Image.Width > darkSmallText.Width, "本地 OCR 放大小字号选区");
        AssertEqual(
            Color.White.ToArgb(),
            candidates[2].Image.GetPixel(0, 0).ToArgb(),
            "本地 OCR 增强候选增加白色边距");
    }
    finally
    {
        foreach (var candidate in candidates)
        {
            candidate.Dispose();
        }
    }
}

using (var wideOcrSelection = new Bitmap(3000, 120))
{
    var candidates = OcrImagePreprocessor.CreateCandidates(wideOcrSelection);
    try
    {
        AssertEqual(2400, candidates[1].Image.Width, "本地 OCR 预处理限制超宽选区的中间图尺寸");
    }
    finally
    {
        foreach (var candidate in candidates)
        {
            candidate.Dispose();
        }
    }
}

AssertEqual(
    "轻截文字识别 2026",
    OcrCandidateSelector.SelectBest(
    [
        new OcrWorkerResult("original", "轻截文?", 1, 1),
        new OcrWorkerResult("contrast", "轻截文字识别 2026", 1, 2)
    ]),
    "本地 OCR 从多路结果中选择信息更完整的候选");

AssertEqual(
    "轻截文字识别 2026\nSecond line",
    OcrTextNormalizer.Normalize(" 轻 截 文 字 识 别 2026\r\nSecond line "),
    "OCR 清理中日韩文字之间的伪空格并保留英文空格和换行");

using (var ocrFeature = new OcrFeature(new TestOcrRecognizer("第一行\nSecond line")))
{
    var ocrHost = new TestOcrCaptureHost();
    ocrFeature.Attach(ocrHost);
    ((ICaptureToolbarCommandProvider)ocrFeature)
        .ExecuteToolbarCommandAsync(OcrFeature.CommandId, CancellationToken.None)
        .GetAwaiter()
        .GetResult();
    AssertEqual("本地 OCR 识别结果", ocrHost.ResultTitle ?? string.Empty, "OCR 使用通用宿主打开结果窗口");
    AssertEqual("第一行\nSecond line", ocrHost.ResultText ?? string.Empty, "OCR 结果保留识别换行");
    AssertTrue(ocrHost.Completed, "OCR 成功后结束冻结的截图会话");
    AssertTrue(ocrHost.SelectionCopied, "OCR 识别宿主提供的当前选区位图");
}

using var blockingOcrRecognizer = new TestBlockingOcrRecognizer();
var cancellableOcrFeature = new OcrFeature(blockingOcrRecognizer);
cancellableOcrFeature.Attach(new TestOcrCaptureHost());
var cancellableOcrTask = Task.Run(() =>
    ((ICaptureToolbarCommandProvider)cancellableOcrFeature)
        .ExecuteToolbarCommandAsync(OcrFeature.CommandId, CancellationToken.None));
AssertTrue(blockingOcrRecognizer.Started.Wait(TimeSpan.FromSeconds(2)), "OCR 异步识别已经启动");
cancellableOcrFeature.Dispose();
var ocrDisposeCancelledRecognition = false;
try
{
    cancellableOcrTask.GetAwaiter().GetResult();
}
catch (OperationCanceledException)
{
    ocrDisposeCancelledRecognition = true;
}
AssertTrue(ocrDisposeCancelledRecognition, "OCR 功能释放时取消活动识别任务");

VerifyPaddleOcrModule(
    new PaddleOcrTinyModule(),
    PaddleOcrVariant.Tiny,
    "screenshot-tool.paddle-ocr.tiny",
    "PP-OCR Tiny 文字识别",
    "screenshot-tool.paddle-ocr.tiny.feature",
    "screenshot-tool.paddle-ocr.tiny.recognize",
    "OCR Tiny");
VerifyPaddleOcrModule(
    new PaddleOcrSmallModule(),
    PaddleOcrVariant.Small,
    "screenshot-tool.paddle-ocr.small",
    "PP-OCR Small 文字识别",
    "screenshot-tool.paddle-ocr.small.feature",
    "screenshot-tool.paddle-ocr.small.recognize",
    "OCR Small");

using (var paddleFeature = new PaddleOcrFeature(
           "tests.paddle-ocr.feature",
           551,
           "tests.paddle-ocr.recognize",
           "OCR 测试",
           "测试 PP-OCR",
           "PP-OCR 测试结果",
           new TestPaddleOcrRecognizer("第一行\nMixed 2026")))
{
    var paddleHost = new TestOcrCaptureHost();
    paddleFeature.Attach(paddleHost);
    ((ICaptureToolbarCommandProvider)paddleFeature)
        .ExecuteToolbarCommandAsync("tests.paddle-ocr.recognize", CancellationToken.None)
        .GetAwaiter()
        .GetResult();
    AssertEqual("PP-OCR 测试结果", paddleHost.ResultTitle ?? string.Empty, "PP-OCR 使用通用结果窗口");
    AssertEqual("第一行\nMixed 2026", paddleHost.ResultText ?? string.Empty, "PP-OCR 保留中英混排结果");
    AssertTrue(paddleHost.Completed, "PP-OCR 成功后结束截图会话");
}

var missingTinyModels = PaddleOcrModelFiles.Resolve(
    Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
    PaddleOcrVariant.Tiny).GetMissingFiles();
AssertEqual(4, missingTinyModels.Count, "PP-OCR Tiny 检查检测、方向、识别和字典四个文件");

using (var qrCodeModule = new QrCodeModule())
{
    AssertEqual(new Version(1, 11, 0), QrCodeModule.MinimumHostVersion, "二维码模块最低主程序版本");
    AssertEqual("screenshot-tool.qr-code", qrCodeModule.Id, "二维码模块 ID 保持稳定");
    AssertEqual("二维码扫描", qrCodeModule.DisplayName, "二维码模块显示名称");
    AssertEqual(new Version(1, 0, 0), qrCodeModule.Version, "二维码模块版本");

    var incompatibleQrCodeModuleRejected = false;
    try
    {
        qrCodeModule.Initialize(new TestModuleContext(new Version(1, 10, 0)));
    }
    catch (NotSupportedException)
    {
        incompatibleQrCodeModuleRejected = true;
    }
    AssertTrue(incompatibleQrCodeModuleRejected, "二维码模块拒绝缺少文本结果契约的旧版主程序");
    qrCodeModule.Initialize(new TestModuleContext(QrCodeModule.MinimumHostVersion));

    var qrCodeFeatures = qrCodeModule.CreateCaptureFeatures().ToArray();
    AssertEqual(1, qrCodeFeatures.Length, "二维码模块按截图会话创建功能实例");
    using var qrCodeFeature = qrCodeFeatures[0];
    AssertEqual("screenshot-tool.qr-code.feature", qrCodeFeature.Id, "二维码功能 ID 保持稳定");
    AssertTrue(qrCodeFeature is ICaptureToolbarCommandProvider, "二维码功能提供截图工具栏入口");
    var qrCodeCommands = ((ICaptureToolbarCommandProvider)qrCodeFeature).GetToolbarCommands();
    AssertEqual(1, qrCodeCommands.Count, "二维码功能只注册一个工具栏命令");
    AssertEqual("二维码", qrCodeCommands[0].Text, "二维码工具栏命令文字");
}

const string qrCodePayload = "https://example.com/cutcut?source=截图框";
using (var qrCodeBitmap = CreateQrCodeBitmap(qrCodePayload, 420))
{
    var decodedQrCodes = new ZxingQrCodeScanner()
        .ScanAsync(qrCodeBitmap, CancellationToken.None)
        .GetAwaiter()
        .GetResult();
    AssertEqual(1, decodedQrCodes.Count, "二维码扫描器只返回选区中的有效二维码");
    AssertEqual(qrCodePayload, decodedQrCodes[0], "二维码扫描器保留原始内容");
}
using (var blankQrCodeBitmap = new Bitmap(320, 180))
{
    using (var graphics = Graphics.FromImage(blankQrCodeBitmap))
    {
        graphics.Clear(Color.White);
    }
    var blankQrCodeResults = new ZxingQrCodeScanner()
        .ScanAsync(blankQrCodeBitmap, CancellationToken.None)
        .GetAwaiter()
        .GetResult();
    AssertEqual(0, blankQrCodeResults.Count, "没有二维码的选区返回空结果");
}

using (var qrCodeFeature = new QrCodeFeature(
           new TestQrCodeScanner(["https://example.com/one", "第二个二维码"])))
{
    var qrCodeHost = new TestOcrCaptureHost();
    qrCodeFeature.Attach(qrCodeHost);
    ((ICaptureToolbarCommandProvider)qrCodeFeature)
        .ExecuteToolbarCommandAsync(QrCodeFeature.CommandId, CancellationToken.None)
        .GetAwaiter()
        .GetResult();
    AssertEqual("二维码扫描结果", qrCodeHost.ResultTitle ?? string.Empty, "二维码使用通用宿主打开结果窗口");
    AssertEqual(
        $"https://example.com/one{Environment.NewLine}{Environment.NewLine}第二个二维码",
        qrCodeHost.ResultText ?? string.Empty,
        "多个二维码结果按块分隔并保留原始内容");
    AssertTrue(qrCodeHost.Completed, "二维码扫描成功后结束冻结的截图会话");
    AssertTrue(qrCodeHost.SelectionCopied, "二维码扫描宿主提供的当前选区位图");
}

using var blockingQrCodeScanner = new TestBlockingQrCodeScanner();
var cancellableQrCodeFeature = new QrCodeFeature(blockingQrCodeScanner);
cancellableQrCodeFeature.Attach(new TestOcrCaptureHost());
var cancellableQrCodeTask = Task.Run(() =>
    ((ICaptureToolbarCommandProvider)cancellableQrCodeFeature)
        .ExecuteToolbarCommandAsync(QrCodeFeature.CommandId, CancellationToken.None));
AssertTrue(blockingQrCodeScanner.Started.Wait(TimeSpan.FromSeconds(2)), "二维码异步扫描已经启动");
cancellableQrCodeFeature.Dispose();
var qrCodeDisposeCancelledScan = false;
try
{
    cancellableQrCodeTask.GetAwaiter().GetResult();
}
catch (OperationCanceledException)
{
    qrCodeDisposeCancelledScan = true;
}
AssertTrue(qrCodeDisposeCancelledScan, "二维码功能释放时取消活动扫描任务");

AssertEqual(
    new Point(612, 100),
    CaptureTextResultForm.CalculateLocation(
        new Rectangle(100, 100, 500, 300),
        new Rectangle(0, 0, 1920, 1080),
        new Size(440, 320)),
    "OCR 结果窗口优先放在选区右侧");
AssertEqual(
    new Point(1248, 100),
    CaptureTextResultForm.CalculateLocation(
        new Rectangle(1700, 100, 180, 300),
        new Rectangle(0, 0, 1920, 1080),
        new Size(440, 320)),
    "右侧空间不足时 OCR 结果窗口放在选区左侧");
AssertEqual(
    new Point(160, 280),
    CaptureTextResultForm.CalculateLocation(
        new Rectangle(300, 500, 300, 120),
        new Rectangle(0, 0, 800, 600),
        new Size(440, 320)),
    "两侧空间都不足时 OCR 结果窗口限制在工作区内");

var ocrModuleTestDirectory = Path.Combine(
    Path.GetTempPath(),
    "ScreenshotTool.OcrModuleTests",
    Guid.NewGuid().ToString("N"));
try
{
    var ocrModulePackageDirectory = Path.Combine(ocrModuleTestDirectory, "Ocr");
    Directory.CreateDirectory(ocrModulePackageDirectory);
    File.Copy(
        typeof(OcrModule).Assembly.Location,
        Path.Combine(ocrModulePackageDirectory, "ScreenshotTool.Ocr.dll"));
    using var ocrModuleHost = new ModuleHost(ocrModuleTestDirectory);
    var loadedOcrModules = ocrModuleHost.Refresh();
    AssertEqual(0, loadedOcrModules.Errors.Count, "OCR 模块以独立单 DLL 包加载");
    AssertEqual(1, loadedOcrModules.Modules.Count, "发现并加载 OCR 模块");
    AssertEqual("screenshot-tool.ocr", loadedOcrModules.Modules[0].Id, "读取 OCR 模块稳定 ID");
    var loadedOcrFeatures = ocrModuleHost.CreateCaptureFeatures();
    AssertEqual(1, loadedOcrFeatures.Count, "OCR 模块为截图会话创建功能实例");
    using var loadedOcrFeature = loadedOcrFeatures[0];
    AssertTrue(loadedOcrFeature is ICaptureToolbarCommandProvider, "加载后的 OCR 功能转发工具栏命令");
    Directory.Delete(ocrModulePackageDirectory, recursive: true);
    var removedOcrModules = ocrModuleHost.Refresh();
    AssertEqual(0, removedOcrModules.Modules.Count, "删除 OCR 模块文件夹后立即从目录卸载");
    AssertEqual(
        "OCR 本地",
        ((ICaptureToolbarCommandProvider)loadedOcrFeature).GetToolbarCommands()[0].Text,
        "活动截图会话在 OCR 模块删除后保持延迟释放租约");
}
finally
{
    if (Directory.Exists(ocrModuleTestDirectory))
    {
        Directory.Delete(ocrModuleTestDirectory, recursive: true);
    }
}

VerifyPaddleOcrModulePackage(
    typeof(PaddleOcrTinyModule).Assembly.Location,
    "PaddleOcrTiny",
    "screenshot-tool.paddle-ocr.tiny");
VerifyPaddleOcrModulePackage(
    typeof(PaddleOcrSmallModule).Assembly.Location,
    "PaddleOcrSmall",
    "screenshot-tool.paddle-ocr.small");

var qrCodeModuleTestDirectory = Path.Combine(
    Path.GetTempPath(),
    "ScreenshotTool.QrCodeModuleTests",
    Guid.NewGuid().ToString("N"));
try
{
    var qrCodeModulePackageDirectory = Path.Combine(qrCodeModuleTestDirectory, "QrCode");
    Directory.CreateDirectory(qrCodeModulePackageDirectory);
    File.Copy(
        typeof(QrCodeModule).Assembly.Location,
        Path.Combine(qrCodeModulePackageDirectory, "ScreenshotTool.QrCode.dll"));
    File.Copy(
        typeof(BarcodeReaderGeneric).Assembly.Location,
        Path.Combine(qrCodeModulePackageDirectory, "zxing.dll"));
    using var qrCodeModuleHost = new ModuleHost(qrCodeModuleTestDirectory);
    var loadedQrCodeModules = qrCodeModuleHost.Refresh();
    AssertEqual(0, loadedQrCodeModules.Errors.Count, "二维码模块连同私有解码依赖加载");
    AssertEqual(1, loadedQrCodeModules.Modules.Count, "发现并加载二维码模块");
    AssertEqual("screenshot-tool.qr-code", loadedQrCodeModules.Modules[0].Id, "读取二维码模块稳定 ID");
    var loadedQrCodeFeatures = qrCodeModuleHost.CreateCaptureFeatures();
    AssertEqual(1, loadedQrCodeFeatures.Count, "二维码模块为截图会话创建功能实例");
    using var loadedQrCodeFeature = loadedQrCodeFeatures[0];
    AssertTrue(loadedQrCodeFeature is ICaptureToolbarCommandProvider, "加载后的二维码功能转发工具栏命令");
    using var loadedQrCodeBitmap = CreateQrCodeBitmap("module-load-context", 360);
    var loadedQrCodeHost = new TestOcrCaptureHost(loadedQrCodeBitmap);
    loadedQrCodeFeature.Attach(loadedQrCodeHost);
    Task.Run(() =>
        ((ICaptureToolbarCommandProvider)loadedQrCodeFeature)
            .ExecuteToolbarCommandAsync(QrCodeFeature.CommandId, CancellationToken.None))
        .GetAwaiter()
        .GetResult();
    AssertEqual(
        "module-load-context",
        loadedQrCodeHost.ResultText ?? string.Empty,
        "热加载二维码模块从自己的目录解析并调用私有解码依赖");
    Directory.Delete(qrCodeModulePackageDirectory, recursive: true);
    var removedQrCodeModules = qrCodeModuleHost.Refresh();
    AssertEqual(0, removedQrCodeModules.Modules.Count, "删除二维码模块文件夹后立即从目录卸载");
    AssertEqual(
        "二维码",
        ((ICaptureToolbarCommandProvider)loadedQrCodeFeature).GetToolbarCommands()[0].Text,
        "活动截图会话在二维码模块删除后保持延迟释放租约");
}
finally
{
    if (Directory.Exists(qrCodeModuleTestDirectory))
    {
        Directory.Delete(qrCodeModuleTestDirectory, recursive: true);
    }
}

var moduleTestDirectory = Path.Combine(Path.GetTempPath(), "ScreenshotTool.ModuleTests", Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(moduleTestDirectory);
    var legacyRootModulePath = Path.Combine(
        moduleTestDirectory,
        "ScreenshotTool.TestModule.dll");
    File.Copy(typeof(TestHotLoadModule).Assembly.Location, legacyRootModulePath);
    using var moduleHost = new ModuleHost(moduleTestDirectory);
    var ignoredRootModule = moduleHost.Refresh();
    AssertEqual(0, ignoredRootModule.Modules.Count, "Modules 根目录不再加载散落 DLL");
    File.Delete(legacyRootModulePath);

    var modulePackageDirectory = Path.Combine(moduleTestDirectory, "TestHotLoad");
    Directory.CreateDirectory(modulePackageDirectory);
    var modulePath = Path.Combine(modulePackageDirectory, "ScreenshotTool.TestModule.dll");
    File.Copy(typeof(TestHotLoadModule).Assembly.Location, modulePath);
    var loadedModules = moduleHost.Refresh();
    AssertEqual(1, loadedModules.Modules.Count, "热加载模块程序集");
    AssertEqual("tests.hot-load", loadedModules.Modules[0].Id, "读取模块元数据");
    AssertEqual(
        modulePackageDirectory,
        Path.GetDirectoryName(loadedModules.Modules[0].AssemblyPath)!,
        "模块入口程序集位于独立一级子文件夹");
    var installedPackage = moduleHost.GetInstalledPackages().Single();
    AssertEqual(ModulePackageState.Enabled, installedPackage.State, "已安装模块默认启用");
    AssertEqual("TestHotLoad", installedPackage.PackageName, "模块管理使用一级文件夹作为包名");

    var hotLoadSettingsHost = new TestModuleSettingsHost();
    hotLoadSettingsHost.SetBoolean("tests.hot-load.flag", true);
    var settingsPages = moduleHost.CreateSettingsPages(hotLoadSettingsHost);
    AssertEqual(1, settingsPages.Count, "热加载模块创建自带设置页");
    using var settingsPage = settingsPages[0];
    AssertEqual("tests.hot-load.settings", settingsPage.Id, "模块设置页 ID 保持稳定");
    AssertTrue(
        settingsPage.Content.Controls.OfType<Label>().Single().Text.Contains(
            "已开启",
            StringComparison.Ordinal),
        "模块设置页通过通用宿主读取模块偏好");

    var features = moduleHost.CreateCaptureFeatures();
    AssertEqual(1, features.Count, "按截图会话组合模块功能");
    using var feature = features[0];
    var featureHost = new TestCaptureFeatureHost();
    feature.Attach(featureHost);
    AssertTrue(feature.HandleKeyDown(new KeyEventArgs(Keys.Control | Keys.Alt | Keys.M)), "模块处理快捷键");
    using (var rendered = new Bitmap(40, 40))
    using (var renderedGraphics = Graphics.FromImage(rendered))
    {
        feature.Render(renderedGraphics, CaptureRenderTarget.Export);
        AssertEqual(Color.Red.ToArgb(), rendered.GetPixel(2, 2).ToArgb(), "模块参与最终截图渲染");
    }

    var disabledModule = moduleHost.SetPackageEnabled("TestHotLoad", enabled: false);
    AssertTrue(disabledModule.Succeeded, "设置工作台可以禁用模块");
    AssertEqual(0, moduleHost.GetModules().Count, "禁用模块后不再用于新截图会话");
    AssertEqual(
        ModulePackageState.Disabled,
        moduleHost.GetInstalledPackages().Single().State,
        "禁用状态持久化在模块包中");
    using (var restartedHost = new ModuleHost(moduleTestDirectory))
    {
        restartedHost.Refresh();
        AssertEqual(0, restartedHost.GetModules().Count, "重启后保持模块禁用状态");
        AssertEqual(
            ModulePackageState.Disabled,
            restartedHost.GetInstalledPackages().Single().State,
            "重启后仍可在设置工作台查看禁用模块");
    }
    AssertTrue(feature.HandleKeyDown(new KeyEventArgs(Keys.Control | Keys.Alt | Keys.M)), "禁用不打断当前截图会话");

    var enabledModule = moduleHost.SetPackageEnabled("TestHotLoad", enabled: true);
    AssertTrue(enabledModule.Succeeded, "设置工作台可以重新启用模块");
    AssertEqual(1, moduleHost.GetModules().Count, "重新启用后模块恢复到新截图会话");
    AssertTrue(
        !moduleHost.DeletePackage("..").Succeeded,
        "模块删除拒绝越过 Modules 根目录");

    var deletedModule = moduleHost.DeletePackage("TestHotLoad");
    AssertTrue(deletedModule.Succeeded, "设置工作台可以永久删除模块");
    AssertTrue(!Directory.Exists(modulePackageDirectory), "永久删除会移除整个模块文件夹");
    AssertEqual(0, moduleHost.GetInstalledPackages().Count, "永久删除后模块不再出现在安装列表");
    AssertEqual(0, moduleHost.GetModules().Count, "永久删除后热拆卸模块");
    AssertEqual(
        0,
        moduleHost.CreateSettingsPages(hotLoadSettingsHost).Count,
        "删除模块后不再创建对应设置页");
    AssertTrue(feature.HandleKeyDown(new KeyEventArgs(Keys.Control | Keys.Alt | Keys.M)), "当前截图会话延迟释放旧模块");
    AssertEqual("测试模块", settingsPage.Title, "活动设置页租约延迟释放旧模块");
}
finally
{
    if (Directory.Exists(moduleTestDirectory))
    {
        Directory.Delete(moduleTestDirectory, recursive: true);
    }
}

var recordingTargetCreated = RecordingTarget.TryCreateForDisplay(
    new Rectangle(101, 51, 639, 479),
    new Rectangle(0, 0, 1920, 1080),
    @"\\.\DISPLAY1",
    out var recordingTarget);
AssertTrue(recordingTargetCreated && recordingTarget is not null, "录屏选区解析单显示器目标");
AssertEqual(new Rectangle(101, 51, 638, 478), recordingTarget!.ScreenBounds, "H.264 录屏选区归一化为偶数尺寸");
AssertEqual(new Rectangle(101, 51, 638, 478), recordingTarget.DisplayRelativeBounds, "录屏选区转换为显示器相对坐标");
AssertTrue(
    !RecordingTarget.TryCreateForDisplay(
        new Rectangle(1800, 50, 200, 200),
        new Rectangle(0, 0, 1920, 1080),
        @"\\.\DISPLAY1",
        out _),
    "录屏选区不能跨越显示器边界");
AssertEqual(
    5,
    ScreenRecordingPreferences.SupportedVideoBitrates.Count,
    "录屏提供五档视频码率");
AssertEqual(
    8_000_000,
    ScreenRecordingPreferences.NormalizeVideoBitrate(8_100_000),
    "录屏码率选择定位最近档位");
var configuredRecordingOptions = RecordingOptions.FromHost(new TestCaptureFeatureHost(
    captureSystemAudio: false,
    captureMicrophone: true,
    showMouseClickIndicator: false,
    framesPerSecond: 59,
    videoBitrate: 12_200_000,
    regionIndicatorStyle: CaptureRegionIndicatorStyle.Solid));
AssertTrue(!configuredRecordingOptions.CaptureSystemAudio, "录屏模块读取已保存的系统声音设置");
AssertTrue(configuredRecordingOptions.CaptureMicrophone, "录屏模块读取已保存的麦克风设置");
AssertTrue(
    !configuredRecordingOptions.ShowMouseClickIndicator,
    "录屏模块读取已保存的左键圆圈开关");
AssertEqual(60, configuredRecordingOptions.FramesPerSecond, "录屏模块归一化已保存的帧率");
AssertEqual(12_000_000, configuredRecordingOptions.VideoBitrate, "录屏模块归一化已保存的码率");
AssertEqual(
    CaptureRegionIndicatorStyle.Solid,
    configuredRecordingOptions.RegionIndicatorStyle,
    "录屏模块读取已保存的范围提示样式");
AssertEqual(
    60_960_000L,
    ScreenRecordingStorageEstimator.EstimateBytes(
        8_000_000,
        includesAudio: true,
        TimeSpan.FromMinutes(1)),
    "一分钟储存占用包含单路混合音频码率");
AssertEqual(
    609_600_000L,
    ScreenRecordingStorageEstimator.EstimateBytes(
        8_000_000,
        includesAudio: true,
        TimeSpan.FromMinutes(10)),
    "十分钟储存占用估算");
AssertEqual(
    3_657_600_000L,
    ScreenRecordingStorageEstimator.EstimateBytes(
        8_000_000,
        includesAudio: true,
        TimeSpan.FromHours(1)),
    "一小时储存占用估算");
AssertEqual(
    "3.66 GB",
    ScreenRecordingStorageEstimator.FormatBytes(3_657_600_000L),
    "大容量估算使用 GB 显示");
var sharedRecordingWidth = new ToolWidthController(ToolWidthRange.Create(1, 32), 4);
var sharedRecordingColor = Color.Empty;
using (var sharedRecordingAnnotations = new LiveAnnotationSessionForm(
           new Rectangle(0, 0, 180, 120),
           new Bitmap(180, 120),
           new TestTextClipboardService(),
           sharedRecordingWidth,
           new DrawingToolCoefficients(),
           AnnotationRotationStep.DefaultDegrees,
           DrawingCursorShape.Circle,
           Color.Red,
           color => sharedRecordingColor = color,
           showMouseClickIndicator: false))
{
    AssertTrue(
        !sharedRecordingAnnotations.MouseClickIndicatorVisible,
        "关闭左键圆圈时录制现场不显示提示层");
    AssertTrue(
        sharedRecordingAnnotations.Tools.Count == CaptureEditorToolCatalog.Tools.Count &&
        sharedRecordingAnnotations.Tools.All(tool =>
            tool.Tool is not CaptureAnnotationTool.Operation and not CaptureAnnotationTool.Select),
        "录屏只显示截图菜单栏原有的绘图工具");
    AssertTrue(
        sharedRecordingAnnotations is ICaptureAnnotationToolbarSession,
        "实时批注会话由宿主提供共享截图工具栏");
    var sharedRecordingToolbarSession =
        (ICaptureAnnotationToolbarSession)sharedRecordingAnnotations;
    var recordingSelectButton = sharedRecordingAnnotations.Toolbar.Controls.OfType<Button>()
        .Single(button => button.Text == "选择");
    sharedRecordingToolbarSession.SetToolVisible(CaptureAnnotationTool.Select, visible: true);
    var sharedRectangleButton = sharedRecordingAnnotations.Toolbar.Controls.OfType<Button>()
        .Single(button => button.Text == "矩形");
    AssertTrue(
        sharedRecordingAnnotations.Toolbar.Controls.GetChildIndex(recordingSelectButton) <
        sharedRecordingAnnotations.Toolbar.Controls.GetChildIndex(sharedRectangleButton),
        "录屏专属选择按钮显示在矩形工具左侧");
    AssertTrue(
        recordingSelectButton.BackColor.ToArgb() != sharedRectangleButton.BackColor.ToArgb(),
        "录屏专属选择按钮使用区别于绘图工具的提示色");
    var recordingSelectInactiveColor = recordingSelectButton.BackColor;
    RaiseButtonClick(recordingSelectButton);
    AssertEqual(
        CaptureAnnotationTool.Select,
        sharedRecordingAnnotations.ActiveTool,
        "录屏选择按钮进入元素编辑状态");
    AssertEqual("✓ 选择中", recordingSelectButton.Text, "录屏选择按钮明确显示已开启状态");
    AssertEqual(2, recordingSelectButton.FlatAppearance.BorderSize, "录屏选择已开启时显示强调边框");
    AssertTrue(
        recordingSelectButton.BackColor.ToArgb() != recordingSelectInactiveColor.ToArgb(),
        "录屏选择开启后切换为明显不同的高亮色");
    RaiseButtonClick(recordingSelectButton);
    AssertEqual(
        CaptureAnnotationTool.Operation,
        sharedRecordingAnnotations.ActiveTool,
        "再次点击录屏选择按钮恢复鼠标穿透状态");
    AssertEqual("选择", recordingSelectButton.Text, "录屏选择关闭后恢复普通文字");
    AssertEqual(1, recordingSelectButton.FlatAppearance.BorderSize, "录屏选择关闭后恢复普通边框");
    sharedRecordingToolbarSession.ConfigureToolbar(
        "● 00:00:05",
        [
            new("pause", "暂停", "暂停录屏", 52),
            new(
                "stop",
                "停止并保存",
                "停止录屏并保存 MP4",
                82,
                CaptureAnnotationToolbarCommandStyle.Danger)
        ]);
    AssertTrue(
        ReferenceEquals(sharedRecordingAnnotations, sharedRecordingAnnotations.ToolbarWindow.Owner),
        "录屏工具栏始终位于实时批注输入层上方");
    AssertTrue(
        sharedRecordingAnnotations.Toolbar.Controls.OfType<Button>().Any(button =>
            button.Text == "停止并保存"),
        "录屏命令注入截图核心工具栏");
    var sharedSnapButton = sharedRecordingAnnotations.Toolbar.Controls.OfType<Button>()
        .Single(button => button.Name == "SnappingButton");
    AssertEqual("吸附 开", sharedSnapButton.Text, "录屏复用截图核心工具栏的元素吸附开关");
    RaiseButtonClick(sharedSnapButton);
    AssertEqual("吸附 关", sharedSnapButton.Text, "录屏工具栏可以独立切换当前会话吸附状态");
    var recordingToolbarCommand = string.Empty;
    ((ICaptureAnnotationToolbarSession)sharedRecordingAnnotations).ToolbarCommandInvoked +=
        (_, e) => recordingToolbarCommand = e.CommandId;
    RaiseButtonClick(sharedRecordingAnnotations.Toolbar.Controls.OfType<Button>()
        .Single(button => button.Text == "停止并保存"));
    AssertEqual("stop", recordingToolbarCommand, "共享工具栏把录制命令回传给模块");
    RaiseButtonClick(sharedRectangleButton);
    AssertEqual(
        CaptureAnnotationTool.Rectangle,
        sharedRecordingAnnotations.ActiveTool,
        "录屏点击共享截图工具进入实时批注");
    var recordingToolbarBounds = sharedRecordingAnnotations.ToolbarBounds;
    sharedRecordingAnnotations.Bounds = Rectangle.Inflate(recordingToolbarBounds, 20, 20);
    var recordingToolbarPoint = new Point(
        recordingToolbarBounds.Left + recordingToolbarBounds.Width / 2,
        recordingToolbarBounds.Top + recordingToolbarBounds.Height / 2);
    AssertTrue(
        !sharedRecordingAnnotations.HandlePointerHookEvent(new LiveAnnotationPointerEvent(
            LiveAnnotationPointerEventKind.LeftDown,
            recordingToolbarPoint)),
        "实时绘图时鼠标钩子不吞掉选区内的录屏停止按钮");
    RaiseButtonClick(sharedRectangleButton);
    AssertEqual(
        CaptureAnnotationTool.Operation,
        sharedRecordingAnnotations.ActiveTool,
        "再次点击当前工具返回鼠标穿透状态");
    AssertTrue(sharedRecordingAnnotations.AdjustWidth(1), "录屏核心批注会话调整共享粗细");
    AssertEqual(5, sharedRecordingWidth.Current, "录屏粗细写回截图共享控制器");
    sharedRecordingAnnotations.Color = Color.Blue;
    AssertEqual(Color.Blue.ToArgb(), sharedRecordingColor.ToArgb(), "录屏颜色写回截图批注状态");
    AssertTrue(
        typeof(LiveAnnotationSessionForm)
            .GetFields(System.Reflection.BindingFlags.Instance |
                       System.Reflection.BindingFlags.NonPublic)
            .Any(field => field.FieldType == typeof(CaptureAnnotationEditor)),
        "录屏批注会话直接组合截图核心编辑器");
    AssertTrue(
        sharedRecordingAnnotations.Editor.AddDraft(
            EditorTool.Rectangle,
            new Point(20, 20),
            new Point(120, 80),
            [],
            Color.Red,
            sharedRecordingAnnotations.ToolWidth),
        "录屏通过截图核心编辑器添加矩形");
    AssertTrue(
        sharedRecordingAnnotations.Editor.AddDraft(
            EditorTool.Arrow,
            new Point(10, 100),
            new Point(150, 30),
            [],
            Color.Blue,
            sharedRecordingAnnotations.ToolWidth),
        "录屏通过截图核心编辑器添加箭头");
    using var recordedAnnotationBitmap = new Bitmap(180, 120);
    using var recordedAnnotationGraphics = Graphics.FromImage(recordedAnnotationBitmap);
    recordedAnnotationGraphics.Clear(Color.Transparent);
    sharedRecordingAnnotations.RenderContent(recordedAnnotationGraphics);
    AssertTrue(
        CountNonTransparentPixels(recordedAnnotationBitmap) > 200,
        "录屏内容层渲染截图核心矩形和箭头");
    AssertTrue(sharedRecordingAnnotations.Undo(), "录屏撤销委托到截图核心编辑器");
    AssertEqual(1, sharedRecordingAnnotations.AnnotationCount, "录屏撤销更新核心批注文档");
}
using (var fullScreenRecordingAnnotations = new LiveAnnotationSessionForm(
           new Rectangle(0, 0, 1920, 1080),
           new Bitmap(1920, 1080),
           new TestTextClipboardService(),
           new ToolWidthController(ToolWidthRange.Create(1, 32), 4),
           new DrawingToolCoefficients(),
           AnnotationRotationStep.DefaultDegrees,
           DrawingCursorShape.Circle,
           Color.Red,
           _ => { },
           showMouseClickIndicator: false))
{
    AssertTrue(
        fullScreenRecordingAnnotations.Editor.AddDraft(
            EditorTool.Rectangle,
            new Point(600, 420),
            new Point(820, 570),
            [],
            Color.Red,
            fullScreenRecordingAnnotations.ToolWidth),
        "全屏录屏性能测试添加可移动元素");
    fullScreenRecordingAnnotations.ActiveTool = CaptureAnnotationTool.Select;
    var blockedSelectionPoint = new Point(500, 300);
    foreach (var blockedInput in new[]
    {
        LiveAnnotationPointerEventKind.RightDown,
        LiveAnnotationPointerEventKind.RightUp,
        LiveAnnotationPointerEventKind.MiddleDown,
        LiveAnnotationPointerEventKind.MiddleUp,
        LiveAnnotationPointerEventKind.XButtonDown,
        LiveAnnotationPointerEventKind.XButtonUp,
        LiveAnnotationPointerEventKind.HorizontalWheel
    })
    {
        AssertTrue(
            fullScreenRecordingAnnotations.HandlePointerHookEvent(
                new LiveAnnotationPointerEvent(blockedInput, blockedSelectionPoint)),
            $"录屏选择模式阻止 {blockedInput} 与底层屏幕内容交互");
    }
    AssertTrue(
        !fullScreenRecordingAnnotations.HandlePointerHookEvent(
            new LiveAnnotationPointerEvent(
                LiveAnnotationPointerEventKind.RightDown,
                new Point(2000, 1200))),
        "录屏选择模式不拦截录制区域外的鼠标输入");
    AssertTrue(
        fullScreenRecordingAnnotations.HandlePointerHookEvent(
            new LiveAnnotationPointerEvent(
                LiveAnnotationPointerEventKind.LeftDown,
                new Point(700, 500))),
        "录屏选择模式消费元素上的左键按下");
    fullScreenRecordingAnnotations.HandlePointerHookEvent(
        new LiveAnnotationPointerEvent(
            LiveAnnotationPointerEventKind.Move,
            new Point(728, 518)));
    var partialContentInvalidation =
        fullScreenRecordingAnnotations.LastContentInvalidationBounds;
    AssertTrue(
        !partialContentInvalidation.IsEmpty &&
        partialContentInvalidation.Width < 500 &&
        partialContentInvalidation.Height < 400,
        $"全屏录屏移动元素只局部重绘。Dirty={partialContentInvalidation}");
    fullScreenRecordingAnnotations.HandlePointerHookEvent(
        new LiveAnnotationPointerEvent(
            LiveAnnotationPointerEventKind.LeftUp,
            new Point(728, 518)));
}
AssertTrue(
    typeof(ScreenRecordingModule).Assembly.GetType(
        "ScreenshotTool.ScreenRecording.RecordingAnnotationDocument") is null,
    "录屏模块不再携带重复批注文档实现");
AssertTrue(
    typeof(ScreenRecordingModule).Assembly.GetType(
        "ScreenshotTool.ScreenRecording.RecordingControlForm") is null,
    "录屏模块不再携带重复编辑工具窗口");
AssertTrue(
    typeof(ScreenRecordingModule).Assembly.GetTypes()
        .All(type => !typeof(Form).IsAssignableFrom(type)),
    "录屏参数转入宿主设置页，模块不再创建专用设置窗口");
AssertTrue(
    typeof(CaptureOverlayForm)
        .GetFields(System.Reflection.BindingFlags.Instance |
                   System.Reflection.BindingFlags.NonPublic)
        .Any(field => field.FieldType == typeof(CaptureEditorToolbar)) &&
    typeof(LiveAnnotationSessionForm)
        .GetFields(System.Reflection.BindingFlags.Instance |
                   System.Reflection.BindingFlags.NonPublic)
        .Any(field => field.FieldType == typeof(CaptureEditorToolbar)),
    "截图与录屏实时批注共用唯一菜单栏组件");
AssertTrue(
    typeof(LiveAnnotationSessionForm)
        .GetFields(System.Reflection.BindingFlags.Instance |
                   System.Reflection.BindingFlags.NonPublic)
        .Count(field => field.FieldType == typeof(LiveAnnotationPointerHook)) == 2,
    "录屏透明批注层由宿主核心捕获鼠标输入");
AssertTrue(
    typeof(LiveAnnotationSessionForm)
        .GetFields(System.Reflection.BindingFlags.Instance |
                   System.Reflection.BindingFlags.NonPublic)
        .Any(field => field.Name == "_inputSurface" && field.FieldType == typeof(Bitmap)) &&
    typeof(LiveAnnotationContentForm)
        .GetFields(System.Reflection.BindingFlags.Instance |
                   System.Reflection.BindingFlags.NonPublic)
        .Any(field => field.Name == "_surface" && field.FieldType == typeof(Bitmap)),
    "录屏透明输入层和内容层使用持久离屏画布避免拖动闪烁");
AssertTrue(
    typeof(ICaptureArtifactHost).GetMethod("CompleteCaptureSession") is not null,
    "模块可在产物保存后请求宿主完成当前捕获会话");
var savedRecordingResult = new RecordingControlResult(
    Saved: true,
    Discarded: false,
    FilePath: "C:\\截图目录\\录屏完成.mp4",
    Error: null);
AssertTrue(
    ScreenRecordingFeature.ShouldCompleteCaptureSession(
        savedRecordingResult,
        failure: null,
        cancellationRequested: false),
    "录屏保存成功后结束截图会话");
AssertTrue(
    !ScreenRecordingFeature.ShouldCompleteCaptureSession(
        savedRecordingResult with { Saved = false, Discarded = true },
        failure: null,
        cancellationRequested: false) &&
    !ScreenRecordingFeature.ShouldCompleteCaptureSession(
        savedRecordingResult,
        failure: new InvalidOperationException("模拟保存失败"),
        cancellationRequested: false) &&
    !ScreenRecordingFeature.ShouldCompleteCaptureSession(
        savedRecordingResult,
        failure: null,
        cancellationRequested: true),
    "取消或失败时恢复截图会话");
var recordingArtifactHost = new TestCaptureArtifactHost();
ScreenRecordingFeature.CompleteSavedRecording(
    recordingArtifactHost,
    savedRecordingResult.FilePath!);
AssertEqual(
    "notify:录屏完成.mp4,complete",
    string.Join(',', recordingArtifactHost.Calls),
    "录屏先通知保存成功再请求关闭截图框");

var recordingModuleTestDirectory = Path.Combine(
    Path.GetTempPath(),
    "ScreenshotTool.RecordingModuleTests",
    Guid.NewGuid().ToString("N"));

var incompatibleRecordingModule = new ScreenRecordingModule();
AssertEqual(new Version(1, 10, 0), ScreenRecordingModule.MinimumHostVersion,
    "模块自带设置页契约的录屏最低主程序版本");
AssertEqual(new Version(1, 7, 0), incompatibleRecordingModule.Version,
    "录屏模块结束截图会话版本");
var incompatibleRecordingModuleRejected = false;
try
{
    incompatibleRecordingModule.Initialize(new TestModuleContext(new Version(1, 0, 0)));
}
catch (NotSupportedException exception)
{
    incompatibleRecordingModuleRejected = exception.Message.Contains(
        "请同时更新轻截基础程序",
        StringComparison.Ordinal);
}
AssertTrue(incompatibleRecordingModuleRejected, "旧版主程序会收到明确的录屏模块兼容提示");

using (var failingFeatureSession = new CaptureFeatureSession(
           new TestCaptureFeatureCatalog(new ThrowingToolbarFeature()),
           new TestCaptureFeatureHost()))
{
    var failingCommand = failingFeatureSession.GetToolbarCommands().Single();
    var failingResult = failingFeatureSession
        .ExecuteToolbarCommandAsync(failingCommand)
        .GetAwaiter()
        .GetResult();
    AssertTrue(!failingResult.Succeeded, "模块按钮异常不会被当作执行成功");
    AssertTrue(
        failingResult.ErrorMessage?.Contains("模拟录屏故障", StringComparison.Ordinal) == true,
        "模块按钮异常会返回可见的具体错误");
    AssertEqual(0, failingFeatureSession.GetToolbarCommands().Count, "故障模块只对当前截图会话停用");
}

try
{
    Directory.CreateDirectory(recordingModuleTestDirectory);
    var recordingModulePackageDirectory = Path.Combine(
        recordingModuleTestDirectory,
        "ScreenRecording");
    Directory.CreateDirectory(recordingModulePackageDirectory);
    var recordingModulePath = Path.Combine(
        recordingModulePackageDirectory,
        "ScreenshotTool.ScreenRecording.dll");
    File.Copy(typeof(ScreenRecordingModule).Assembly.Location, recordingModulePath);
    var recordingDependencyPath = Path.Combine(
        recordingModulePackageDirectory,
        "NativeDependency.dll");
    File.WriteAllBytes(recordingDependencyPath, [1, 2, 3, 4]);
    using var recordingModuleHost = new ModuleHost(recordingModuleTestDirectory);
    var refresh = recordingModuleHost.Refresh();
    AssertEqual(0, refresh.Errors.Count, "录屏模块私有原生依赖不会被误报为模块错误");
    AssertEqual(1, refresh.Modules.Count, "发现并加载可选录屏模块");
    AssertEqual("screenshot-tool.screen-recording", refresh.Modules[0].Id, "录屏模块 ID 保持稳定");
    var recordingSettingsPages = recordingModuleHost.CreateSettingsPages(
        new TestModuleSettingsHost());
    AssertEqual(1, recordingSettingsPages.Count, "录屏模块安装后提供自带设置页");
    using var recordingSettingsPage = recordingSettingsPages[0];
    AssertEqual(
        "screenshot-tool.screen-recording.settings",
        recordingSettingsPage.Id,
        "录屏模块设置页 ID 保持稳定");
    var recordingFeatures = recordingModuleHost.CreateCaptureFeatures();
    AssertEqual(1, recordingFeatures.Count, "录屏模块为截图会话创建功能实例");
    using var recordingFeature = recordingFeatures[0];
    AssertTrue(recordingFeature is ICaptureToolbarCommandProvider, "录屏功能提供截图工具栏命令");
    var recordingCommands = ((ICaptureToolbarCommandProvider)recordingFeature).GetToolbarCommands();
    AssertEqual("录屏", recordingCommands[0].Text, "录屏模块暴露可组合工具栏入口");
    File.WriteAllBytes(recordingDependencyPath, [1, 2, 3, 4, 5]);
    var reloadedRecordingModules = recordingModuleHost.Refresh();
    AssertTrue(reloadedRecordingModules.Changed, "模块文件夹内的私有依赖更新会触发热重载");
    AssertEqual(1, reloadedRecordingModules.Modules.Count, "私有依赖更新后录屏模块保持可用");
    Directory.Delete(recordingModulePackageDirectory, recursive: true);
    var removedRecordingModules = recordingModuleHost.Refresh();
    AssertEqual(0, removedRecordingModules.Modules.Count, "删除录屏模块文件夹后立即从目录卸载");
    AssertEqual(
        0,
        recordingModuleHost.CreateSettingsPages(new TestModuleSettingsHost()).Count,
        "卸载录屏模块后不再出现录屏设置页");
    AssertEqual("screenshot-tool.screen-recording.feature", recordingFeature.Id, "活动录屏会话保留延迟释放租约");
}
finally
{
    if (Directory.Exists(recordingModuleTestDirectory))
    {
        Directory.Delete(recordingModuleTestDirectory, recursive: true);
    }
}

LongCaptureLogicTests.Run();
LongCapturePreparationTests.Run();
BidirectionalLongCaptureTests.Run();
LongCaptureWindowTests.Run();

Console.WriteLine("首次与更新启动工作台、GitHub 软件更新、OCR 离线识别模块、二维码扫描模块、可选录屏模块、实时批注、图片命名、文字重编辑、重叠元素轮换、粗细记忆、旋转与缩放、八手柄单边缩放、Ctrl 固定步长、元素吸附与双击 Ctrl、Ctrl+A 分级扩展、Alt 临时移动、Ctrl 多选、框选与整组操作、透明文字、重新框选、模块热加载、长截图拼接、保存通知与文件定位测试全部通过。");
return;

SelectionResizeEdges Hit(int x, int y) =>
    SelectionResizer.HitTest(selection, new Point(x, y), tolerance);

Rectangle Resize(SelectionResizeEdges edges, int x, int y) =>
    SelectionResizer.Resize(selection, edges, new Point(x, y), limits, minimumSize: 12);

static void AssertEqual<T>(T expected, T actual, string name)
    where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{name}失败：期望 {expected}，实际 {actual}。");
    }
}

static void AssertSingleColumnSettings(Control page, int expectedCount, string name)
{
    var rows = Descendants(page)
        .OfType<Panel>()
        .Where(panel => string.Equals(panel.Tag as string, "SettingRow", StringComparison.Ordinal))
        .OrderBy(panel => panel.Top)
        .ToArray();
    AssertEqual(expectedCount, rows.Length, $"{name}设置项数量");
    foreach (var row in rows)
    {
        var inputCount = row.Controls.Cast<Control>().Count(control =>
            control is CheckBox or ComboBox or NumericUpDown or TextBox);
        AssertEqual(1, inputCount, $"{name}每行只有一个设置控件");
    }
    for (var index = 1; index < rows.Length; index++)
    {
        AssertTrue(
            rows[index].Top >= rows[index - 1].Bottom,
            $"{name}每一行只放置一个设置项");
    }
}

static IEnumerable<Control> Descendants(Control parent)
{
    foreach (Control child in parent.Controls)
    {
        yield return child;
        foreach (var descendant in Descendants(child))
        {
            yield return descendant;
        }
    }
}

static void AssertTrue(bool value, string name)
{
    if (!value)
    {
        throw new InvalidOperationException($"{name}失败。");
    }
}

static void RaiseButtonClick(Button button)
{
    typeof(Button).GetMethod(
            "OnClick",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!
        .Invoke(button, [EventArgs.Empty]);
}

static void VerifyPaddleOcrModule(
    PaddleOcrModuleBase module,
    PaddleOcrVariant variant,
    string expectedModuleId,
    string expectedDisplayName,
    string expectedFeatureId,
    string expectedCommandId,
    string expectedCommandText)
{
    var moduleDirectory = Path.Combine(
        Path.GetTempPath(),
        "ScreenshotTool.PaddleOcrMetadataTests",
        Guid.NewGuid().ToString("N"));
    try
    {
        CreatePaddleOcrModelPlaceholders(moduleDirectory, variant);
        AssertEqual(
            new Version(1, 11, 0),
            PaddleOcrModuleBase.MinimumHostVersion,
            $"{expectedDisplayName}最低主程序版本");
        AssertEqual(expectedModuleId, module.Id, $"{expectedDisplayName}模块 ID");
        AssertEqual(expectedDisplayName, module.DisplayName, $"{expectedDisplayName}显示名称");
        AssertEqual(new Version(1, 0, 0), module.Version, $"{expectedDisplayName}模块版本");

        var incompatibleRejected = false;
        try
        {
            module.Initialize(new TestModuleContext(new Version(1, 10, 0)));
        }
        catch (NotSupportedException)
        {
            incompatibleRejected = true;
        }
        AssertTrue(incompatibleRejected, $"{expectedDisplayName}拒绝旧版主程序");

        module.Initialize(new TestModuleContext(
            PaddleOcrModuleBase.MinimumHostVersion,
            moduleDirectory));
        var features = module.CreateCaptureFeatures().ToArray();
        AssertEqual(1, features.Length, $"{expectedDisplayName}创建独立截图功能");
        using var feature = features[0];
        AssertEqual(expectedFeatureId, feature.Id, $"{expectedDisplayName}功能 ID");
        AssertTrue(
            feature is ICaptureToolbarCommandProvider,
            $"{expectedDisplayName}提供截图工具栏入口");
        var commands = ((ICaptureToolbarCommandProvider)feature).GetToolbarCommands();
        AssertEqual(1, commands.Count, $"{expectedDisplayName}只注册一个命令");
        AssertEqual(expectedCommandId, commands[0].Id, $"{expectedDisplayName}命令 ID");
        AssertEqual(expectedCommandText, commands[0].Text, $"{expectedDisplayName}命令文字");
    }
    finally
    {
        module.Dispose();
        if (Directory.Exists(moduleDirectory))
        {
            Directory.Delete(moduleDirectory, recursive: true);
        }
    }
}

static void VerifyPaddleOcrModulePackage(
    string entryAssemblyPath,
    string packageName,
    string expectedModuleId)
{
    var testDirectory = Path.Combine(
        Path.GetTempPath(),
        "ScreenshotTool.PaddleOcrModuleTests",
        Guid.NewGuid().ToString("N"));
    ModuleHost? moduleHost = null;
    ICaptureFeature? feature = null;
    try
    {
        var packageDirectory = Path.Combine(testDirectory, packageName);
        Directory.CreateDirectory(packageDirectory);
        var variant = packageName.EndsWith("Tiny", StringComparison.Ordinal)
            ? PaddleOcrVariant.Tiny
            : PaddleOcrVariant.Small;
        CreatePaddleOcrModelPlaceholders(packageDirectory, variant);
        var files = new[]
        {
            entryAssemblyPath,
            typeof(PaddleOcrModuleBase).Assembly.Location,
            Path.Combine(AppContext.BaseDirectory, "RapidOcrNet.dll"),
            Path.Combine(AppContext.BaseDirectory, "Microsoft.ML.OnnxRuntime.dll"),
            Path.Combine(AppContext.BaseDirectory, "SkiaSharp.dll"),
            Path.Combine(AppContext.BaseDirectory, "Clipper2Lib.dll"),
            Path.Combine(AppContext.BaseDirectory, "System.Numerics.Tensors.dll")
        };
        foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AssertTrue(File.Exists(file), $"PP-OCR 测试依赖存在：{Path.GetFileName(file)}");
            File.Copy(file, Path.Combine(packageDirectory, Path.GetFileName(file)));
        }

        moduleHost = new ModuleHost(testDirectory);
        var loaded = moduleHost.Refresh();
        AssertEqual(0, loaded.Errors.Count, $"{packageName}连同私有托管依赖加载");
        AssertEqual(1, loaded.Modules.Count, $"{packageName}只发现一个模块入口");
        AssertEqual(expectedModuleId, loaded.Modules[0].Id, $"{packageName}读取稳定模块 ID");
        var features = moduleHost.CreateCaptureFeatures();
        AssertEqual(1, features.Count, $"{packageName}创建截图功能实例");
        feature = features[0];

        Directory.Delete(packageDirectory, recursive: true);
        var removed = moduleHost.Refresh();
        AssertEqual(0, removed.Modules.Count, $"{packageName}删除后立即从目录卸载");
        AssertEqual(
            1,
            ((ICaptureToolbarCommandProvider)feature).GetToolbarCommands().Count,
            $"{packageName}活动会话保留延迟释放租约");
    }
    finally
    {
        feature?.Dispose();
        moduleHost?.Dispose();
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }
}

static void CreatePaddleOcrModelPlaceholders(
    string moduleDirectory,
    PaddleOcrVariant variant)
{
    var files = PaddleOcrModelFiles.Resolve(moduleDirectory, variant);
    Directory.CreateDirectory(Path.GetDirectoryName(files.DetectorPath)!);
    foreach (var path in new[]
             {
                 files.DetectorPath,
                 files.ClassifierPath,
                 files.RecognizerPath,
                 files.DictionaryPath
             })
    {
        File.WriteAllBytes(path, [0]);
    }
}

static Bitmap CreateSolidBitmap(Size size, Color color)
{
    var bitmap = new Bitmap(size.Width, size.Height);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(color);
    return bitmap;
}

static bool ContainsPixelDifferentFrom(Bitmap bitmap, Rectangle bounds, Color color)
{
    var expected = color.ToArgb();
    for (var y = bounds.Top; y < bounds.Bottom; y++)
    {
        for (var x = bounds.Left; x < bounds.Right; x++)
        {
            if (bitmap.GetPixel(x, y).ToArgb() != expected)
            {
                return true;
            }
        }
    }

    return false;
}

static Bitmap RenderArrowForWidth(float width)
{
    var bitmap = new Bitmap(160, 80);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Black);
    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    new ArrowAnnotation(
        new Point(20, 40),
        new Point(140, 40),
        Color.White,
        width).Render(graphics, bitmap);
    return bitmap;
}

static int CountNonBlackPixelsInColumn(Bitmap bitmap, int x)
{
    var count = 0;
    for (var y = 0; y < bitmap.Height; y++)
    {
        if (bitmap.GetPixel(x, y).ToArgb() != Color.Black.ToArgb())
        {
            count++;
        }
    }
    return count;
}

static int CountNonTransparentPixels(Bitmap bitmap)
{
    var count = 0;
    for (var y = 0; y < bitmap.Height; y++)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            if (bitmap.GetPixel(x, y).A > 0)
            {
                count++;
            }
        }
    }
    return count;
}

static Bitmap CreateQrCodeBitmap(string payload, int size)
{
    var matrix = new MultiFormatWriter().encode(
        payload,
        BarcodeFormat.QR_CODE,
        size,
        size,
        new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.CHARACTER_SET] = "UTF-8"
        });
    var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.White);
    using var blackBrush = new SolidBrush(Color.Black);
    for (var y = 0; y < matrix.Height; y++)
    {
        for (var x = 0; x < matrix.Width; x++)
        {
            if (matrix[x, y])
            {
                graphics.FillRectangle(blackBrush, x, y, 1, 1);
            }
        }
    }

    return bitmap;
}

static int CountNonBlackPixels(Bitmap bitmap, Rectangle bounds)
{
    var count = 0;
    for (var y = bounds.Top; y < bounds.Bottom; y++)
    {
        for (var x = bounds.Left; x < bounds.Right; x++)
        {
            if (bitmap.GetPixel(x, y).ToArgb() != Color.Black.ToArgb())
            {
                count++;
            }
        }
    }
    return count;
}

static int MaxNonBlackPixelsInColumn(Bitmap bitmap, int left, int right)
{
    var maximum = 0;
    for (var x = left; x < right; x++)
    {
        maximum = Math.Max(maximum, CountNonBlackPixelsInColumn(bitmap, x));
    }
    return maximum;
}

static Bitmap RenderDocumentSelection(AnnotationDocument document, Bitmap source, Rectangle selection)
{
    var result = new Bitmap(selection.Width, selection.Height);
    using var graphics = Graphics.FromImage(result);
    graphics.Clear(Color.Black);
    graphics.TranslateTransform(-selection.X, -selection.Y);
    graphics.SetClip(selection);
    document.Render(graphics, source);
    return result;
}

internal sealed class TestCaptureFeatureHost(
    bool longCaptureSafetyChecksEnabled = false,
    bool captureSystemAudio = ScreenRecordingPreferences.DefaultCaptureSystemAudio,
    bool captureMicrophone = ScreenRecordingPreferences.DefaultCaptureMicrophone,
    bool showMouseClickIndicator = ScreenRecordingPreferences.DefaultShowMouseClickIndicator,
    int framesPerSecond = ScreenRecordingPreferences.DefaultFramesPerSecond,
    int videoBitrate = ScreenRecordingPreferences.DefaultVideoBitrate,
    CaptureRegionIndicatorStyle regionIndicatorStyle =
        ScreenRecordingPreferences.DefaultRegionIndicatorStyle) : ICaptureFeatureHost
{
    public bool HasSelection => true;
    public Rectangle Selection => new(0, 0, 40, 40);
    public Point CursorClientPosition => new(20, 20);
    public int Dpi => 96;
    public bool GetBooleanPreference(string id, bool defaultValue) => id switch
    {
        LongCapturePreferences.SafetyChecksId => longCaptureSafetyChecksEnabled,
        ScreenRecordingPreferences.CaptureSystemAudioId => captureSystemAudio,
        ScreenRecordingPreferences.CaptureMicrophoneId => captureMicrophone,
        ScreenRecordingPreferences.ShowMouseClickIndicatorId => showMouseClickIndicator,
        _ => defaultValue
    };
    public int GetIntegerPreference(string id, int defaultValue) => id switch
    {
        ScreenRecordingPreferences.FramesPerSecondId => framesPerSecond,
        ScreenRecordingPreferences.VideoBitrateId => videoBitrate,
        ScreenRecordingPreferences.RegionIndicatorStyleId => (int)regionIndicatorStyle,
        _ => defaultValue
    };
    public void InvalidateAll()
    {
    }
    public void Invalidate(Rectangle bounds)
    {
    }
    public void SetCursor(Cursor cursor)
    {
    }
    public void SetMouseCapture(bool capture)
    {
    }
    public Bitmap CopyDesktopSelection() => new(Selection.Width, Selection.Height);
}

internal sealed class TestCaptureArtifactHost : ICaptureArtifactHost
{
    public List<string> Calls { get; } = [];
    public string OutputFolder => "C:\\截图目录";
    public bool HasSelection => true;
    public Rectangle Selection => new(0, 0, 40, 40);
    public Point CursorClientPosition => new(20, 20);
    public int Dpi => 96;
    public bool GetBooleanPreference(string id, bool defaultValue) => defaultValue;
    public int GetIntegerPreference(string id, int defaultValue) => defaultValue;
    public void InvalidateAll() { }
    public void Invalidate(Rectangle bounds) { }
    public void SetCursor(Cursor cursor) { }
    public void SetMouseCapture(bool capture) { }
    public Bitmap CopyDesktopSelection() => new(Selection.Width, Selection.Height);
    public void NotifyArtifactSaved(string path) =>
        Calls.Add($"notify:{Path.GetFileName(path)}");
    public void CompleteCaptureSession() => Calls.Add("complete");
}

internal sealed class TestModuleContext(
    Version hostVersion,
    string? moduleDirectory = null) : IModuleContext
{
    public string ModuleDirectory { get; } = moduleDirectory ?? AppContext.BaseDirectory;

    public Version HostVersion { get; } = hostVersion;
}

internal sealed class TestModuleSettingsHost : IModuleSettingsHost
{
    private readonly Dictionary<string, bool> _booleans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _integers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);

    public int SaveCount { get; private set; }

    public bool GetBoolean(string id, bool defaultValue) =>
        _booleans.TryGetValue(id, out var value) ? value : defaultValue;

    public int GetInteger(string id, int defaultValue) =>
        _integers.TryGetValue(id, out var value) ? value : defaultValue;

    public string GetString(string id, string defaultValue) =>
        _strings.TryGetValue(id, out var value) ? value : defaultValue;

    public void SetBoolean(string id, bool value) => _booleans[id] = value;

    public void SetInteger(string id, int value) => _integers[id] = value;

    public void SetString(string id, string value) => _strings[id] = value;

    public void Save() => SaveCount++;
}

internal sealed class TestStartupEntryStore : IStartupEntryStore
{
    public string? Name { get; private set; }
    public string? Value { get; set; }

    public string? GetValue(string name) => Value;

    public void SetValue(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public void DeleteValue(string name)
    {
        Name = name;
        Value = null;
    }
}

internal sealed class TestCaptureFeatureCatalog(ICaptureFeature feature) : ICaptureFeatureCatalog
{
    public IReadOnlyList<ICaptureFeature> CreateCaptureFeatures() => [feature];
}

internal sealed class ThrowingToolbarFeature : CaptureFeatureBase, ICaptureToolbarCommandProvider
{
    private static readonly IReadOnlyList<CaptureToolbarCommand> Commands =
    [
        new("tests.throwing-toolbar", "录屏", "测试故障模块")
    ];

    public override string Id => "tests.throwing-toolbar-feature";

    public override int Order => 0;

    public IReadOnlyList<CaptureToolbarCommand> GetToolbarCommands() => Commands;

    public Task ExecuteToolbarCommandAsync(string commandId, CancellationToken cancellationToken) =>
        Task.FromException(new InvalidOperationException("模拟录屏故障"));
}

internal sealed class TestTextClipboardService : IClipboardService
{
    public string? Text { get; set; }

    public void SetImage(Image image)
    {
    }

    public Bitmap? GetImage() => null;

    public string? GetText() => Text;

    public void SetText(string text) => Text = text;
}

internal sealed class TestOcrRecognizer(string text) : IOcrRecognizer
{
    public Task<string> RecognizeAsync(Bitmap image, CancellationToken cancellationToken)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new InvalidOperationException("OCR 接收有效选区位图失败。");
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(text);
    }
}

internal sealed class TestBlockingOcrRecognizer : IOcrRecognizer, IDisposable
{
    public ManualResetEventSlim Started { get; } = new(initialState: false);

    public async Task<string> RecognizeAsync(Bitmap image, CancellationToken cancellationToken)
    {
        Started.Set();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return string.Empty;
    }

    public void Dispose() => Started.Dispose();
}

internal sealed class TestPaddleOcrRecognizer(string text) : IPaddleOcrRecognizer
{
    public Task<string> RecognizeAsync(Bitmap image, CancellationToken cancellationToken)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new InvalidOperationException("PP-OCR 接收有效选区位图失败。");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(text);
    }

    public void Dispose()
    {
    }
}

internal sealed class TestQrCodeScanner(IReadOnlyList<string> results) : IQrCodeScanner
{
    public Task<IReadOnlyList<string>> ScanAsync(
        Bitmap image,
        CancellationToken cancellationToken)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new InvalidOperationException("二维码扫描器接收有效选区位图失败。");
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(results);
    }
}

internal sealed class TestBlockingQrCodeScanner : IQrCodeScanner, IDisposable
{
    public ManualResetEventSlim Started { get; } = new(initialState: false);

    public async Task<IReadOnlyList<string>> ScanAsync(
        Bitmap image,
        CancellationToken cancellationToken)
    {
        Started.Set();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return [];
    }

    public void Dispose() => Started.Dispose();
}

internal sealed class TestOcrCaptureHost(Bitmap? selectionImage = null) :
    ICaptureTextResultHost,
    ICaptureArtifactHost
{
    public bool Completed { get; private set; }
    public bool SelectionCopied { get; private set; }
    public string? ResultTitle { get; private set; }
    public string? ResultText { get; private set; }
    public bool HasSelection => true;
    public Rectangle Selection => new(
        0,
        0,
        selectionImage?.Width ?? 320,
        selectionImage?.Height ?? 120);
    public Point CursorClientPosition => Point.Empty;
    public int Dpi => 96;
    public string OutputFolder => Path.GetTempPath();
    public bool GetBooleanPreference(string id, bool defaultValue) => defaultValue;
    public int GetIntegerPreference(string id, int defaultValue) => defaultValue;
    public void InvalidateAll() { }
    public void Invalidate(Rectangle bounds) { }
    public void SetCursor(Cursor cursor) { }
    public void SetMouseCapture(bool capture) { }

    public Bitmap CopyDesktopSelection()
    {
        SelectionCopied = true;
        return selectionImage is null
            ? new Bitmap(Selection.Width, Selection.Height)
            : new Bitmap(selectionImage);
    }

    public void ShowTextResult(string title, string text)
    {
        ResultTitle = title;
        ResultText = text;
    }

    public void NotifyArtifactSaved(string path) { }

    public void CompleteCaptureSession() => Completed = true;
}

internal sealed class StaticJsonHttpMessageHandler(string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
}

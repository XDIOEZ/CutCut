using ScreenshotTool.Presentation;
using ScreenshotTool.Presentation.Pages;
using ScreenshotTool.Infrastructure;
using ScreenshotTool.Infrastructure.Modules;
using ScreenshotTool.Editing;
using ScreenshotTool.Contracts;
using ScreenshotTool.TestModule;
using ScreenshotTool.Core;
using ScreenshotTool;
using ScreenshotTool.Abstractions;

var selection = new Rectangle(100, 100, 400, 300);
const int tolerance = 6;

AssertTrue(
    !SingleInstanceLaunchPolicy.RequiresRestartConfirmation(ownsMutex: true),
    "旧实例未运行时直接正常启动且不显示特殊启动提示");
AssertTrue(
    SingleInstanceLaunchPolicy.RequiresRestartConfirmation(ownsMutex: false),
    "检测到旧实例时显示关闭旧实例并重新启动的特殊提示");
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
    CaptureSelectAllAction.ExpandSelectionToFullScreen,
    CaptureSelectAllPolicy.Resolve(editingElementCount: 0, allEditingElementsSelected: false),
    "没有编辑元素时 Ctrl+A 直接扩充到全屏");
AssertEqual(
    CaptureSelectAllAction.SelectEditingElements,
    CaptureSelectAllPolicy.Resolve(editingElementCount: 3, allEditingElementsSelected: false),
    "存在未全选编辑元素时 Ctrl+A 优先全选元素");
AssertEqual(
    CaptureSelectAllAction.ExpandSelectionToFullScreen,
    CaptureSelectAllPolicy.Resolve(editingElementCount: 3, allEditingElementsSelected: true),
    "编辑元素已经全选时再次 Ctrl+A 扩充到全屏");

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
AssertEqual(StickerSelectionMoveMode.FollowSelection, new AppSettings().Preferences.StickerSelectionMoveMode, "贴纸默认跟随截图框");
AssertTrue(!new AppSettings().Preferences.LongCaptureSafetyChecksEnabled, "长截图安全校验默认关闭");
using (var longCaptureSettingsPage = new LongCaptureSettingsPage(safetyChecksEnabled: false))
{
    AssertTrue(!longCaptureSettingsPage.SafetyChecksEnabled, "长截图设置页显示默认宽松模式");
    longCaptureSettingsPage.SafetyChecksEnabled = true;
    AssertTrue(longCaptureSettingsPage.SafetyChecksEnabled, "长截图设置页可开启安全校验");
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
}

var settingsTestDirectory = Path.Combine(
    Path.GetTempPath(),
    "ScreenshotTool.SettingsTests",
    Guid.NewGuid().ToString("N"));
try
{
    var profileStore = new JsonSettingsStore(settingsTestDirectory, "account-demo");
    var configuredSettings = new AppSettings
    {
        OutputFolder = Path.Combine(settingsTestDirectory, "captures"),
        StartMinimized = true,
        HotkeyModifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt,
        HotkeyVirtualKey = (int)Keys.Q,
        Preferences = new UserPreferences
        {
            StickerSelectionMoveMode = StickerSelectionMoveMode.KeepScreenPosition,
            MinimumToolWidth = 3,
            MaximumToolWidth = 17,
            LongCaptureSafetyChecksEnabled = true,
            DrawingToolCoefficients = configuredCoefficients
        }
    };
    profileStore.Save(configuredSettings);
    var savedJson = File.ReadAllText(profileStore.SettingsPath);
    AssertTrue(savedJson.Contains("\"schemaVersion\": 1", StringComparison.Ordinal), "JSON 保存配置版本");
    AssertTrue(savedJson.Contains("\"profileId\": \"account-demo\"", StringComparison.Ordinal), "JSON 保存配置身份");
    AssertTrue(savedJson.Contains("\"preferences\"", StringComparison.Ordinal), "JSON 独立保存用户偏好");
    AssertTrue(savedJson.Contains("\"keepScreenPosition\"", StringComparison.Ordinal), "JSON 使用可读的贴纸模式");
    AssertTrue(savedJson.Contains("\"longCaptureSafetyChecksEnabled\": true", StringComparison.Ordinal), "JSON 保存长截图安全开关");
    AssertTrue(savedJson.Contains("\"arrowHeadWidth\": 4", StringComparison.Ordinal), "JSON 保存箭头头部基础系数");

    var loadedSettings = profileStore.Load();
    AssertEqual(StickerSelectionMoveMode.KeepScreenPosition,
        loadedSettings.Preferences.StickerSelectionMoveMode,
        "JSON 恢复贴纸移动喜好");
    AssertEqual(3, loadedSettings.Preferences.MinimumToolWidth, "JSON 恢复粗细下限");
    AssertEqual(17, loadedSettings.Preferences.MaximumToolWidth, "JSON 恢复粗细上限");
    AssertTrue(loadedSettings.Preferences.LongCaptureSafetyChecksEnabled, "JSON 恢复长截图安全开关");
    AssertEqual(1.5M, loadedSettings.Preferences.DrawingToolCoefficients.Rectangle, "JSON 恢复矩形基础系数");
    AssertEqual(1.25M, loadedSettings.Preferences.DrawingToolCoefficients.ArrowBody, "JSON 恢复箭身基础系数");
    AssertEqual(4M, loadedSettings.Preferences.DrawingToolCoefficients.ArrowHeadWidth, "JSON 恢复箭头宽度基础系数");
    AssertTrue(loadedSettings.StartMinimized, "JSON 恢复启动喜好");
    AssertEqual((int)Keys.Q, loadedSettings.HotkeyVirtualKey, "JSON 恢复快捷键");

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
    AssertTrue(!migratedSettings.Preferences.LongCaptureSafetyChecksEnabled, "旧 JSON 默认迁移为宽松长截图");
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

var moduleTestDirectory = Path.Combine(Path.GetTempPath(), "ScreenshotTool.ModuleTests", Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(moduleTestDirectory);
    var modulePath = Path.Combine(moduleTestDirectory, "ScreenshotTool.TestModule.dll");
    File.Copy(typeof(TestHotLoadModule).Assembly.Location, modulePath);
    using var moduleHost = new ModuleHost(moduleTestDirectory);
    var loadedModules = moduleHost.Refresh();
    AssertEqual(1, loadedModules.Modules.Count, "热加载模块程序集");
    AssertEqual("tests.hot-load", loadedModules.Modules[0].Id, "读取模块元数据");

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

    File.Delete(modulePath);
    var removedModules = moduleHost.Refresh();
    AssertEqual(0, removedModules.Modules.Count, "删除 DLL 后热拆卸模块");
    AssertTrue(feature.HandleKeyDown(new KeyEventArgs(Keys.Control | Keys.Alt | Keys.M)), "当前截图会话延迟释放旧模块");
}
finally
{
    if (Directory.Exists(moduleTestDirectory))
    {
        Directory.Delete(moduleTestDirectory, recursive: true);
    }
}

LongCaptureLogicTests.Run();
LongCapturePreparationTests.Run();
BidirectionalLongCaptureTests.Run();
LongCaptureWindowTests.Run();

Console.WriteLine("绘制元素四角自由缩放与导出、Ctrl+A 编辑元素全选与二次全屏、Alt 临时移动与绘图工具恢复、所有编辑元素 Alt 加左键移动、右键移动即时重绘与残影清理、分层 Esc、Ctrl 多选、左键拖拽框选、整组拖动与 Delete、透明文字、重新框选启动、工具开关、文件夹拖放与旧图片迁移、选区、用户偏好 JSON、贴纸策略、工具粗细、模块、长截图确定性拼接与文件定位测试全部通过。");
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

static void AssertTrue(bool value, string name)
{
    if (!value)
    {
        throw new InvalidOperationException($"{name}失败。");
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
    bool longCaptureSafetyChecksEnabled = false) : ICaptureFeatureHost
{
    public bool HasSelection => true;
    public Rectangle Selection => new(0, 0, 40, 40);
    public Point CursorClientPosition => new(20, 20);
    public int Dpi => 96;
    public bool GetBooleanPreference(string id, bool defaultValue) =>
        id == CaptureFeaturePreferenceIds.LongCaptureSafetyChecks
            ? longCaptureSafetyChecksEnabled
            : defaultValue;
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

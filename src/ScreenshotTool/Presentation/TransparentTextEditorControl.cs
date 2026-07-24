using System.Drawing.Drawing2D;
using ScreenshotTool.Abstractions;
using ScreenshotTool.Editing;

namespace ScreenshotTool.Presentation;

internal sealed class TransparentTextEditorControl : Control
{
    private const int GlyphSafetyPadding = 3;
    private const int MaximumUndoHistoryCount = 100;
    public static Size ContentPadding { get; } = new(4, 3);

    private Font _editorFont;
    private readonly int _horizontalPadding;
    private readonly int _verticalPadding;
    private readonly Size _minimumSize;
    private readonly Rectangle _movementBounds;
    private readonly IClipboardService? _clipboardService;
    private readonly Func<bool>? _moveModeActive;
    private readonly Action? _altModifierUsed;
    private readonly ContextMenuStrip _textMenu;
    private readonly List<TextEditorUndoState> _undoHistory = [];
    private int _caretIndex;
    private int _selectionAnchor;
    private bool _isSelectingText;
    private bool _isMoving;
    private Point _movePointerOrigin;
    private Rectangle _moveBoundsOrigin;

    public TransparentTextEditorControl(
        Point location,
        Size minimumSize,
        Color foregroundColor,
        Rectangle movementBounds,
        IClipboardService? clipboardService = null,
        float textFontSize = TextToolSizing.DefaultFontSize,
        Size? textPadding = null,
        Func<bool>? moveModeActive = null,
        Action? altModifierUsed = null)
    {
        if (!float.IsFinite(textFontSize) || textFontSize <= 0F)
        {
            throw new ArgumentOutOfRangeException(nameof(textFontSize));
        }

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);

        _movementBounds = movementBounds.IsEmpty
            ? new Rectangle(location, minimumSize)
            : movementBounds;
        var effectivePadding = textPadding ?? ContentPadding;
        _horizontalPadding = Math.Max(0, effectivePadding.Width);
        _verticalPadding = Math.Max(0, effectivePadding.Height);
        _clipboardService = clipboardService;
        _moveModeActive = moveModeActive;
        _altModifierUsed = altModifierUsed;
        _textMenu = new ContextMenuStrip();
        _textMenu.Items.Add("剪切", null, (_, _) => CutSelectedText());
        _textMenu.Items.Add("复制", null, (_, _) => CopySelectedText());
        _textMenu.Items.Add("粘贴", null, (_, _) => PasteClipboardText());
        _textMenu.Items.Add(new ToolStripSeparator());
        _textMenu.Items.Add("全选", null, (_, _) => SelectAllText());
        _textMenu.Opening += (_, _) =>
        {
            _textMenu.Items[0].Enabled = SelectionLength > 0 && _clipboardService is not null;
            _textMenu.Items[1].Enabled = SelectionLength > 0 && _clipboardService is not null;
            _textMenu.Items[2].Enabled = _clipboardService is not null;
            _textMenu.Items[4].Enabled = Text.Length > 0;
        };
        ContextMenuStrip = _textMenu;
        _minimumSize = new Size(
            Math.Max(1, Math.Min(minimumSize.Width, _movementBounds.Width)),
            Math.Max(1, Math.Min(minimumSize.Height, _movementBounds.Height)));
        Bounds = TextEditorInteraction.Move(
            new Rectangle(location, _minimumSize),
            Point.Empty,
            _movementBounds);
        ForeColor = foregroundColor;
        BackColor = Color.Transparent;
        Cursor = Cursors.IBeam;
        ImeMode = ImeMode.On;
        TabStop = true;
        _editorFont = new Font(
            "Microsoft YaHei UI",
            textFontSize,
            FontStyle.Bold,
            GraphicsUnit.Pixel);
        Font = _editorFont;
        ResizeToContent();
    }

    public event EventHandler? CommitRequested;

    public int CaretIndex => _caretIndex;

    public int SelectionStart => Math.Min(_selectionAnchor, _caretIndex);

    public int SelectionLength => Math.Abs(_caretIndex - _selectionAnchor);

    public string SelectedText => SelectionLength == 0
        ? string.Empty
        : Text.Substring(SelectionStart, SelectionLength);

    public Rectangle TextContentBounds => Rectangle.FromLTRB(
        Left + _horizontalPadding,
        Top + _verticalPadding,
        Math.Max(Left + _horizontalPadding + 1, Right - _horizontalPadding),
        Math.Max(Top + _verticalPadding + 1, Bottom - _verticalPadding));

    public float TextFontSize => _editorFont.Size;

    public bool CanUndoTextChange => _undoHistory.Count > 0;

    public void SetTextFontSize(float textFontSize)
    {
        if (!float.IsFinite(textFontSize) || textFontSize <= 0F)
        {
            throw new ArgumentOutOfRangeException(nameof(textFontSize));
        }
        if (Math.Abs(_editorFont.Size - textFontSize) < 0.001F)
        {
            return;
        }

        var previousFont = _editorFont;
        _editorFont = new Font(
            "Microsoft YaHei UI",
            textFontSize,
            FontStyle.Bold,
            GraphicsUnit.Pixel);
        Font = _editorFont;
        previousFont.Dispose();
        if (Text.Length > 0)
        {
            var wrapped = WrapToRightBoundary(Text, _caretIndex);
            Text = wrapped.Text;
            _caretIndex = wrapped.CaretIndex;
            _selectionAnchor = _caretIndex;
        }
        ResizeToContent();
        Invalidate();
    }

    public bool IsTextFullyVisible
    {
        get
        {
            var lines = Text.Split('\n');
            var contentSize = TextContentBounds.Size;
            return lines.All(line => MeasureLineWidth(line) <= contentSize.Width) &&
                   lines.Length * Font.Height <= contentSize.Height;
        }
    }

    public void InsertText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var normalized = TextInputNormalizer.ToHalfWidthLatin(
            value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'));
        var selectionStart = SelectionStart;
        var candidate = Text
            .Remove(selectionStart, SelectionLength)
            .Insert(selectionStart, normalized);
        var candidateCaret = selectionStart + normalized.Length;
        var wrapped = WrapToRightBoundary(candidate, candidateCaret);
        if (Text == wrapped.Text &&
            _caretIndex == wrapped.CaretIndex &&
            _selectionAnchor == wrapped.CaretIndex)
        {
            return;
        }

        RecordUndoState();
        Text = wrapped.Text;
        _caretIndex = wrapped.CaretIndex;
        _selectionAnchor = _caretIndex;
        Invalidate();
    }

    public bool UndoTextChange()
    {
        if (_undoHistory.Count == 0)
        {
            return false;
        }

        var stateIndex = _undoHistory.Count - 1;
        var state = _undoHistory[stateIndex];
        _undoHistory.RemoveAt(stateIndex);
        Text = state.Text;
        _caretIndex = Math.Clamp(state.CaretIndex, 0, Text.Length);
        _selectionAnchor = Math.Clamp(state.SelectionAnchor, 0, Text.Length);
        Invalidate();
        return true;
    }

    public void SelectAllText()
    {
        _selectionAnchor = 0;
        _caretIndex = Text.Length;
        Invalidate();
    }

    public void SelectText(int start, int length)
    {
        var normalizedStart = Math.Clamp(start, 0, Text.Length);
        _selectionAnchor = normalizedStart;
        _caretIndex = Math.Clamp(normalizedStart + Math.Max(0, length), 0, Text.Length);
        Invalidate();
    }

    public void CopySelectedText() => CopySelection();

    public void CutSelectedText() => CutSelection();

    public void PasteClipboardText() => PasteText();

    protected override bool IsInputKey(Keys keyData)
    {
        var keyCode = keyData & Keys.KeyCode;
        return keyCode is Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Delete ||
               base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsAltKey(e.KeyCode))
        {
            UpdatePointerCursor();
        }

        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            if (TextEditorEnterPolicy.Resolve(e.Control) == TextEditorEnterAction.InsertLineBreak)
            {
                InsertText("\n");
            }
            else
            {
                CommitRequested?.Invoke(this, EventArgs.Empty);
            }
            return;
        }

        if (e.Control && !e.Alt)
        {
            switch (e.KeyCode)
            {
                case Keys.Z:
                    UndoTextChange();
                    e.SuppressKeyPress = true;
                    return;
                case Keys.A:
                    SelectAllText();
                    e.SuppressKeyPress = true;
                    return;
                case Keys.C:
                    CopySelection();
                    e.SuppressKeyPress = true;
                    return;
                case Keys.X:
                    CutSelection();
                    e.SuppressKeyPress = true;
                    return;
                case Keys.V:
                    PasteText();
                    e.SuppressKeyPress = true;
                    return;
            }
        }

        if (!e.Control && !e.Alt)
        {
            switch (e.KeyCode)
            {
                case Keys.Back:
                    RemoveBeforeCaret();
                    e.SuppressKeyPress = true;
                    return;
                case Keys.Delete:
                    RemoveAfterCaret();
                    e.SuppressKeyPress = true;
                    return;
                case Keys.Left:
                    MoveCaret(-1, e.Shift);
                    e.SuppressKeyPress = true;
                    return;
                case Keys.Right:
                    MoveCaret(1, e.Shift);
                    e.SuppressKeyPress = true;
                    return;
                case Keys.Home:
                    SetCaret(FindCurrentLineStart(), e.Shift);
                    e.SuppressKeyPress = true;
                    return;
                case Keys.End:
                    SetCaret(FindCurrentLineEnd(), e.Shift);
                    e.SuppressKeyPress = true;
                    return;
            }
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (IsAltKey(e.KeyCode))
        {
            UpdatePointerCursor();
        }
        base.OnKeyUp(e);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar))
        {
            InsertText(e.KeyChar.ToString());
            e.Handled = true;
            return;
        }

        base.OnKeyPress(e);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        _caretIndex = Math.Clamp(_caretIndex, 0, Text.Length);
        _selectionAnchor = Math.Clamp(_selectionAnchor, 0, Text.Length);
        base.OnTextChanged(e);
        ResizeToContent();
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (IsAltPressed())
        {
            _altModifierUsed?.Invoke();
        }

        if (TextEditorInteraction.ShouldBeginMove(e.Button, IsMoveModeActive()))
        {
            Focus();
            _isSelectingText = false;
            _isMoving = true;
            _movePointerOrigin = Cursor.Position;
            _moveBoundsOrigin = Bounds;
            Capture = true;
            Cursor = Cursors.SizeAll;
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            Focus();
            var index = FindCharacterIndex(e.Location);
            if ((ModifierKeys & Keys.Shift) != Keys.Shift)
            {
                _selectionAnchor = index;
            }
            _caretIndex = index;
            _isSelectingText = true;
            Capture = true;
            Invalidate();
            return;
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isMoving)
        {
            var pointer = Cursor.Position;
            var offset = new Point(
                pointer.X - _movePointerOrigin.X,
                pointer.Y - _movePointerOrigin.Y);
            Bounds = TextEditorInteraction.Move(_moveBoundsOrigin, offset, _movementBounds);
            Cursor = Cursors.SizeAll;
            return;
        }

        if (_isSelectingText)
        {
            _caretIndex = FindCharacterIndex(e.Location);
            Invalidate();
            return;
        }

        Cursor = IsMoveModeActive() ? Cursors.SizeAll : Cursors.IBeam;
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _isMoving)
        {
            _isMoving = false;
            Capture = false;
            Cursor = IsMoveModeActive() ? Cursors.SizeAll : Cursors.IBeam;
            return;
        }

        if (e.Button == MouseButtons.Left && _isSelectingText)
        {
            _isSelectingText = false;
            Capture = false;
            return;
        }

        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (!_isMoving)
        {
            Cursor = Cursors.IBeam;
        }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture)
        {
            _isMoving = false;
            _isSelectingText = false;
        }
        base.OnMouseCaptureChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var textBrush = new SolidBrush(ForeColor);
        using var format = CreateTextFormat();
        var contentBounds = GetContentBounds();
        DrawSelection(e.Graphics);
        var textState = e.Graphics.Save();
        try
        {
            e.Graphics.SetClip(contentBounds, CombineMode.Intersect);
            e.Graphics.DrawString(Text, Font, textBrush, contentBounds, format);
        }
        finally
        {
            e.Graphics.Restore(textState);
        }

        using var border = new Pen(Color.FromArgb(225, 56, 189, 248), 1.3F)
        {
            DashStyle = DashStyle.Dash
        };
        e.Graphics.DrawRectangle(border, 0, 0, Math.Max(1, ClientSize.Width - 1), Math.Max(1, ClientSize.Height - 1));

        if (Focused && SelectionLength == 0)
        {
            var caret = CalculateCaretLocation(e.Graphics);
            using var caretPen = new Pen(ForeColor, 1.5F);
            e.Graphics.DrawLine(caretPen, caret, new PointF(caret.X, caret.Y + Font.Height));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _textMenu.Dispose();
            _editorFont.Dispose();
        }
        base.Dispose(disposing);
    }

    private void RemoveBeforeCaret()
    {
        if (RemoveSelection())
        {
            return;
        }
        if (_caretIndex <= 0)
        {
            return;
        }

        var newCaretIndex = _caretIndex - 1;
        RecordUndoState();
        Text = Text.Remove(newCaretIndex, 1);
        _caretIndex = newCaretIndex;
        _selectionAnchor = _caretIndex;
        Invalidate();
    }

    private void RemoveAfterCaret()
    {
        if (RemoveSelection())
        {
            return;
        }
        if (_caretIndex >= Text.Length)
        {
            return;
        }

        RecordUndoState();
        Text = Text.Remove(_caretIndex, 1);
        Invalidate();
    }

    private void MoveCaret(int delta, bool extendSelection)
    {
        if (!extendSelection && SelectionLength > 0)
        {
            SetCaret(delta < 0 ? SelectionStart : SelectionStart + SelectionLength, false);
            return;
        }
        SetCaret(Math.Clamp(_caretIndex + delta, 0, Text.Length), extendSelection);
    }

    private void SetCaret(int index, bool extendSelection)
    {
        _caretIndex = Math.Clamp(index, 0, Text.Length);
        if (!extendSelection)
        {
            _selectionAnchor = _caretIndex;
        }
        Invalidate();
    }

    private int FindCurrentLineStart()
    {
        if (_caretIndex == 0)
        {
            return 0;
        }

        var newline = Text.LastIndexOf('\n', _caretIndex - 1);
        return newline < 0 ? 0 : newline + 1;
    }

    private int FindCurrentLineEnd()
    {
        var newline = Text.IndexOf('\n', _caretIndex);
        return newline < 0 ? Text.Length : newline;
    }

    private bool RemoveSelection()
    {
        if (SelectionLength == 0)
        {
            return false;
        }

        var start = SelectionStart;
        RecordUndoState();
        Text = Text.Remove(start, SelectionLength);
        _caretIndex = start;
        _selectionAnchor = start;
        Invalidate();
        return true;
    }

    private void CopySelection()
    {
        if (_clipboardService is not null && SelectionLength > 0)
        {
            _clipboardService.SetText(SelectedText);
        }
    }

    private void CutSelection()
    {
        if (_clipboardService is null || SelectionLength == 0)
        {
            return;
        }

        _clipboardService.SetText(SelectedText);
        RemoveSelection();
    }

    private void PasteText()
    {
        if (_clipboardService?.GetText() is { Length: > 0 } text)
        {
            InsertText(text);
        }
    }

    private void RecordUndoState()
    {
        if (_undoHistory.Count >= MaximumUndoHistoryCount)
        {
            _undoHistory.RemoveAt(0);
        }
        _undoHistory.Add(new TextEditorUndoState(Text, _caretIndex, _selectionAnchor));
    }

    private int FindCharacterIndex(Point point)
    {
        var lines = Text.Split('\n');
        var lineIndex = Math.Clamp(
            (point.Y - _verticalPadding) / Math.Max(1, Font.Height),
            0,
            Math.Max(0, lines.Length - 1));
        var lineStart = 0;
        for (var index = 0; index < lineIndex; index++)
        {
            lineStart += lines[index].Length + 1;
        }

        var line = lines[lineIndex];
        var relativeX = point.X - _horizontalPadding;
        if (relativeX <= 0)
        {
            return lineStart;
        }

        for (var index = 1; index <= line.Length; index++)
        {
            var previousWidth = MeasureLineWidth(line[..(index - 1)]);
            var currentWidth = MeasureLineWidth(line[..index]);
            if (relativeX < (previousWidth + currentWidth) / 2F)
            {
                return lineStart + index - 1;
            }
        }
        return lineStart + line.Length;
    }

    private void DrawSelection(Graphics graphics)
    {
        if (SelectionLength == 0)
        {
            return;
        }

        var selectionEnd = SelectionStart + SelectionLength;
        var lines = Text.Split('\n');
        var lineStart = 0;
        using var brush = new SolidBrush(Color.FromArgb(150, 51, 153, 255));
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var lineEnd = lineStart + line.Length;
            var start = Math.Max(SelectionStart, lineStart);
            var end = Math.Min(selectionEnd, lineEnd);
            if (start < end)
            {
                var prefixWidth = MeasureLineWidth(line[..(start - lineStart)]);
                var selectedWidth = MeasureLineWidth(line[(start - lineStart)..(end - lineStart)]);
                graphics.FillRectangle(
                    brush,
                    _horizontalPadding + prefixWidth,
                    _verticalPadding + lineIndex * Font.Height,
                    Math.Max(1, selectedWidth),
                    Font.Height);
            }
            lineStart = lineEnd + 1;
        }
    }

    private int MeasureLineWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        using var graphics = CreateGraphics();
        using var format = CreateMeasurementFormat();
        return (int)Math.Ceiling(graphics.MeasureString(
            text,
            Font,
            int.MaxValue,
            format).Width) + GlyphSafetyPadding;
    }

    private (string Text, int CaretIndex) WrapToRightBoundary(string text, int caretIndex)
    {
        var maximumWidth = Math.Max(
            1,
            _movementBounds.Right - Left - _horizontalPadding * 2 - GlyphSafetyPadding);
        var wrapped = new System.Text.StringBuilder(text.Length);
        var currentLine = new System.Text.StringBuilder();
        var wrappedCaret = -1;
        for (var index = 0; index < text.Length; index++)
        {
            if (index == caretIndex)
            {
                wrappedCaret = wrapped.Length;
            }

            var character = text[index];
            if (character == '\n')
            {
                wrapped.Append(character);
                currentLine.Clear();
                continue;
            }

            var candidateLine = currentLine.ToString() + character;
            if (currentLine.Length > 0 && MeasureLineWidth(candidateLine) > maximumWidth)
            {
                wrapped.Append('\n');
                currentLine.Clear();
            }
            wrapped.Append(character);
            currentLine.Append(character);
        }

        if (wrappedCaret < 0)
        {
            wrappedCaret = wrapped.Length;
        }
        return (wrapped.ToString(), wrappedCaret);
    }

    private PointF CalculateCaretLocation(Graphics graphics)
    {
        var prefix = Text[.._caretIndex].Replace("\r\n", "\n", StringComparison.Ordinal);
        var measurementText = prefix + "|";
        using var format = CreateTextFormat();
        format.SetMeasurableCharacterRanges([new CharacterRange(prefix.Length, 1)]);
        using var region = graphics.MeasureCharacterRanges(
            measurementText,
            Font,
            GetContentBounds(),
            format)[0];
        var measured = region.GetBounds(graphics);
        var x = Math.Clamp(measured.Left, _horizontalPadding, Math.Max(_horizontalPadding, ClientSize.Width - _horizontalPadding));
        var y = Math.Clamp(measured.Top, _verticalPadding, Math.Max(_verticalPadding, ClientSize.Height - Font.Height - _verticalPadding));
        return new PointF(x, y);
    }

    private void ResizeToContent()
    {
        if (Font is null || _movementBounds.IsEmpty)
        {
            return;
        }

        var lines = (string.IsNullOrEmpty(Text) ? " " : Text)
            .Split('\n');
        var textWidth = lines.Max(line => MeasureLineWidth(
            string.IsNullOrEmpty(line) ? " " : line));
        var desiredWidth = Math.Clamp(
            textWidth + _horizontalPadding * 2,
            _minimumSize.Width,
            Math.Max(_minimumSize.Width, _movementBounds.Right - Left));
        var desiredHeight = Math.Clamp(
            lines.Length * Font.Height + _verticalPadding * 2,
            _minimumSize.Height,
            _movementBounds.Height);

        Bounds = TextEditorInteraction.Move(
            new Rectangle(Location, new Size(desiredWidth, desiredHeight)),
            Point.Empty,
            _movementBounds);
    }

    private static bool IsAltPressed() => (ModifierKeys & Keys.Alt) == Keys.Alt;

    private bool IsMoveModeActive() => _moveModeActive?.Invoke() ?? IsAltPressed();

    private static bool IsAltKey(Keys keyCode) => keyCode is Keys.Menu or Keys.LMenu or Keys.RMenu;

    private void UpdatePointerCursor()
    {
        var pointer = PointToClient(Cursor.Position);
        Cursor = IsMoveModeActive() && ClientRectangle.Contains(pointer)
            ? Cursors.SizeAll
            : Cursors.IBeam;
    }

    private RectangleF GetContentBounds() => new(
        _horizontalPadding,
        _verticalPadding,
        Math.Max(1, ClientSize.Width - _horizontalPadding * 2),
        Math.Max(1, ClientSize.Height - _verticalPadding * 2));

    private static StringFormat CreateTextFormat() => new(StringFormat.GenericTypographic)
    {
        Trimming = StringTrimming.None,
        FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces
    };

    private static StringFormat CreateMeasurementFormat() => new(StringFormat.GenericTypographic)
    {
        Trimming = StringTrimming.None,
        FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces
    };

    private readonly record struct TextEditorUndoState(
        string Text,
        int CaretIndex,
        int SelectionAnchor);
}

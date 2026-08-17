using System.Runtime.InteropServices;
using ScreenshotTool.Core;
using ScreenshotTool.Presentation.Theme;

namespace ScreenshotTool.Presentation;

internal sealed class HotkeyInputBox : TextBox
{
    private HotkeyDefinition? _hotkey;

    public HotkeyInputBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        Cursor = Cursors.Hand;
        BackColor = Color.White;
        TabStop = false;
        UpdateDisplay();
    }

    public HotkeyDefinition? Hotkey
    {
        get => _hotkey;
        set
        {
            _hotkey = value;
            UpdateDisplay();
        }
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        SelectAll();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        TryApplyHotkey(e.KeyData);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (TryApplyHotkey(keyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool TryApplyHotkey(Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        if (keyData.HasFlag(Keys.Control)) modifiers |= HotkeyModifiers.Control;
        if (keyData.HasFlag(Keys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (keyData.HasFlag(Keys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (IsKeyDown(Keys.LWin) || IsKeyDown(Keys.RWin))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        var candidate = new HotkeyDefinition(modifiers, (int)key);
        if (candidate.IsValid)
        {
            Hotkey = candidate;
            return true;
        }

        return false;
    }

    private void UpdateDisplay()
    {
        Text = _hotkey?.ToDisplayText() ?? "未设置";
        ForeColor = _hotkey is null ? AppTheme.MutedText : AppTheme.Text;
        SelectionStart = 0;
        SelectionLength = 0;
    }

    private static bool IsKeyDown(Keys key) => (GetKeyState((int)key) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
}

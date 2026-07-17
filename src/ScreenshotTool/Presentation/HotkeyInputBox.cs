using System.Runtime.InteropServices;
using ScreenshotTool.Core;

namespace ScreenshotTool.Presentation;

internal sealed class HotkeyInputBox : TextBox
{
    private HotkeyDefinition _hotkey = HotkeyDefinition.Default;

    public HotkeyInputBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        Cursor = Cursors.Hand;
        BackColor = Color.White;
        TabStop = false;
        UpdateDisplay();
    }

    public HotkeyDefinition Hotkey
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

        var key = e.KeyCode;
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            return;
        }

        var modifiers = HotkeyModifiers.None;
        if (e.Control) modifiers |= HotkeyModifiers.Control;
        if (e.Shift) modifiers |= HotkeyModifiers.Shift;
        if (e.Alt) modifiers |= HotkeyModifiers.Alt;
        if (IsKeyDown(Keys.LWin) || IsKeyDown(Keys.RWin))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        var candidate = new HotkeyDefinition(modifiers, (int)key);
        if (candidate.IsValid)
        {
            Hotkey = candidate;
        }
    }

    private void UpdateDisplay()
    {
        Text = _hotkey.ToDisplayText();
        SelectionStart = 0;
        SelectionLength = 0;
    }

    private static bool IsKeyDown(Keys key) => (GetKeyState((int)key) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
}

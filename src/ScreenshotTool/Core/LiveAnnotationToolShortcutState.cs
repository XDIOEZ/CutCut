namespace ScreenshotTool.Core;

internal sealed class LiveAnnotationToolShortcutState
{
    public const int HoldThresholdMilliseconds = 350;
    private readonly HashSet<Keys> _pressedKeys = [];
    private Keys _activeKey = Keys.None;
    private long _pressedAtMilliseconds;
    private bool _pointerInteractionStarted;

    public bool IsPressed => _activeKey != Keys.None;

    public static bool IsShortcutKey(Keys keyCode) => (keyCode & Keys.KeyCode) is
        Keys.Oem3 or >= Keys.D1 and <= Keys.D6 or >= Keys.NumPad1 and <= Keys.NumPad6;

    public bool IsTracked(Keys keyCode) => _pressedKeys.Contains(keyCode & Keys.KeyCode);

    public bool TryPress(Keys keyCode, long nowMilliseconds)
    {
        keyCode &= Keys.KeyCode;
        if (!IsShortcutKey(keyCode) || !_pressedKeys.Add(keyCode))
        {
            return false;
        }

        // Track conflicting keys as well, so neither repeats nor their release
        // can accidentally activate another tool or leak into the application.
        if (IsPressed)
        {
            return false;
        }

        _activeKey = keyCode;
        _pressedAtMilliseconds = nowMilliseconds;
        _pointerInteractionStarted = false;
        return true;
    }

    public bool TryRelease(Keys keyCode, long nowMilliseconds, out bool temporary)
    {
        keyCode &= Keys.KeyCode;
        temporary = false;
        if (!_pressedKeys.Remove(keyCode) || keyCode != _activeKey)
        {
            return false;
        }

        temporary = _pointerInteractionStarted ||
            nowMilliseconds - _pressedAtMilliseconds >= HoldThresholdMilliseconds;
        _activeKey = Keys.None;
        _pressedAtMilliseconds = 0;
        _pointerInteractionStarted = false;
        return true;
    }

    public void MarkPointerInteraction()
    {
        if (IsPressed)
        {
            _pointerInteractionStarted = true;
        }
    }

    public void Reset()
    {
        _pressedKeys.Clear();
        _activeKey = Keys.None;
        _pressedAtMilliseconds = 0;
        _pointerInteractionStarted = false;
    }
}

namespace ScreenshotTool.Editing;

internal sealed class ControlDoubleTapDetector(int maximumIntervalMilliseconds = 360)
{
    private readonly int _maximumIntervalMilliseconds = Math.Max(100, maximumIntervalMilliseconds);
    private bool _isControlDown;
    private bool _currentTapEligible;
    private long _lastBareTapAt = -1;

    public bool RegisterKeyDown(Keys keyCode, long timestampMilliseconds)
    {
        if (IsControlKey(keyCode))
        {
            if (!_isControlDown)
            {
                _isControlDown = true;
                _currentTapEligible = true;
            }
            return false;
        }

        if (_isControlDown)
        {
            _currentTapEligible = false;
            _lastBareTapAt = -1;
        }
        return false;
    }

    public bool RegisterKeyUp(Keys keyCode, long timestampMilliseconds)
    {
        if (!IsControlKey(keyCode) || !_isControlDown)
        {
            return false;
        }

        _isControlDown = false;
        if (!_currentTapEligible)
        {
            _currentTapEligible = false;
            return false;
        }

        _currentTapEligible = false;
        var isDoubleTap = _lastBareTapAt >= 0 &&
            timestampMilliseconds - _lastBareTapAt <= _maximumIntervalMilliseconds;
        _lastBareTapAt = isDoubleTap ? -1 : timestampMilliseconds;
        return isDoubleTap;
    }

    public void CancelCurrentTap()
    {
        _currentTapEligible = false;
        _lastBareTapAt = -1;
    }

    private static bool IsControlKey(Keys keyCode) =>
        keyCode is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey;
}

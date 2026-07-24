using ScreenshotTool.Core;

namespace ScreenshotTool.Editing;

internal sealed class AnnotationMoveActivationState
{
    private readonly AnnotationMoveActivationMode _mode;
    private bool _altKeyDown;
    private bool _altUsedAsModifier;
    private bool _toggleActive;

    public AnnotationMoveActivationState(AnnotationMoveActivationMode mode)
    {
        _mode = Enum.IsDefined(mode)
            ? mode
            : AnnotationMoveActivationMode.HoldAlt;
    }

    public AnnotationMoveActivationMode Mode => _mode;

    public bool IsActive(bool altPhysicallyPressed) =>
        _mode == AnnotationMoveActivationMode.HoldAlt
            ? altPhysicallyPressed
            : _toggleActive;

    public void HandleAltKeyDown()
    {
        if (_altKeyDown)
        {
            return;
        }

        _altKeyDown = true;
        _altUsedAsModifier = false;
    }

    public bool HandleAltKeyUp()
    {
        if (!_altKeyDown)
        {
            return false;
        }

        _altKeyDown = false;
        if (_mode != AnnotationMoveActivationMode.ToggleOnAltTap ||
            _altUsedAsModifier)
        {
            return false;
        }

        _toggleActive = !_toggleActive;
        return true;
    }

    public void MarkAltUsedAsModifier()
    {
        if (_altKeyDown)
        {
            _altUsedAsModifier = true;
        }
    }
}

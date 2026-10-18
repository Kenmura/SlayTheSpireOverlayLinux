namespace SlayTheSpireOverlay.Core.Services;

/// <summary>Turns a held/not-held signal into a single "just pressed" pulse.</summary>
public sealed class EdgeDetector
{
    private bool _previous;

    public bool Update(bool down)
    {
        bool pressed = down && !_previous;
        _previous = down;
        return pressed;
    }
}

/// <summary>Lets polling code run at most once per engine frame. Frame 0 (outside the loop) always passes.</summary>
public sealed class FrameGate
{
    private ulong _lastFrame;

    public bool ShouldProcess(ulong frame)
    {
        if (frame != 0 && frame == _lastFrame) return false;
        _lastFrame = frame;
        return true;
    }
}

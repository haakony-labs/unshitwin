namespace UnshitWindows.Switching;

/// <summary>
/// One Alt-held cycling session over a fixed snapshot of windows in most-recently-used order.
/// Index 0 is the window that was active when the session started.
/// </summary>
internal sealed class CycleSession
{
    public CycleSession(IReadOnlyList<nint> windows)
    {
        if (windows.Count == 0) throw new ArgumentException("A session needs at least one window.", nameof(windows));
        Windows = windows;
    }

    public IReadOnlyList<nint> Windows { get; }

    public int Index { get; private set; }

    /// <summary>Moves one step forward or back (wrapping), skipping windows that are gone.
    /// Returns 0 when no other live window is left.</summary>
    public nint Step(bool forward, Func<nint, bool>? isAlive = null)
    {
        int count = Windows.Count;
        for (int i = 0; i < count - 1; i++)
        {
            Index = (Index + (forward ? 1 : count - 1)) % count;
            nint window = Windows[Index];
            if (isAlive?.Invoke(window) ?? true) return window;
        }
        return 0;
    }
}

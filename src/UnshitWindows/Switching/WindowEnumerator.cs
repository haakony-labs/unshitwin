using System.Runtime.InteropServices;
using static UnshitWindows.Native.NativeMethods;

namespace UnshitWindows.Switching;

/// <summary>Finds the windows Alt+Tab would show, and which app each belongs to.</summary>
internal static unsafe class WindowEnumerator
{
    /// <summary>
    /// <paramref name="current"/> first, followed by the other Alt+Tab-eligible windows of the same app
    /// in z-order (which is most-recently-used order).
    /// </summary>
    public static List<nint> GetSameAppWindows(nint current, bool includeMinimized)
    {
        var pathCache = new Dictionary<uint, string?>();
        var result = new List<nint> { current };
        string appKey = GetAppKey(current, pathCache);

        foreach (nint hwnd in EnumerateTopLevel())
        {
            if (hwnd == current || !IsAltTabWindow(hwnd)) continue;
            if (!includeMinimized && IsIconic(hwnd)) continue;
            if (string.Equals(GetAppKey(hwnd, pathCache), appKey, StringComparison.OrdinalIgnoreCase))
                result.Add(hwnd);
        }
        return result;
    }

    public static bool IsAltTabWindow(nint hwnd)
    {
        if (!IsWindowVisible(hwnd) || IsCloaked(hwnd)) return false;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        bool appWindow = (exStyle & WS_EX_APPWINDOW) != 0;
        if (!appWindow)
        {
            if ((exStyle & (WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE)) != 0) return false;
            if (GetWindow(hwnd, GW_OWNER) != 0) return false;
        }

        string cls = GetClassName(hwnd);
        return cls is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd");
    }

    /// <summary>
    /// Identifies the app: the full path of the owning executable. UWP apps all live in
    /// ApplicationFrameHost.exe, so for those the hosted app's process is used instead.
    /// </summary>
    private static string GetAppKey(nint hwnd, Dictionary<uint, string?> cache)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        string? path = GetPath(pid, cache);

        if (path != null && path.EndsWith(@"\ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            uint hosted = FindHostedUwpProcess(hwnd, pid);
            // A minimized UWP app detaches its content window, so it can't be identified;
            // treat it as an app of its own rather than lumping all UWP apps together.
            return hosted != 0 ? GetPath(hosted, cache) ?? $"pid:{hosted}" : $"frame:{hwnd}";
        }
        return path ?? $"pid:{pid}";
    }

    private static string? GetPath(uint pid, Dictionary<uint, string?> cache)
    {
        if (!cache.TryGetValue(pid, out string? path))
            cache[pid] = path = GetProcessImagePath(pid);
        return path;
    }

    private static List<nint> EnumerateTopLevel()
    {
        var list = new List<nint>();
        var handle = GCHandle.Alloc(list);
        try
        {
            EnumWindows(&CollectWindow, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return list;
    }

    [UnmanagedCallersOnly]
    private static int CollectWindow(nint hwnd, nint state)
    {
        ((List<nint>)GCHandle.FromIntPtr(state).Target!).Add(hwnd);
        return 1;
    }

    private sealed class UwpSearch
    {
        public uint FramePid;
        public uint FoundPid;
    }

    private static uint FindHostedUwpProcess(nint frame, uint framePid)
    {
        var search = new UwpSearch { FramePid = framePid };
        var handle = GCHandle.Alloc(search);
        try
        {
            EnumChildWindows(frame, &CheckUwpChild, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return search.FoundPid;
    }

    [UnmanagedCallersOnly]
    private static int CheckUwpChild(nint hwnd, nint state)
    {
        var search = (UwpSearch)GCHandle.FromIntPtr(state).Target!;
        if (GetClassName(hwnd) == "Windows.UI.Core.CoreWindow")
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != search.FramePid)
            {
                search.FoundPid = pid;
                return 0; // stop
            }
        }
        return 1;
    }
}

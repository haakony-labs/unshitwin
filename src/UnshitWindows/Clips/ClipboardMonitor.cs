using System.Runtime.InteropServices;
using static UnshitWindows.Native.NativeMethods;

namespace UnshitWindows.Clips;

/// <summary>Turns WM_CLIPBOARDUPDATE notifications into history entries.</summary>
internal sealed unsafe class ClipboardMonitor(ClipboardHistory history, nint ownerWindow)
{
    // Formats apps set to say "don't record this" (password managers, other clipboard tools).
    private static readonly uint FormatExclude = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
    private static readonly uint FormatCanInclude = RegisterClipboardFormat("CanIncludeInClipboardHistory");
    private static readonly uint FormatViewerIgnore = RegisterClipboardFormat("Clipboard Viewer Ignore");

    private uint _ownSequence;
    private bool _busy;
    private bool _pending;

    public bool KeepImages { get; set; } = true;
    public bool KeepFiles { get; set; } = true;

    /// <summary>Call right after this app sets the clipboard so the change isn't recorded as a new copy.</summary>
    public void MarkOwnChange() => _ownSequence = GetClipboardSequenceNumber();

    public void OnClipboardUpdate()
    {
        // Reading the clipboard can pump messages, so a second update may arrive mid-read.
        if (_busy)
        {
            _pending = true;
            return;
        }
        _busy = true;
        try
        {
            do
            {
                _pending = false;
                Capture();
            } while (_pending);
        }
        finally
        {
            _busy = false;
        }
    }

    private void Capture()
    {
        if (GetClipboardSequenceNumber() == _ownSequence) return;
        if (IsMarkedPrivate()) return;

        ClipEntry? entry;
        try
        {
            entry = Read();
        }
        catch (ExternalException)
        {
            return; // clipboard stayed locked by another app; skip this copy
        }
        if (entry != null) history.Add(entry);
    }

    private ClipEntry? Read()
    {
        if (KeepFiles && Clipboard.ContainsFileDropList())
        {
            var files = Clipboard.GetFileDropList().Cast<string>().ToArray();
            if (files.Length > 0) return new FilesEntry(files);
        }
        if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
        {
            string text = Clipboard.GetText(TextDataFormat.UnicodeText);
            if (text.Length > 0) return new TextEntry(text);
        }
        if (KeepImages && Clipboard.ContainsImage())
        {
            using var image = Clipboard.GetImage();
            if (image != null) return ImageEntry.FromImage(image);
        }
        return null;
    }

    private bool IsMarkedPrivate()
    {
        if (IsClipboardFormatAvailable(FormatExclude) || IsClipboardFormatAvailable(FormatViewerIgnore)) return true;
        if (!IsClipboardFormatAvailable(FormatCanInclude)) return false;

        // CanIncludeInClipboardHistory holds a DWORD; 0 means "keep out of history".
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(ownerWindow))
            {
                try
                {
                    nint handle = GetClipboardData(FormatCanInclude);
                    if (handle == 0) return false;
                    void* data = GlobalLock(handle);
                    if (data == null) return false;
                    try
                    {
                        return GlobalSize(handle) >= sizeof(int) && *(int*)data == 0;
                    }
                    finally
                    {
                        GlobalUnlock(handle);
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }
            Thread.Sleep(20);
        }
        return true; // couldn't check: err on the side of not recording
    }
}

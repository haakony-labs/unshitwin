using System.Collections.Specialized;
using UnshitWindows.Native;

namespace UnshitWindows.Clips;

/// <summary>Puts a history entry back on the clipboard and pastes it into the target window.</summary>
internal sealed class Paster(ClipboardMonitor monitor)
{
    /// <summary>
    /// Sets the clipboard and activates <paramref name="target"/> synchronously (before the first await),
    /// then waits for the target to be in front and for Shift/Alt/Win to be released before sending Ctrl+V.
    /// </summary>
    public async Task PasteAsync(ClipEntry entry, nint target, bool sendPaste)
    {
        SetClipboard(entry);
        if (target == 0 || !NativeMethods.IsWindow(target)) return;

        WindowActivator.Activate(target);
        if (!sendPaste) return;

        for (int i = 0; i < 50 && !WindowActivator.IsForeground(target); i++) await Task.Delay(10);
        // Held Shift would turn our Ctrl+V into Ctrl+Shift+V (our own shortcut); Alt/Win would change it too.
        for (int i = 0; i < 200 && Input.AnyDown(NativeMethods.VK_SHIFT, NativeMethods.VK_MENU, NativeMethods.VK_LWIN, NativeMethods.VK_RWIN); i++)
            await Task.Delay(10);

        // Never paste into some other window that happens to be in front.
        if (WindowActivator.IsForeground(target)) Input.SendCtrlV();
    }

    private void SetClipboard(ClipEntry entry)
    {
        var data = new DataObject();
        Bitmap? bitmap = null;
        try
        {
            switch (entry)
            {
                case TextEntry text:
                    data.SetText(text.Text, TextDataFormat.UnicodeText);
                    break;
                case FilesEntry files:
                    var list = new StringCollection();
                    list.AddRange(files.Paths.ToArray());
                    data.SetFileDropList(list);
                    break;
                case ImageEntry image:
                    using (var stream = new MemoryStream(image.Png))
                    using (var decoded = new Bitmap(stream))
                        bitmap = new Bitmap(decoded); // detach from the stream
                    data.SetImage(bitmap);
                    data.SetData("PNG", false, new MemoryStream(image.Png)); // keeps transparency for apps that read it
                    break;
            }
            Clipboard.SetDataObject(data, copy: true, retryTimes: 10, retryDelay: 50);
            monitor.MarkOwnChange();
        }
        finally
        {
            bitmap?.Dispose();
        }
    }
}

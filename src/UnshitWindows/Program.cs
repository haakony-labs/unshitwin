namespace UnshitWindows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\UnshitWindows.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            // Already running (it lives in the tray): launching it again opens its Settings.
            MessageWindow.SignalRunningInstance();
            return;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
    }
}

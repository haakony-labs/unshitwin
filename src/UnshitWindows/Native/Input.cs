using static UnshitWindows.Native.NativeMethods;

namespace UnshitWindows.Native;

/// <summary>Physical key state and synthetic keystrokes.</summary>
internal static unsafe class Input
{
    /// <summary>An unassigned virtual key. Tapping it while Alt is held keeps the
    /// Alt release from opening the menu bar of the window we just activated.</summary>
    private const ushort VK_MENU_MASK = 0xE8;
    private const ushort VK_V = 0x56;

    public static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    public static bool AnyDown(params ReadOnlySpan<int> vks)
    {
        foreach (int vk in vks)
            if (IsDown(vk)) return true;
        return false;
    }

    public static void TapMenuMaskKey() =>
        Send([(VK_MENU_MASK, false), (VK_MENU_MASK, true)]);

    public static void SendCtrlV() =>
        Send([(VK_CONTROL, false), (VK_V, false), (VK_V, true), (VK_CONTROL, true)]);

    private static void Send(ReadOnlySpan<(ushort Vk, bool Up)> keys)
    {
        INPUT* inputs = stackalloc INPUT[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            inputs[i] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = keys[i].Vk,
                        wScan = (ushort)MapVirtualKey(keys[i].Vk, MAPVK_VK_TO_VSC),
                        dwFlags = keys[i].Up ? KEYEVENTF_KEYUP : 0,
                    },
                },
            };
        }
        SendInput((uint)keys.Length, inputs, sizeof(INPUT));
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace PasswordVault.App.Helpers;

/// <summary>
/// Symbol im Infobereich (System-Tray) über Shell_NotifyIcon.
/// Nachrichten kommen über eine Subclass des Hauptfensters an – das funktioniert
/// auch, während das Fenster ausgeblendet ist.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    public sealed record MenuItem(string Text, Action OnClick, bool Checked = false, bool Enabled = true)
    {
        public static readonly MenuItem Separator = new("-", () => { });
    }

    private const uint WM_APP_TRAY = 0x8000 + 0x51;
    private const uint WM_SIZE = 0x0005;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_CONTEXTMENU = 0x007B;
    private const uint NIN_SELECT = 0x0400;
    private const uint NIN_KEYSELECT = 0x0401;
    private const int SIZE_MINIMIZED = 1;

    private readonly IntPtr _hwnd;
    private readonly uint _taskbarCreatedMsg;
    private readonly SubclassProc _proc; // Referenz halten, sonst sammelt der GC den Delegate ein
    private IntPtr _hIcon;
    private string _tooltip;
    private bool _added;

    public event Action? Activated;
    public event Func<IReadOnlyList<MenuItem>>? MenuRequested;
    public event Action? Minimized;

    public TrayIcon(IntPtr hwnd, string iconPath, string tooltip)
    {
        _hwnd = hwnd;
        _tooltip = tooltip;
        _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");
        _hIcon = File.Exists(iconPath)
            ? LoadImage(IntPtr.Zero, iconPath, 1 /*IMAGE_ICON*/,
                GetSystemMetrics(49 /*SM_CXSMICON*/), GetSystemMetrics(50 /*SM_CYSMICON*/), 0x10 /*LR_LOADFROMFILE*/)
            : IntPtr.Zero;
        _proc = WndProc;
        SetWindowSubclass(_hwnd, _proc, (UIntPtr)0x5056, UIntPtr.Zero);
    }

    public void Show()
    {
        if (_added) return;
        var data = CreateData(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        _added = Shell_NotifyIcon(NIM_ADD, ref data);
        data.uVersion = 4; // NOTIFYICON_VERSION_4
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
    }

    public void Hide()
    {
        if (!_added) return;
        var data = CreateData(0);
        Shell_NotifyIcon(NIM_DELETE, ref data);
        _added = false;
    }

    public void SetTooltip(string text)
    {
        _tooltip = text;
        if (!_added) return;
        var data = CreateData(NIF_TIP | NIF_SHOWTIP);
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    /// <summary>Kurze Sprechblase am Tray-Symbol.</summary>
    public void ShowBalloon(string title, string text)
    {
        if (!_added) return;
        var data = CreateData(NIF_INFO);
        data.szInfoTitle = title;
        data.szInfo = text;
        data.dwInfoFlags = 0x4 /*NIIF_USER*/ | 0x20 /*NIIF_LARGE_ICON*/;
        data.hBalloonIcon = _hIcon;
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    private NOTIFYICONDATAW CreateData(uint flags) => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd = _hwnd,
        uID = 1,
        uFlags = flags,
        uCallbackMessage = WM_APP_TRAY,
        hIcon = _hIcon,
        szTip = _tooltip.Length > 127 ? _tooltip[..127] : _tooltip,
        szInfo = "",
        szInfoTitle = ""
    };

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr refData)
    {
        if (msg == WM_APP_TRAY)
        {
            // NOTIFYICON_VERSION_4: Ereignis steht im unteren Wort von lParam
            uint evt = (uint)(lParam.ToInt64() & 0xFFFF);
            switch (evt)
            {
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                case NIN_SELECT:
                case NIN_KEYSELECT:
                    Activated?.Invoke();
                    break;
                case WM_CONTEXTMENU:
                case WM_RBUTTONUP:
                    ShowMenu();
                    break;
            }
            return IntPtr.Zero;
        }

        if (msg == _taskbarCreatedMsg && _added)
        {
            // Explorer wurde neu gestartet → Symbol neu anlegen
            _added = false;
            Show();
        }

        if (msg == WM_SIZE && wParam.ToInt64() == SIZE_MINIMIZED)
            Minimized?.Invoke();

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        var items = MenuRequested?.Invoke();
        if (items == null || items.Count == 0) return;

        var menu = CreatePopupMenu();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Text == "-")
            {
                AppendMenu(menu, 0x800 /*MF_SEPARATOR*/, UIntPtr.Zero, null);
                continue;
            }
            uint flags = 0; // MF_STRING
            if (item.Checked) flags |= 0x8;   // MF_CHECKED
            if (!item.Enabled) flags |= 0x3;  // MF_GRAYED | MF_DISABLED
            AppendMenu(menu, flags, (UIntPtr)(i + 1), item.Text);
        }

        GetCursorPos(out var pt);
        // Ohne SetForegroundWindow schließt sich das Menü nicht beim Klick daneben
        SetForegroundWindow(_hwnd);
        var cmd = TrackPopupMenuEx(menu, 0x0100 /*TPM_RETURNCMD*/ | 0x0002 /*TPM_RIGHTBUTTON*/ | 0x0020 /*TPM_BOTTOMALIGN*/,
            pt.X, pt.Y, _hwnd, IntPtr.Zero);
        PostMessage(_hwnd, 0 /*WM_NULL*/, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(menu);

        if (cmd > 0 && cmd <= items.Count && items[cmd - 1].Enabled)
            items[cmd - 1].OnClick();
    }

    public void Dispose()
    {
        Hide();
        RemoveWindowSubclass(_hwnd, _proc, (UIntPtr)0x5056);
        if (_hIcon != IntPtr.Zero) DestroyIcon(_hIcon);
        _hIcon = IntPtr.Zero;
    }

    // ================= Win32 =================

    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_INFO = 0x10, NIF_SHOWTIP = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr refData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    private static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATAW data);

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc proc, UIntPtr id, UIntPtr refData);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc proc, UIntPtr id);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string? text);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr tpm);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
}

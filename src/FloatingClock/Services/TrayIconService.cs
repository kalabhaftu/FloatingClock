using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using FloatingClock.Models;
using AppThemeMode = FloatingClock.Models.ThemeMode;

namespace FloatingClock.Services;

public sealed class TrayIconService : IDisposable
{
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIF_INFO = 0x00000010;

    private const int WM_USER = 0x0400;
    public const int WM_TRAYICON = WM_USER + 101;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int ExtractIconEx(string szFileName, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, int nIcons);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private readonly MainWindow _mainWindow;
    private readonly IntPtr _hwnd;
    private readonly HwndSource? _hwndSource;
    private readonly uint _wmTaskbarCreated;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isAdded;
    private ContextMenu? _trayContextMenu;

    public TrayIconService(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        _hwnd = new WindowInteropHelper(mainWindow).EnsureHandle();
        _wmTaskbarCreated = RegisterWindowMessage("TaskbarCreated");

        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);

        ExtractAppIcon();
        AddTrayIcon();
        BuildContextMenu();
    }

    private void ExtractAppIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                ExtractIconEx(exePath, 0, out _, out _hIcon, 1);
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Failed to extract icon for system tray");
        }
    }

    public void AddTrayIcon()
    {
        if (_isAdded) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "Floating Clock"
        };

        if (Shell_NotifyIcon(NIM_ADD, ref nid))
        {
            _isAdded = true;
            ErrorLogger.LogInfo("System tray icon added successfully.");
        }
    }

    public void UpdateTooltip(string tooltip)
    {
        if (!_isAdded) return;

        if (tooltip.Length >= 127)
            tooltip = tooltip[..124] + "…";

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_TIP,
            szTip = tooltip
        };

        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    public void RemoveTrayIcon()
    {
        if (!_isAdded) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1
        };

        Shell_NotifyIcon(NIM_DELETE, ref nid);
        _isAdded = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)_wmTaskbarCreated)
        {
            // Windows Explorer restarted; re-add the notification icon
            _isAdded = false;
            AddTrayIcon();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == WM_TRAYICON)
        {
            int eventId = lParam.ToInt32();
            switch (eventId)
            {
                case WM_LBUTTONUP:
                    OnTrayLeftClick();
                    handled = true;
                    break;

                case WM_LBUTTONDBLCLK:
                    _mainWindow.RestoreAndActivate();
                    handled = true;
                    break;

                case WM_RBUTTONUP:
                    ShowTrayContextMenu();
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }

    private void OnTrayLeftClick()
    {
        // Toggle visibility or bring to front
        if (_mainWindow.Visibility == Visibility.Visible && _mainWindow.WindowState != WindowState.Minimized && _mainWindow.IsActive)
        {
            _mainWindow.MinimizeToTray();
        }
        else
        {
            _mainWindow.RestoreAndActivate();
        }
    }

    public void BuildContextMenu()
    {
        var menu = new ContextMenu();

        var toggleShowItem = new MenuItem { Header = "Show Clock" };
        toggleShowItem.Click += (_, _) =>
        {
            if (_mainWindow.Visibility == Visibility.Visible && _mainWindow.WindowState != WindowState.Minimized)
                _mainWindow.MinimizeToTray();
            else
                _mainWindow.RestoreAndActivate();
        };

        var alwaysOnTopItem = new MenuItem
        {
            Header = "Always On Top",
            IsCheckable = true,
            IsChecked = _mainWindow.IsAlwaysOnTop
        };
        alwaysOnTopItem.Click += (_, _) => _mainWindow.ToggleAlwaysOnTop();

        var showInTaskbarItem = new MenuItem
        {
            Header = "Show in Taskbar",
            IsCheckable = true,
            IsChecked = _mainWindow.IsShowInTaskbar
        };
        showInTaskbarItem.Click += (_, _) => _mainWindow.ToggleShowInTaskbar();

        menu.Items.Add(toggleShowItem);
        menu.Items.Add(alwaysOnTopItem);
        menu.Items.Add(showInTaskbarItem);
        menu.Items.Add(new Separator());

        // Theme Submenu
        var themeMenu = new MenuItem { Header = "Theme" };
        var darkItem = new MenuItem { Header = "Dark", Tag = "Dark" };
        var lightItem = new MenuItem { Header = "Light", Tag = "Light" };
        var systemItem = new MenuItem { Header = "System", Tag = "System" };

        darkItem.Click += (s, e) => _mainWindow.ChangeTheme(AppThemeMode.Dark);
        lightItem.Click += (s, e) => _mainWindow.ChangeTheme(AppThemeMode.Light);
        systemItem.Click += (s, e) => _mainWindow.ChangeTheme(AppThemeMode.System);

        themeMenu.Items.Add(darkItem);
        themeMenu.Items.Add(lightItem);
        themeMenu.Items.Add(systemItem);
        menu.Items.Add(themeMenu);

        // Format Submenu
        var formatMenu = new MenuItem { Header = "Format" };
        var twelveItem = new MenuItem { Header = "12 Hour" };
        var twentyFourItem = new MenuItem { Header = "24 Hour" };
        twelveItem.Click += (s, e) => _mainWindow.ChangeFormat(TimeFormatMode.TwelveHour);
        twentyFourItem.Click += (s, e) => _mainWindow.ChangeFormat(TimeFormatMode.TwentyFourHour);
        formatMenu.Items.Add(twelveItem);
        formatMenu.Items.Add(twentyFourItem);
        menu.Items.Add(formatMenu);

        menu.Items.Add(new Separator());

        var timezonesItem = new MenuItem { Header = "Timezones…" };
        timezonesItem.Click += (s, e) => _mainWindow.OpenTimezonesDialog();
        menu.Items.Add(timezonesItem);

        var logItem = new MenuItem { Header = "Open Log Folder" };
        logItem.Click += (s, e) => _mainWindow.OpenLogFolder();
        menu.Items.Add(logItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(exitItem);

        _trayContextMenu = menu;
    }

    private void ShowTrayContextMenu()
    {
        if (_trayContextMenu == null) return;

        // Update checked states dynamically before showing
        if (_trayContextMenu.Items.Count > 0)
        {
            if (_trayContextMenu.Items[0] is MenuItem showHideItem)
            {
                showHideItem.Header = (_mainWindow.Visibility == Visibility.Visible && _mainWindow.WindowState != WindowState.Minimized)
                    ? "Hide Clock"
                    : "Show Clock";
            }
            if (_trayContextMenu.Items[1] is MenuItem onTopItem)
            {
                onTopItem.IsChecked = _mainWindow.IsAlwaysOnTop;
            }
            if (_trayContextMenu.Items[2] is MenuItem taskbarItem)
            {
                taskbarItem.IsChecked = _mainWindow.IsShowInTaskbar;
            }

            // Update theme checks
            if (_trayContextMenu.Items[4] is MenuItem themeMenu)
            {
                foreach (var sub in themeMenu.Items)
                {
                    if (sub is MenuItem mi && mi.Tag is string tag)
                    {
                        mi.IsChecked = tag switch
                        {
                            "Dark" => _mainWindow.CurrentTheme == AppThemeMode.Dark,
                            "Light" => _mainWindow.CurrentTheme == AppThemeMode.Light,
                            "System" => _mainWindow.CurrentTheme == AppThemeMode.System,
                            _ => false
                        };
                    }
                }
            }

            // Update format checks
            if (_trayContextMenu.Items[5] is MenuItem formatMenu)
            {
                if (formatMenu.Items[0] is MenuItem twelve)
                    twelve.IsChecked = _mainWindow.CurrentFormat == TimeFormatMode.TwelveHour;
                if (formatMenu.Items[1] is MenuItem twentyFour)
                    twentyFour.IsChecked = _mainWindow.CurrentFormat == TimeFormatMode.TwentyFourHour;
            }
        }

        SetForegroundWindow(_hwnd);
        _trayContextMenu.Placement = PlacementMode.MousePoint;
        _trayContextMenu.IsOpen = true;
    }

    public void Dispose()
    {
        RemoveTrayIcon();
        _hwndSource?.RemoveHook(WndProc);

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }
}

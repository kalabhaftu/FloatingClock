using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FloatingClock.Models;
using FloatingClock.Services;
using Microsoft.Win32;
using AppThemeMode = FloatingClock.Models.ThemeMode;

namespace FloatingClock;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private AppSettings _settings;
    private string _lastHours   = "";
    private string _lastMinutes = "";
    private int _currentTzIndex;
    private TrayIconService? _trayService;

    public bool IsAlwaysOnTop => _settings.AlwaysOnTop;
    public bool IsShowInTaskbar => _settings.ShowInTaskbar;
    public AppThemeMode CurrentTheme => _settings.Theme;
    public TimeFormatMode CurrentFormat => _settings.TimeFormat;

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        ApplySettings();

        // 100 ms tick → accurate second transitions, negligible CPU
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => UpdateClock();
        _timer.Start();

        UpdateClock();

        MouseWheel += OnMouseWheel;
        Closing    += OnClosing;
        Loaded     += (_, _) => ErrorLogger.LogInfo("Main window loaded.");

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            _trayService = new TrayIconService(this);
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Failed to initialize tray icon service");
        }
    }

    // ──────────── settings & display ────────────

    private void ApplySettings()
    {
        Topmost = _settings.AlwaysOnTop;
        AlwaysOnTopMenuItem.IsChecked = _settings.AlwaysOnTop;
        ShowInTaskbar = _settings.ShowInTaskbar;
        ShowInTaskbarMenuItem.IsChecked = _settings.ShowInTaskbar;
        UpdatePinVisual();

        double width  = Math.Max(MinWidth, _settings.WindowWidth);
        double height = Math.Max(MinHeight, _settings.WindowHeight);
        double left   = _settings.WindowLeft;
        double top    = _settings.WindowTop;

        // Multi-monitor position validation: detect disconnected monitors and reposition safely
        if (ScreenHelper.EnsureVisible(ref left, ref top, width, height))
        {
            _settings.WindowLeft = left;
            _settings.WindowTop = top;
            _settings.Save();
        }

        Left = left;
        Top = top;
        Width = width;
        Height = height;

        TwelveHourMenuItem.IsChecked     = _settings.TimeFormat == TimeFormatMode.TwelveHour;
        TwentyFourHourMenuItem.IsChecked = _settings.TimeFormat == TimeFormatMode.TwentyFourHour;

        DarkThemeMenuItem.IsChecked   = _settings.Theme == AppThemeMode.Dark;
        LightThemeMenuItem.IsChecked  = _settings.Theme == AppThemeMode.Light;
        SystemThemeMenuItem.IsChecked = _settings.Theme == AppThemeMode.System;

        ThemeManager.ApplyTheme(_settings.Theme);

        _currentTzIndex = Math.Clamp(
            _settings.SelectedTimezoneIndex, 0,
            Math.Max(0, _settings.Timezones.Count - 1));
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                double left = Left;
                double top = Top;
                double width = ActualWidth > 0 ? ActualWidth : Width;
                double height = ActualHeight > 0 ? ActualHeight : Height;

                if (ScreenHelper.EnsureVisible(ref left, ref top, width, height))
                {
                    Left = left;
                    Top = top;
                    _settings.WindowLeft = left;
                    _settings.WindowTop = top;
                    _settings.Save();
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, "Display settings change handling failed");
            }
        });
    }

    private void UpdatePinVisual()
    {
        if (PinPath == null) return;
        PinPath.Fill = _settings.AlwaysOnTop
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("InfoForeground");
    }

    // ──────────── clock update ────────────

    private void UpdateClock()
    {
        try
        {
            DateTime now;
            string tzLabel = "";

            if (_settings.Timezones.Count > 0 && _currentTzIndex < _settings.Timezones.Count)
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(_settings.Timezones[_currentTzIndex]);
                now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
                if (tz.Id != TimeZoneInfo.Local.Id || _settings.Timezones.Count > 1)
                {
                    tzLabel = TimezoneHelper.Format(tz);
                }
            }
            else
            {
                now = DateTime.Now;
            }

            // Hours
            string hours, amPm = "";
            if (_settings.TimeFormat == TimeFormatMode.TwelveHour)
            {
                int h = now.Hour % 12;
                if (h == 0) h = 12;
                hours = h.ToString("D2");
                amPm  = now.Hour >= 12 ? "PM" : "AM";
            }
            else
            {
                hours = now.Hour.ToString("D2");
            }

            string minutes = now.Minute.ToString("D2");

            if (hours != _lastHours)   { HoursCard.SetValue(hours);     _lastHours   = hours;   }
            if (minutes != _lastMinutes) { MinutesCard.SetValue(minutes); _lastMinutes = minutes; }

            SecondsText.Text  = now.Second.ToString("D2");
            DateText.Text     = now.ToString("M/dd/yy");
            DayText.Text      = now.ToString("dddd");
            AmPmText.Text     = amPm;
            AmPmText.Visibility = _settings.TimeFormat == TimeFormatMode.TwelveHour
                                      ? Visibility.Visible : Visibility.Collapsed;
            TimezoneText.Text = tzLabel;

            // Update tray tooltip
            var tip = string.IsNullOrEmpty(tzLabel)
                ? $"Floating Clock - {hours}:{minutes} {amPm}".Trim()
                : $"Floating Clock - {hours}:{minutes} {amPm} ({tzLabel})".Trim();
            _trayService?.UpdateTooltip(tip);
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Clock update failed");
        }
    }

    // ──────────── window interactions ────────────

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep)
        {
            // Do not drag if user clicked an action button inside the top bar
            if (FindVisualParent<System.Windows.Controls.Button>(dep) != null)
                return;
        }

        if (e.ClickCount == 2)
        {
            ToggleAlwaysOnTop();
        }
        else
        {
            DragMove();
        }
    }

    public void ToggleAlwaysOnTop()
    {
        _settings.AlwaysOnTop = !_settings.AlwaysOnTop;
        Topmost = _settings.AlwaysOnTop;
        AlwaysOnTopMenuItem.IsChecked = _settings.AlwaysOnTop;
        UpdatePinVisual();
        _settings.Save();
    }

    public void ToggleShowInTaskbar()
    {
        _settings.ShowInTaskbar = !_settings.ShowInTaskbar;
        ShowInTaskbar = _settings.ShowInTaskbar;
        ShowInTaskbarMenuItem.IsChecked = _settings.ShowInTaskbar;
        _settings.Save();
    }

    public void ChangeTheme(AppThemeMode mode)
    {
        _settings.Theme = mode;
        DarkThemeMenuItem.IsChecked   = _settings.Theme == AppThemeMode.Dark;
        LightThemeMenuItem.IsChecked  = _settings.Theme == AppThemeMode.Light;
        SystemThemeMenuItem.IsChecked = _settings.Theme == AppThemeMode.System;
        ThemeManager.ApplyTheme(_settings.Theme);
        UpdatePinVisual();
        _settings.Save();
    }

    public void ChangeFormat(TimeFormatMode mode)
    {
        _settings.TimeFormat = mode;
        TwelveHourMenuItem.IsChecked     = _settings.TimeFormat == TimeFormatMode.TwelveHour;
        TwentyFourHourMenuItem.IsChecked = _settings.TimeFormat == TimeFormatMode.TwentyFourHour;
        _lastHours = "";
        UpdateClock();
        _settings.Save();
    }

    public void OpenTimezonesDialog()
    {
        var win = new Windows.SettingsWindow(_settings) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _settings = win.Settings;
            _currentTzIndex = Math.Clamp(_settings.SelectedTimezoneIndex, 0,
                Math.Max(0, _settings.Timezones.Count - 1));
            _lastHours = _lastMinutes = "";
            ApplySettings();
            UpdateClock();
            _settings.Save();
        }
    }

    public void OpenLogFolder()
    {
        try { Process.Start("explorer.exe", ErrorLogger.GetLogDirectory()); }
        catch (Exception ex) { ErrorLogger.LogError(ex, "Open log folder failed"); }
    }

    public void RestoreAndActivate()
    {
        if (Visibility != Visibility.Visible)
            Visibility = Visibility.Visible;

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        // Ensure visible in case monitor was disconnected while minimized
        double left = Left;
        double top = Top;
        double width = ActualWidth > 0 ? ActualWidth : Width;
        double height = ActualHeight > 0 ? ActualHeight : Height;
        if (ScreenHelper.EnsureVisible(ref left, ref top, width, height))
        {
            Left = left;
            Top = top;
            _settings.WindowLeft = left;
            _settings.WindowTop = top;
            _settings.Save();
        }

        Activate();
        Topmost = _settings.AlwaysOnTop;
    }

    public void MinimizeToTray()
    {
        Visibility = Visibility.Hidden;
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleAlwaysOnTop();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        MinimizeToTray();
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_settings.Timezones.Count <= 1) return;
        _currentTzIndex = (e.Delta > 0)
            ? (_currentTzIndex + 1) % _settings.Timezones.Count
            : (_currentTzIndex - 1 + _settings.Timezones.Count) % _settings.Timezones.Count;
        _settings.SelectedTimezoneIndex = _currentTzIndex;
        _settings.Save();
        _lastHours = _lastMinutes = "";
        UpdateClock();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _timer.Stop();
        _trayService?.Dispose();

        _settings.WindowLeft   = Left;
        _settings.WindowTop    = Top;
        _settings.WindowWidth  = Width;
        _settings.WindowHeight = Height;
        _settings.SelectedTimezoneIndex = _currentTzIndex;
        _settings.Save();
    }

    // ──────────── context-menu handlers ────────────

    private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
    {
        ToggleAlwaysOnTop();
    }

    private void ShowInTaskbar_Click(object sender, RoutedEventArgs e)
    {
        ToggleShowInTaskbar();
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Tag: string tag }) return;
        var mode = tag switch
        {
            "Light"  => AppThemeMode.Light,
            "System" => AppThemeMode.System,
            _        => AppThemeMode.Dark
        };
        ChangeTheme(mode);
    }

    private void Format_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Tag: string tag }) return;
        var mode = tag == "Twelve" ? TimeFormatMode.TwelveHour : TimeFormatMode.TwentyFourHour;
        ChangeFormat(mode);
    }

    private void Timezones_Click(object sender, RoutedEventArgs e)
    {
        OpenTimezonesDialog();
    }

    private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
    {
        MinimizeToTray();
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        OpenLogFolder();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parentObj = VisualTreeHelper.GetParent(child);
        if (parentObj == null) return null;
        if (parentObj is T parent) return parent;
        return FindVisualParent<T>(parentObj);
    }
}

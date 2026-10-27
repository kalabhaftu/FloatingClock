using System;
using System.Windows;
using Microsoft.Win32;
using AppThemeMode = FloatingClock.Models.ThemeMode;

namespace FloatingClock.Services;

public static class ThemeManager
{
    public static void ApplyTheme(AppThemeMode mode)
    {
        var resolved = mode;
        if (mode == AppThemeMode.System)
            resolved = IsSystemDarkTheme() ? AppThemeMode.Dark : AppThemeMode.Light;

        var themeUri = resolved == AppThemeMode.Dark
            ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
            : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

        var app = Application.Current;
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("Themes/ControlStyles.xaml", UriKind.Relative)
        });

        ErrorLogger.LogInfo($"Theme applied: {mode} → {resolved}");
    }

    private static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            return val is int i && i == 0;
        }
        catch { return true; }
    }
}

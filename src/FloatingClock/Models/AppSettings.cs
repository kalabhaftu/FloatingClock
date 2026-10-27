using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FloatingClock.Services;

namespace FloatingClock.Models;

public enum ThemeMode { Dark, Light, System }
public enum TimeFormatMode { TwelveHour, TwentyFourHour }

public class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public TimeFormatMode TimeFormat { get; set; } = TimeFormatMode.TwelveHour;
    public bool AlwaysOnTop { get; set; } = true;
    public List<string> Timezones { get; set; } = new() { TimeZoneInfo.Local.Id };
    public int SelectedTimezoneIndex { get; set; }
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 700;
    public double WindowHeight { get; set; } = 350;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FloatingClock", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Failed to load settings");
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Failed to save settings");
        }
    }
}

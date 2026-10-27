using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FloatingClock.Models;
using FloatingClock.Services;

namespace FloatingClock.Windows;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; private set; }
    private readonly ObservableCollection<TimezoneItem> _configuredTimezones;
    private int _selectedIndex;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        // Deep-copy so Cancel discards changes
        Settings = new AppSettings
        {
            Theme                 = settings.Theme,
            TimeFormat            = settings.TimeFormat,
            AlwaysOnTop           = settings.AlwaysOnTop,
            Timezones             = new(settings.Timezones),
            SelectedTimezoneIndex = settings.SelectedTimezoneIndex,
            WindowLeft            = settings.WindowLeft,
            WindowTop             = settings.WindowTop,
            WindowWidth           = settings.WindowWidth,
            WindowHeight          = settings.WindowHeight
        };

        _selectedIndex = Math.Clamp(Settings.SelectedTimezoneIndex, 0, Math.Max(0, Settings.Timezones.Count - 1));

        _configuredTimezones = new ObservableCollection<TimezoneItem>(
            Settings.Timezones.Select(id => new TimezoneItem(id)));
        TimezoneListBox.ItemsSource = _configuredTimezones;

        if (_configuredTimezones.Count > _selectedIndex)
            TimezoneListBox.SelectedIndex = _selectedIndex;

        // Populate available system timezones with friendly formatted items
        var allZones = TimeZoneInfo.GetSystemTimeZones()
            .Select(tz => new TimezoneItem(tz))
            .OrderBy(item => item.Display)
            .ToList();

        TimezoneCombo.ItemsSource = allZones;
        if (allZones.Count > 0)
        {
            // Try selecting local timezone by default in combo
            var localItem = allZones.FirstOrDefault(z => z.Id == TimeZoneInfo.Local.Id);
            TimezoneCombo.SelectedItem = localItem ?? allZones[0];
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep)
        {
            if (FindVisualParent<System.Windows.Controls.Primitives.ButtonBase>(dep) != null ||
                FindVisualParent<System.Windows.Controls.ListBox>(dep) != null ||
                FindVisualParent<System.Windows.Controls.ComboBox>(dep) != null)
                return;
        }
        DragMove();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (TimezoneCombo.SelectedItem is not TimezoneItem selected) return;

        if (!_configuredTimezones.Any(z => z.Id.Equals(selected.Id, StringComparison.OrdinalIgnoreCase)))
        {
            var newItem = new TimezoneItem(selected.Id);
            _configuredTimezones.Add(newItem);
            TimezoneListBox.SelectedItem = newItem;
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (TimezoneListBox.SelectedItem is not TimezoneItem selected) return;

        if (_configuredTimezones.Count <= 1)
        {
            MessageBox.Show("You must keep at least one timezone configured.", "Floating Clock",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var idx = _configuredTimezones.IndexOf(selected);
        _configuredTimezones.Remove(selected);

        _selectedIndex = Math.Clamp(_selectedIndex, 0, _configuredTimezones.Count - 1);
        TimezoneListBox.SelectedIndex = Math.Clamp(idx, 0, _configuredTimezones.Count - 1);
    }

    private void SetActive_Click(object sender, RoutedEventArgs e)
    {
        if (TimezoneListBox.SelectedIndex >= 0)
        {
            _selectedIndex = TimezoneListBox.SelectedIndex;
            MessageBox.Show($"Active timezone set to: {_configuredTimezones[_selectedIndex].Display}",
                "Floating Clock", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void TimezoneListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TimezoneListBox.SelectedIndex >= 0)
        {
            _selectedIndex = TimezoneListBox.SelectedIndex;
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (_configuredTimezones.Count == 0)
            _configuredTimezones.Add(new TimezoneItem(TimeZoneInfo.Local.Id));

        Settings.Timezones = _configuredTimezones.Select(z => z.Id).ToList();
        Settings.SelectedTimezoneIndex = Math.Clamp(_selectedIndex, 0, Settings.Timezones.Count - 1);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parentObj = VisualTreeHelper.GetParent(child);
        if (parentObj == null) return null;
        if (parentObj is T parent) return parent;
        return FindVisualParent<T>(parentObj);
    }
}

using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Northpad.Core.Storage;

namespace Northpad.App.Themes;

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

public interface IThemeService : IDisposable
{
    ThemePreference Preference { get; }

    void Initialize();

    void Set(ThemePreference preference);
}

public sealed class ThemeManager : IThemeService
{
    public const string SettingKey = "theme";

    private readonly ISettingsStore _settings;

    public ThemeManager(ISettingsStore settings)
    {
        _settings = settings;
    }

    public ThemePreference Preference { get; private set; } = ThemePreference.System;

    public void Initialize()
    {
        Preference = _settings.Get(SettingKey) switch
        {
            "light" => ThemePreference.Light,
            "dark" => ThemePreference.Dark,
            _ => ThemePreference.System,
        };
        Apply();
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
    }

    public void Set(ThemePreference preference)
    {
        Preference = preference;
        _settings.Set(SettingKey, preference switch
        {
            ThemePreference.Light => "light",
            ThemePreference.Dark => "dark",
            _ => "system",
        });
        Apply();
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
    }

    private void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color))
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.Invoke(Apply);
    }

    private void Apply()
    {
        if (SystemParameters.HighContrast)
        {
            Set("Brush.Window", SystemColors.WindowColor);
            Set("Brush.Sidebar", SystemColors.ControlColor);
            Set("Brush.Surface", SystemColors.WindowColor);
            Set("Brush.Text", SystemColors.WindowTextColor);
            Set("Brush.Muted", SystemColors.GrayTextColor);
            Set("Brush.Border", SystemColors.ControlDarkColor);
            Set("Brush.Hover", SystemColors.ControlLightColor);
            Set("Brush.Active", SystemColors.HighlightColor);
            Set("Brush.Accent", SystemColors.HighlightColor);
            Set("Brush.AccentText", SystemColors.HighlightTextColor);
            Set("Brush.AccentHover", SystemColors.HighlightColor);
            Set("Brush.Input", SystemColors.WindowColor);
            Set("Brush.Danger", SystemColors.WindowTextColor);
            return;
        }

        var dark = Preference switch
        {
            ThemePreference.Dark => true,
            ThemePreference.Light => false,
            _ => !IsSystemLight(),
        };
        ApplyPalette(dark);
    }

    private static void ApplyPalette(bool dark)
    {
        if (!dark)
        {
            Set("Brush.Window", "#FFFFFF");
            Set("Brush.Sidebar", "#F5F5F5");
            Set("Brush.Surface", "#FFFFFF");
            Set("Brush.Text", "#1A1A1A");
            Set("Brush.Muted", "#5E5E5E");
            Set("Brush.Border", "#E4E4E4");
            Set("Brush.Hover", "#EFEFEF");
            Set("Brush.Active", "#E6E6E6");
            Set("Brush.Accent", "#1A1A1A");
            Set("Brush.AccentText", "#FFFFFF");
            Set("Brush.AccentHover", "#333333");
            Set("Brush.Input", "#FFFFFF");
            Set("Brush.Danger", "#8C2F2F");
            return;
        }

        Set("Brush.Window", "#101010");
        Set("Brush.Sidebar", "#0C0C0C");
        Set("Brush.Surface", "#161616");
        Set("Brush.Text", "#F2F2F2");
        Set("Brush.Muted", "#A3A3A3");
        Set("Brush.Border", "#2A2A2A");
        Set("Brush.Hover", "#1C1C1C");
        Set("Brush.Active", "#242424");
        Set("Brush.Accent", "#F2F2F2");
        Set("Brush.AccentText", "#111111");
        Set("Brush.AccentHover", "#DCDCDC");
        Set("Brush.Input", "#141414");
        Set("Brush.Danger", "#8C2F2F");
    }

    private static void Set(string key, string hex) => Set(key, (Color)ColorConverter.ConvertFromString(hex));

    private static void Set(string key, Color color)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        if (resources[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }

        resources[key] = new SolidColorBrush(color);
    }

    private static bool IsSystemLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value ? value != 0 : true;
    }
}

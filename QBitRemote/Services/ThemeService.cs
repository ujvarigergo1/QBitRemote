using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace QBitRemote.Services;

public static class ThemeService
{
    public static IReadOnlyList<ThemeOption> Options { get; } = [new("System", "system"), new("Light", "light"), new("Dark", "dark")];
    public static void Apply(string? preference)
    {
        var dark = preference?.Equals("dark", StringComparison.OrdinalIgnoreCase) == true ||
                   (string.IsNullOrWhiteSpace(preference) || preference.Equals("system", StringComparison.OrdinalIgnoreCase)) && IsSystemDark();
        var resources = Application.Current.Resources;
        resources["AppBackground"] = Brush(dark ? "#111827" : "#F8FAFC");
        resources["SurfaceBackground"] = Brush(dark ? "#1F2937" : "#FFFFFF");
        resources["ControlBackground"] = Brush(dark ? "#111827" : "#F8FAFC");
        resources["PrimaryText"] = Brush(dark ? "#F9FAFB" : "#0F172A");
        resources["MutedText"] = Brush(dark ? "#CBD5E1" : "#475569");
        resources["BorderBrush"] = Brush(dark ? "#475569" : "#CBD5E1");
        resources["GridLineBrush"] = Brush(dark ? "#334155" : "#EDF2F7");
        resources[SystemColors.WindowBrushKey] = Brush(dark ? "#1F2937" : "#FFFFFF");
        resources[SystemColors.WindowTextBrushKey] = Brush(dark ? "#F9FAFB" : "#0F172A");
        resources[SystemColors.ControlBrushKey] = Brush(dark ? "#1F2937" : "#F8FAFC");
        resources[SystemColors.ControlTextBrushKey] = Brush(dark ? "#F9FAFB" : "#0F172A");
        resources[SystemColors.ControlLightBrushKey] = Brush(dark ? "#334155" : "#FFFFFF");
        resources[SystemColors.ControlLightLightBrushKey] = Brush(dark ? "#1F2937" : "#FFFFFF");
        resources[SystemColors.ControlDarkBrushKey] = Brush(dark ? "#0F172A" : "#94A3B8");
        resources[SystemColors.ControlDarkDarkBrushKey] = Brush(dark ? "#020617" : "#64748B");
        resources[SystemColors.GrayTextBrushKey] = Brush(dark ? "#94A3B8" : "#64748B");
        resources[SystemColors.MenuBrushKey] = Brush(dark ? "#1F2937" : "#FFFFFF");
        resources[SystemColors.MenuTextBrushKey] = Brush(dark ? "#F9FAFB" : "#0F172A");
        resources[SystemColors.HighlightBrushKey] = Brush(dark ? "#2563EB" : "#2563EB");
        resources[SystemColors.HighlightTextBrushKey] = Brush("#FFFFFF");
        resources[SystemColors.InactiveSelectionHighlightBrushKey] = Brush(dark ? "#334155" : "#DBEAFE");
        resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = Brush(dark ? "#F9FAFB" : "#0F172A");
    }
    private static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));
    private static bool IsSystemDark()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return key?.GetValue("AppsUseLightTheme") is int value && value == 0; }
        catch { return false; }
    }
}
public sealed record ThemeOption(string Name, string Value) { public override string ToString() => LocalizationService.Instance["Theme" + Name]; }

namespace MarkdownPreview.Core;

/// <summary>Shared colors used by native editor controls and generated preview pages.</summary>
public sealed record ThemePalette(bool IsDark, string Surface, string Panel, string Input, string Text, string Muted, string Stroke, string Hover, string Accent)
{
    public string Link => IsDark ? "#9CDCFE" : Accent;
}

/// <summary>Defines stable preference identifiers and coordinated application palettes.</summary>
public static class AppThemes
{
    public static IReadOnlyList<string> Names { get; } = ["Dark", "Midnight", "Graphite", "Forest", "Warm", "Light", "System"];

    /// <summary>Resolves a saved identifier, with a safe default for unknown preferences.</summary>
    public static ThemePalette Get(string name, bool systemDark = false) => name switch
    {
        "Light" => new(false, "#FAFBFD", "#F0F2F6", "#FFFFFF", "#202632", "#657086", "#D8DDE7", "#E2E8F3", "#245DC1"),
        "System" => Get(systemDark ? "Dark" : "Light"),
        "Midnight" => new(true, "#111827", "#172338", "#0D1523", "#E1EBFA", "#93A9C8", "#30425F", "#263D5D", "#73C7F7"),
        "Graphite" => new(true, "#222222", "#2A2A2A", "#191919", "#E9E5EE", "#A7A1AF", "#434047", "#3F3949", "#C4A7E7"),
        "Forest" => new(true, "#17231F", "#1E2E27", "#111C18", "#E1EEE4", "#9AB5A3", "#385144", "#304B3B", "#97D5AA"),
        "Warm" => new(true, "#28211E", "#342A25", "#1F1916", "#EFE4D8", "#BAA38E", "#514236", "#4B3A2B", "#EDBC80"),
        _ => new(true, "#1E2026", "#252832", "#191B21", "#E5E7EB", "#949CAC", "#373C49", "#353C4D", "#82AAFF")
    };
}

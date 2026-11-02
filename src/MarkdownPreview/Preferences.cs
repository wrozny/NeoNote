using System.IO;
using System.Text.Json;

namespace MarkdownPreview;

/// <summary>Stores non-secret per-user editor preferences.</summary>
public sealed class Preferences
{
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "pl";
    public string Font { get; set; } = "Consolas";
    public double FontSize { get; set; } = 15;
    public bool WordWrap { get; set; }
    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductInfo.Name, "settings.json");

    /// <summary>Loads preferences, tolerating a missing or damaged local settings file.</summary>
    public static Preferences Load()
    {
        try
        {
            var path = SettingsPath;
            if (!File.Exists(path))
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                path = new[] { "NeoPad", "MarkdownPreview" }.Select(name => Path.Combine(root, name, "settings.json"))
                    .FirstOrDefault(File.Exists) ?? path;
            }
            return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) ?? new();
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    /// <summary>Persists preferences without storing document contents.</summary>
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var temporary = SettingsPath + "." + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this)); File.Move(temporary, SettingsPath, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

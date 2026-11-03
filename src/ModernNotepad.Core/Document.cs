using System.Text;

namespace ModernNotepad.Core;

public enum FileFormat { Markdown, Json, Xml, Ini, Toml, Text }

/// <summary>Owns document identity and preserves encoding during atomic saves.</summary>
public sealed class Document
{
    public const long MaximumFileBytes = 20 * 1024 * 1024;
    public string? Path { get; private set; }
    public string Text { get; set; } = "";
    public string SavedText { get; private set; } = "";
    public FileFormat Format { get; set; } = FileFormat.Markdown;
    public Encoding Encoding { get; private set; } = new UTF8Encoding(false, true);
    public byte[]? DiskBytes { get; private set; }
    public bool IsDirty => Path is null || Text != SavedText;
    public string LineEnding => Text.Contains("\r\n") ? "CRLF" : Text.Contains('\n') ? "LF" : "CRLF";

    /// <summary>Maps a filename to its editor format.</summary>
    public static FileFormat DetectFormat(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".md" or ".markdown" => FileFormat.Markdown,
        ".json" => FileFormat.Json, ".xml" or ".xaml" or ".svg" => FileFormat.Xml,
        ".ini" or ".cfg" => FileFormat.Ini, ".toml" => FileFormat.Toml, _ => FileFormat.Text
    };

    /// <summary>Loads bytes with BOM detection and a Windows-1250 fallback for legacy Polish text.</summary>
    public static Document Open(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumFileBytes) throw new DocumentTooLargeException(stream.Length);
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        Encoding encoding = new UTF8Encoding(false, true);
        var offset = 0;
        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) { encoding = new UTF8Encoding(true, true); offset = 3; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE, 0, 0 })) { encoding = new UTF32Encoding(false, true, true); offset = 4; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0, 0, 0xFE, 0xFF })) { encoding = new UTF32Encoding(true, true, true); offset = 4; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE })) { encoding = new UnicodeEncoding(false, true, true); offset = 2; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF })) { encoding = new UnicodeEncoding(true, true, true); offset = 2; }
        string text;
        try { text = encoding.GetString(bytes, offset, bytes.Length - offset); }
        catch (DecoderFallbackException) { encoding = Encoding.GetEncoding(1250); text = encoding.GetString(bytes); }
        return new Document { Path = System.IO.Path.GetFullPath(path), Text = text, SavedText = text,
            Format = DetectFormat(path), Encoding = encoding, DiskBytes = bytes };
    }

    /// <summary>Detects external modifications, including deletion.</summary>
    public bool HasExternalChanges() => Path is not null &&
        (!File.Exists(Path) || new FileInfo(Path).Length != DiskBytes?.LongLength || !File.ReadAllBytes(Path).AsSpan().SequenceEqual(DiskBytes));

    /// <summary>Writes to a sibling temporary file and updates identity only after a successful replacement.</summary>
    public void Save(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var bytes = Encoding.GetPreamble().Concat(Encoding.GetBytes(Text)).ToArray();
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
            else File.Move(temporary, fullPath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        Path = fullPath;
        SavedText = Text;
        DiskBytes = bytes;
    }
}

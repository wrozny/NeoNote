using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace MarkdownPreview.Core;

/// <summary>Formats structured documents without silently dropping comments or changing unsupported formats.</summary>
public static class FileFormatter
{
    /// <summary>Reports formats with a loss-aware formatter in this version.</summary>
    public static bool Supports(FileFormat format) => format is FileFormat.Json or FileFormat.Xml;

    /// <summary>Formats valid JSON or XML using four spaces and preserves line endings and final-newline intent.</summary>
    public static string Format(string text, FileFormat format)
    {
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string result;
        switch (format)
        {
            case FileFormat.Json:
                using (var document = JsonDocument.Parse(text))
                    result = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
                    { WriteIndented = true, IndentSize = 4, NewLine = newline, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                break;
            case FileFormat.Xml:
                using (var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = true }))
                {
                    var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                    var output = new StringBuilder();
                    using (var writer = XmlWriter.Create(output, new XmlWriterSettings
                    { Indent = true, IndentChars = "    ", NewLineChars = newline, NewLineHandling = NewLineHandling.None, OmitXmlDeclaration = true }))
                        document.Save(writer);
                    result = (document.Declaration is null ? "" : document.Declaration + newline) + output;
                }
                break;
            default:
                throw new NotSupportedException("Formatting is available for JSON and XML.");
        }
        return result.TrimEnd('\r', '\n') + (text.EndsWith('\n') ? newline : "");
    }
}

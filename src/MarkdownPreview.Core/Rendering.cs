using System.Net;
using System.Text.Json;
using System.Xml;
using Ganss.Xss;
using Markdig;
using Tomlyn;
using AngleSharp.Html.Parser;
using ColorCode;

namespace MarkdownPreview.Core;

public sealed record Diagnostic(int Line, int Column, string Message);

/// <summary>Parses documents without touching UI state.</summary>
public static class Rendering
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    /// <summary>Renders Markdown and sanitizes embedded HTML before insertion into a fixed page.</summary>
    public static string MarkdownBody(string text)
    {
        var sanitizer = new HtmlSanitizer();
        foreach (var tag in new[] { "details", "summary", "input" }) sanitizer.AllowedTags.Add(tag);
        foreach (var attribute in new[] { "class", "type", "checked", "disabled" }) sanitizer.AllowedAttributes.Add(attribute);
        var parsed = new HtmlParser().ParseDocument(sanitizer.Sanitize(Markdown.ToHtml(text, Pipeline)));
        foreach (var code in parsed.QuerySelectorAll("pre > code"))
        {
            var languageName = code.ClassList.FirstOrDefault(c => c.StartsWith("language-"))?[9..];
            var language = languageName is null ? null : Languages.FindById(languageName);
            if (language is not null) code.InnerHtml = new ColorCode.HtmlFormatter().GetHtmlString(code.TextContent, language);
        }
        return parsed.Body!.InnerHtml;
    }

    /// <summary>Builds a script-free preview with a constrained content security policy.</summary>
    public static string Preview(string body, bool dark) => "<!doctype html><html><head><meta charset='utf-8'>" +
        "<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; img-src https: http: data:; style-src 'unsafe-inline'; script-src 'none'; frame-src 'none'; form-action 'none'\">" +
        "<style>body{font:16px/1.65 Segoe UI,sans-serif;padding:20px 32px;overflow-wrap:anywhere;color:" +
        (dark ? "#ddd;background:#202124" : "#24292f;background:#fff") +
        "}pre,code{font-family:Consolas,monospace;background:" + (dark ? "#303238" : "#eef0f3") +
        ";border-radius:4px}pre{padding:16px;overflow:auto}code{padding:2px 4px}pre code{padding:0}a{color:#58a6ff}" +
        "table{border-collapse:collapse}td,th{border:1px solid #777;padding:6px 12px}blockquote{border-left:3px solid #888;margin-left:0;padding-left:16px}" +
        "img{max-width:100%}h1,h2{border-bottom:1px solid #666;padding-bottom:6px}input{pointer-events:none}</style></head><body>" + body + "</body></html>";

    /// <summary>Returns syntax errors with one-based locations for the selected format.</summary>
    public static IReadOnlyList<Diagnostic> Validate(string text, FileFormat format)
    {
        try
        {
            switch (format)
            {
                case FileFormat.Json:
                    using (JsonDocument.Parse(text)) { }
                    break;
                case FileFormat.Xml:
                    using (var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                        while (reader.Read()) { }
                    break;
                case FileFormat.Toml:
                    return Toml.Parse(text).Diagnostics.Where(d => d.Kind == Tomlyn.Syntax.DiagnosticMessageKind.Error)
                        .Select(d => new Diagnostic(d.Span.Start.Line + 1, d.Span.Start.Column + 1, d.Message)).ToArray();
                case FileFormat.Ini:
                    var errors = new List<Diagnostic>();
                    var lines = text.Split('\n');
                    for (var i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i].Trim();
                        if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
                        if (line.StartsWith('[') ? !line.EndsWith(']') : line.IndexOf('=') <= 0)
                            errors.Add(new Diagnostic(i + 1, 1, "Expected [section] or key=value."));
                    }
                    return errors;
            }
        }
        catch (JsonException error) { return [new((int)(error.LineNumber ?? 0) + 1, (int)(error.BytePositionInLine ?? 0) + 1, error.Message)]; }
        catch (XmlException error) { return [new(error.LineNumber, error.LinePosition, error.Message)]; }
        return [];
    }
}

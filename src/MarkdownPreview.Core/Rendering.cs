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
    public static string MarkdownBody(string text, bool dark = true)
    {
        var sanitizer = new HtmlSanitizer();
        foreach (var tag in new[] { "details", "summary", "input" }) sanitizer.AllowedTags.Add(tag);
        foreach (var attribute in new[] { "class", "type", "checked", "disabled" }) sanitizer.AllowedAttributes.Add(attribute);
        var parsed = new HtmlParser().ParseDocument(sanitizer.Sanitize(Markdown.ToHtml(text, Pipeline)));
        foreach (var code in parsed.QuerySelectorAll("pre > code"))
        {
            var languageName = code.ClassList.FirstOrDefault(c => c.StartsWith("language-"))?[9..];
            var language = languageName is null ? null : Languages.FindById(languageName);
            if (language is not null) code.InnerHtml = new ColorCode.HtmlFormatter(dark ? ColorCode.Styling.StyleDictionary.DefaultDark : ColorCode.Styling.StyleDictionary.DefaultLight).GetHtmlString(code.TextContent, language);
        }
        return parsed.Body!.InnerHtml;
    }

    /// <summary>Builds a script-free preview with a constrained content security policy.</summary>
    public static string Preview(string body, bool dark) => "<!doctype html><html><head><meta charset='utf-8'>" +
        "<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; img-src https: http: data:; style-src 'unsafe-inline'; script-src 'none'; frame-src 'none'; form-action 'none'\">" +
        "<style>:root{color-scheme:" + (dark ? "dark" : "light") + "}*{box-sizing:border-box}body{font:15px/1.75 'Segoe UI',sans-serif;margin:0 auto;max-width:900px;padding:28px 34px 60px;overflow-wrap:anywhere;color:" +
        (dark ? "#e5e7eb;background:#1e2026" : "#202632;background:#fafbfd") +
        "}pre,code{font-family:Consolas,monospace;background:" + (dark ? "#272b35" : "#edf0f5") +
        ";border-radius:6px}pre{padding:18px;overflow:auto;line-height:1.5;border:1px solid " + (dark ? "#373c49" : "#d8dde7") +
        "}code{padding:2px 5px;font-size:.9em}pre code{padding:0}a{color:" + (dark ? "#82aaff" : "#245dc1") +
        ";text-underline-offset:3px}h1,h2,h3{line-height:1.3;letter-spacing:-.025em;margin-top:1.4em}h1{font-size:2em}h2{font-size:1.5em}" +
        "table{border-collapse:collapse;width:100%;margin:20px 0}td,th{border:1px solid " + (dark ? "#373c49" : "#d8dde7") +
        ";padding:8px 12px;text-align:left}th{background:" + (dark ? "#252832" : "#f0f2f6") +
        "}blockquote{border-left:3px solid #82aaff;margin-left:0;padding:4px 18px;color:" + (dark ? "#aeb8c9" : "#657086") +
        "}img{max-width:100%;border-radius:6px}h1,h2{border-bottom:1px solid " + (dark ? "#373c49" : "#d8dde7") +
        ";padding-bottom:10px}input{pointer-events:none}hr{border:0;border-top:1px solid #657086;margin:28px 0}::selection{background:#385580;color:#fff}" +
        "@media(max-width:450px){body{padding:18px}}::-webkit-scrollbar{width:10px;height:10px}::-webkit-scrollbar-thumb{background:#657086;border-radius:6px}</style></head><body>" + body + "</body></html>";

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

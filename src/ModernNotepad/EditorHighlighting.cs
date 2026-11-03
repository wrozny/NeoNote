using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using ModernNotepad.Core;

namespace ModernNotepad;

/// <summary>Installs language rules and draws validation markers on erroneous lines.</summary>
public sealed class EditorHighlighting : DocumentColorizingTransformer
{
    public IReadOnlyList<Diagnostic> Diagnostics { get; set; } = [];

    /// <summary>Colors a diagnostic location without changing document text.</summary>
    protected override void ColorizeLine(ICSharpCode.AvalonEdit.Document.DocumentLine line)
    {
        if (line.Length == 0 || !Diagnostics.Any(d => d.Line == line.LineNumber)) return;
        ChangeLinePart(line.Offset, line.EndOffset, element => element.TextRunProperties.SetTextDecorations(System.Windows.TextDecorations.Underline));
    }

    /// <summary>Creates a format-specific definition with theme-aware token colors.</summary>
    public static IHighlightingDefinition Create(FileFormat format, bool dark)
    {
        var ruleSet = new HighlightingRuleSet();
        var rules = new List<(string Pattern, string Color)>();
        var blue = dark ? "#79B8FF" : "#005CC5";
        var green = dark ? "#9ECBFF" : "#032F62";
        var orange = dark ? "#FFAB70" : "#B34B00";
        switch (format)
        {
            case FileFormat.Markdown:
                rules.Add((@"^\s{0,3}#{1,6}\s.*$", blue));
                rules.Add((@"`[^`]+`|\*\*.+?\*\*|__.+?__", orange));
                rules.Add((@"!?\[[^\]]*\]\([^)]*\)", green));
                rules.Add((@"</?[^>]+>", blue));
                break;
            case FileFormat.Json:
            case FileFormat.Toml:
            case FileFormat.Ini:
                rules.Add(("\"(?:\\\\.|[^\"\\\\])*\"|'[^']*'", green));
                rules.Add((@"\b(?:true|false|null|inf|nan)\b", blue));
                rules.Add((@"\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b", orange));
                rules.Add((@"[{}\[\]=:,]", blue));
                if (format != FileFormat.Json) rules.Insert(0, (@"[#;].*$", "#7A9C72"));
                break;
            case FileFormat.Xml:
                rules.Add((@"<!--.*?-->", "#7A9C72"));
                rules.Add(("\"[^\"]*\"|'[^']*'", green));
                rules.Add((@"</?[\w:.-]+|/?>", blue));
                break;
        }
        foreach (var (pattern, color) in rules)
            ruleSet.Rules.Add(new HighlightingRule { Regex = new System.Text.RegularExpressions.Regex(pattern),
                Color = new HighlightingColor { Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString(color)) } });
        return new Definition(ruleSet);
    }

    private sealed class Definition(HighlightingRuleSet rules) : IHighlightingDefinition
    {
        public string Name => "Document";
        public HighlightingRuleSet MainRuleSet => rules;
        public IEnumerable<HighlightingColor> NamedHighlightingColors => [];
        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();
        /// <summary>Returns the single document rule set.</summary>
        public HighlightingRuleSet GetNamedRuleSet(string name) => rules;
        /// <summary>Reports that this definition has no separately named colors.</summary>
        public HighlightingColor? GetNamedColor(string name) => null;
    }
}

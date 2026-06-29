namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Baut den Hierarchie-Baum aus einer flachen <see cref="CodeDefinition"/>-Liste
/// und berechnet den Pfad eines Codes im Baum.
/// Logik aus ObservationCatalogViewModel extrahiert, verhaltensneutral.
/// </summary>
public static class VsaCatalogTreeBuilder
{
    // SN EN 13508-2 Hauptkategorie-Labels (2-Zeichen-Prefix)
    public static readonly IReadOnlyDictionary<string, string> MainCategoryLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AE"] = "Aenderungen der Grundlagen",
            ["BA"] = "Struktur der Rohrleitungen",
            ["BB"] = "Betrieb der Rohrleitungen",
            ["BC"] = "Bestandsaufnahme der Rohrleitungen",
            ["BD"] = "Sonstiges Rohrleitungen",
            ["DA"] = "Struktur Schacht",
            ["DB"] = "Betrieb Schacht",
            ["DC"] = "Bestandsaufnahme Schacht",
            ["DD"] = "Sonstiges Schacht",
        };

    /// <summary>
    /// Baut den Hierarchie-Baum aus einer Liste von Codes in einen <see cref="CatalogTreeNode"/> (Root).
    /// CategoryPath hat Vorrang; sonst automatische Strukturierung aus Code-Prefix.
    /// </summary>
    public static CatalogTreeNode BuildTree(
        IEnumerable<CodeDefinition> allCodes,
        ICodeCatalogProvider catalog)
    {
        var root = new CatalogTreeNode("Root", "Root");
        var list = allCodes.ToList();

        foreach (var code in list)
        {
            // Wenn categoryPath explizit gesetzt ist, diesen verwenden
            if (code.CategoryPath is { Count: > 0 })
            {
                var node = root;
                foreach (var level in code.CategoryPath)
                {
                    if (string.IsNullOrWhiteSpace(level))
                        continue;
                    if (!node.Children.TryGetValue(level, out var next))
                    {
                        next = new CatalogTreeNode(level, ResolveCategoryLabel(level, catalog));
                        node.Children[level] = next;
                    }
                    node = next;
                }
                node.Codes.Add(code);
                continue;
            }

            // Automatische Baumstruktur aus Code-Prefix (SN EN 13508-2)
            var codeStr = (code.Code ?? string.Empty).Trim().ToUpperInvariant();
            if (codeStr.Length < 3)
            {
                root.Codes.Add(code);
                continue;
            }

            // Ebene 1: Hauptkategorie (2 Zeichen, z.B. "BA", "BB", "BC")
            var mainPrefix = codeStr.Substring(0, 2);
            if (!root.Children.TryGetValue(mainPrefix, out var mainNode))
            {
                var mainLabel = MainCategoryLabels.TryGetValue(mainPrefix, out var label)
                    ? label
                    : (code.Group ?? mainPrefix);
                mainNode = new CatalogTreeNode(mainPrefix, mainLabel);
                root.Children[mainPrefix] = mainNode;
            }

            // Ebene 2: Unterkategorie (3 Zeichen, z.B. "BBA", "BBC")
            var subPrefix = codeStr.Substring(0, 3);
            if (!mainNode.Children.TryGetValue(subPrefix, out var subNode))
            {
                var subLabel = ResolveSubCategoryLabel(subPrefix, catalog, list);
                subNode = new CatalogTreeNode(subPrefix, subLabel);
                mainNode.Children[subPrefix] = subNode;
            }

            // Code als Blatt unter der Unterkategorie
            subNode.Codes.Add(code);
        }

        return root;
    }

    /// <summary>
    /// Berechnet den Pfad-Key-Liste von der Root zum gegebenen Code.
    /// CategoryPath hat Vorrang; sonst automatisch aus Code-Prefix.
    /// </summary>
    public static List<string> BuildPathToCode(CodeDefinition code)
    {
        // Wenn categoryPath explizit gesetzt ist, diesen verwenden
        if (code.CategoryPath is { Count: > 0 })
            return code.CategoryPath;

        // Automatisch aus Code-Prefix ableiten
        var codeStr = (code.Code ?? string.Empty).Trim().ToUpperInvariant();
        var path = new List<string>();
        if (codeStr.Length >= 2)
            path.Add(codeStr.Substring(0, 2));
        if (codeStr.Length >= 3)
            path.Add(codeStr.Substring(0, 3));
        return path;
    }

    /// <summary>
    /// Loest das Label fuer einen categoryPath-Eintrag auf (mit Katalog-Lookup).
    /// </summary>
    public static string ResolveCategoryLabel(string key, ICodeCatalogProvider catalog)
    {
        if (catalog.TryGet(key, out var def))
            return $"{def.Code}  {def.Title}";
        return key;
    }

    /// <summary>
    /// Loest das Unterkategorie-Label fuer einen 3-Zeichen-Prefix auf.
    /// Der VSA-KEK-Katalog ist die Quelle der Wahrheit fuer Code-Titel.
    /// </summary>
    public static string ResolveSubCategoryLabel(
        string prefix,
        ICodeCatalogProvider catalog,
        IReadOnlyList<CodeDefinition> allCodes)
    {
        if (catalog.TryGet(prefix, out var def))
            return FormatCatalogLabel(prefix, def);

        // Sonst: finde den ersten Code mit diesem Prefix und nutze dessen Gruppen-Info
        var first = allCodes.FirstOrDefault(c =>
            c.Code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (first is not null && !string.IsNullOrWhiteSpace(first.Title))
        {
            var groupPart = ExtractSubGroupName(first);
            if (!string.IsNullOrWhiteSpace(groupPart))
                return $"{prefix}  {groupPart}";
        }

        return prefix;
    }

    /// <summary>
    /// Formatiert ein Katalog-Label aus Code + Titel.
    /// </summary>
    public static string FormatCatalogLabel(string requestedCode, CodeDefinition def)
    {
        var code = string.IsNullOrWhiteSpace(def.Code) ? requestedCode : def.Code.Trim();
        var title = def.Title?.Trim();
        return string.IsNullOrWhiteSpace(title) ? code : $"{code}  {title}";
    }

    /// <summary>
    /// Extrahiert den Kurzname einer Untergruppe aus dem Titel des ersten Codes.
    /// </summary>
    public static string ExtractSubGroupName(CodeDefinition firstCode)
    {
        var title = firstCode.Title ?? string.Empty;
        if (title.Contains(':'))
            return title.Substring(0, title.IndexOf(':')).Trim();

        return title;
    }
}

/// <summary>
/// Ein Knoten im Katalog-Hierarchiebaum.
/// </summary>
public sealed class CatalogTreeNode
{
    public string Key { get; }
    public string Label { get; }
    public Dictionary<string, CatalogTreeNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<CodeDefinition> Codes { get; } = new();

    public CatalogTreeNode(string key, string label)
    {
        Key = key;
        Label = label;
    }
}

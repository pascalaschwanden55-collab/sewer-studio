using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace AuswertungPro.Next.Application.Protocol;

public sealed class XmlCodeCatalogProvider : ICodeCatalogProvider
{
    private readonly string _xmlPath;
    private readonly string? _fallbackJsonPath;
    private readonly string? _fallbackTextXmlPath;
    private readonly List<CodeDefinition> _codes = new();
    private readonly Dictionary<string, CodeDefinition> _byCode = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> LastLoadWarnings { get; private set; } = Array.Empty<string>();

    public XmlCodeCatalogProvider(string xmlPath, string? fallbackJsonPath = null, string? fallbackTextXmlPath = null)
    {
        _xmlPath = xmlPath;
        _fallbackJsonPath = fallbackJsonPath;
        _fallbackTextXmlPath = fallbackTextXmlPath;
        Load();
    }

    public IReadOnlyList<CodeDefinition> GetAll()
        => _codes.Select(CloneCode).ToList();

    public bool TryGet(string code, out CodeDefinition def)
    {
        if (_byCode.TryGetValue(NormalizeCode(code), out var match))
        {
            def = CloneCode(match);
            return true;
        }

        def = new CodeDefinition();
        return false;
    }

    public void Save(IReadOnlyList<CodeDefinition> codes)
    {
        // XML ist read-only. Wir aktualisieren nur den In-Memory-Cache,
        // damit Editoren nicht crashen.
        _codes.Clear();
        _byCode.Clear();
        foreach (var c in NormalizeCodes(codes ?? Array.Empty<CodeDefinition>()))
        {
            _codes.Add(c);
            _byCode[c.Code] = c;
        }
    }

    public IReadOnlyList<string> AllowedCodes()
        => _codes.Select(x => x.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
    {
        var list = NormalizeCodes(codes ?? _codes);
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < list.Count; i++)
        {
            var row = i + 1;
            var codeDef = list[i];

            if (string.IsNullOrWhiteSpace(codeDef.Code))
                errors.Add($"Code fehlt (Eintrag #{row}).");
            else if (!seen.Add(codeDef.Code))
                errors.Add($"Duplikat-Code '{codeDef.Code}'.");

            if (string.IsNullOrWhiteSpace(codeDef.Title))
                errors.Add($"Title fehlt fuer Code '{SafeCodeLabel(codeDef.Code, row)}'.");
        }

        return errors;
    }

    private void Load()
    {
        if (!File.Exists(_xmlPath))
            throw new FileNotFoundException($"VSA XML-Katalog nicht gefunden: {_xmlPath}");

        var fallback = LoadFallbackJson(_fallbackJsonPath);
        var fallbackTexts = LoadFallbackTextMap(_fallbackTextXmlPath);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var doc = XDocument.Load(_xmlPath);
        var root = doc.Root ?? throw new InvalidOperationException("XML-Katalog ohne Root-Element.");

        if (string.Equals(root.Name.LocalName, "WCCat", StringComparison.OrdinalIgnoreCase))
        {
            LoadWinCanCatalog(root, fallback, fallbackTexts, counts);
            return;
        }

        var navItems = ParseNavigationItems(root, fallbackTexts);
        var placeholders = ParseObservationPlaceholders(root);
        var valueLists = ParseValueLists(root);

        _codes.Clear();
        _byCode.Clear();

        foreach (var op in root.Element("OpCodes")?.Elements("CatalogOpCode") ?? Enumerable.Empty<XElement>())
        {
            var code = op.Attribute("OpCode")?.Value?.Trim() ?? string.Empty;
            if (code.Length == 0)
                continue;

            var title = DecodeText(op.Element("Text"));
            var description = DecodeText(op.Element("ObservationText")) ?? DecodeText(op.Element("Remarks"));

            var categoryPath = BuildCategoryPath(op, navItems);
            var group = categoryPath.Count >= 2
                ? $"{categoryPath[0]}/{categoryPath[1]}"
                : (categoryPath.FirstOrDefault() ?? "Unbekannt");

            if (fallback.TryGetValue(code, out var fallbackDef))
            {
                if (!string.IsNullOrWhiteSpace(fallbackDef.Title))
                    title = fallbackDef.Title;
                if (!string.IsNullOrWhiteSpace(fallbackDef.Group))
                    group = fallbackDef.Group;
                if (!string.IsNullOrWhiteSpace(fallbackDef.Description))
                    description = fallbackDef.Description;
            }

            if (fallbackTexts.TryGetValue(code, out var fallbackText))
            {
                if (string.IsNullOrWhiteSpace(title))
                    title = fallbackText.Title;
                if (string.IsNullOrWhiteSpace(description))
                    description = fallbackText.Description;
            }

            if (string.IsNullOrWhiteSpace(title))
                title = code;
            if (string.IsNullOrWhiteSpace(group))
                group = "Unbekannt";

            var def = new CodeDefinition
            {
                Code = code,
                Title = title,
                Group = group,
                CategoryPath = categoryPath,
                Description = description,
                Parameters = BuildParameters(op, placeholders, valueLists, fallbackDef: fallbackDef)
            };

            if (fallback.TryGetValue(code, out var f2))
            {
                def.RequiresRange = f2.RequiresRange;
                def.RangeThresholdM = f2.RangeThresholdM;
                def.RangeThresholdText = f2.RangeThresholdText;
            }

            AddOrUpdate(def, counts);
        }

        FinalizeLoad(counts);
    }

    private void LoadWinCanCatalog(
        XElement root,
        Dictionary<string, CodeDefinition> fallback,
        Dictionary<string, FallbackText> fallbackTexts,
        Dictionary<string, int> counts)
    {
        XNamespace ns = root.Name.Namespace;

        _codes.Clear();
        _byCode.Clear();

        foreach (var row in root.Descendants(ns + "CHAREXT"))
        {
            var closeCodeRaw = row.Element(ns + "CE_CloseCode")?.Value ?? string.Empty;
            var code = ExtractCloseCode(closeCodeRaw);
            if (string.IsNullOrWhiteSpace(code))
                code = (row.Element(ns + "CE_Code")?.Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var title = (row.Element(ns + "CE_ChildCaption")?.Value ?? string.Empty).Trim();
            var description = (row.Element(ns + "CE_Remarks")?.Value ?? string.Empty).Trim();

            var group = string.Empty;

            if (fallback.TryGetValue(code, out var fallbackDef))
            {
                if (!string.IsNullOrWhiteSpace(fallbackDef.Title))
                    title = fallbackDef.Title;
                if (!string.IsNullOrWhiteSpace(fallbackDef.Group))
                    group = fallbackDef.Group;
                if (!string.IsNullOrWhiteSpace(fallbackDef.Description))
                    description = fallbackDef.Description;
            }

            if (fallbackTexts.TryGetValue(code, out var fallbackText))
            {
                if (string.IsNullOrWhiteSpace(title))
                    title = fallbackText.Title;
                if (string.IsNullOrWhiteSpace(description))
                    description = fallbackText.Description;
            }

            if (string.IsNullOrWhiteSpace(title))
                title = code;
            if (string.IsNullOrWhiteSpace(group))
                group = "Unbekannt";

            var categoryPath = BuildCategoryPathFromGroup(group);
            categoryPath.AddRange(BuildPrefixPath(code));
            categoryPath = categoryPath
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var def = new CodeDefinition
            {
                Code = code,
                Title = title,
                Group = group,
                CategoryPath = categoryPath,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                Parameters = fallbackDef?.Parameters.Select(CloneParameter).ToList() ?? new List<CodeParameter>()
            };

            if (fallbackDef is not null)
            {
                def.RequiresRange = fallbackDef.RequiresRange;
                def.RangeThresholdM = fallbackDef.RangeThresholdM;
                def.RangeThresholdText = fallbackDef.RangeThresholdText;
            }

            AddOrUpdate(def, counts);
        }

        FinalizeLoad(counts);
    }

    private static string ExtractCloseCode(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts[0].Trim() : raw.Trim();
    }

    public void Reload()
    {
        Load();
    }

    private static Dictionary<string, CodeDefinition> LoadFallbackJson(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new Dictionary<string, CodeDefinition>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(path);
            var doc = System.Text.Json.JsonSerializer.Deserialize<CodeCatalogDocument>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? new CodeCatalogDocument();

            return doc.Codes
                .Where(c => !string.IsNullOrWhiteSpace(c.Code))
                .ToDictionary(c => c.Code, c => c, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, CodeDefinition>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, CatalogNavItem> ParseNavigationItems(
        XElement catalog,
        Dictionary<string, FallbackText> fallbackTexts)
    {
        var dict = new Dictionary<string, CatalogNavItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in catalog.Element("NavigationItems")?.Elements("CatalogNavigationItem") ?? Enumerable.Empty<XElement>())
        {
            var id = el.Attribute("Id")?.Value ?? "";
            if (id.Length == 0)
                continue;

            var navCode = el.Attribute("Code")?.Value?.Trim();
            var title = DecodeText(el.Element("Text")) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(navCode) && fallbackTexts.TryGetValue(navCode, out var fallback))
                title = fallback.Title;

            dict[id] = new CatalogNavItem
            {
                Id = id,
                ParentId = el.Attribute("ParentId")?.Value,
                Code = navCode,
                Title = title
            };
        }

        return dict;
    }

    private static Dictionary<string, PlaceholderInfo> ParseObservationPlaceholders(XElement catalog)
    {
        var dict = new Dictionary<string, PlaceholderInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in catalog.Element("ObservationPlaceholders")?.Elements("CatalogObservationPlaceholder") ?? Enumerable.Empty<XElement>())
        {
            var key = el.Attribute("Placeholder")?.Value;
            if (string.IsNullOrWhiteSpace(key))
                continue;

            dict[key] = new PlaceholderInfo
            {
                Placeholder = key!,
                DataType = el.Attribute("DataType")?.Value ?? "String",
                PlaceholderType = el.Element("PlaceholderType")?.Value ?? string.Empty,
                Text = DecodeText(el.Element("Text")) ?? string.Empty,
                Prefix = DecodeText(el.Element("Prefix")),
                Suffix = DecodeText(el.Element("Suffix")),
                Unit = DecodeText(el.Element("Unit"))
            };
        }

        return dict;
    }

    private static Dictionary<string, List<string>> ParseValueLists(XElement catalog)
    {
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in catalog.Element("Profiles")?.Elements("CatalogProfile") ?? Enumerable.Empty<XElement>())
        {
            var valueLists = profile.Element("ValueLists");
            if (valueLists is null)
                continue;

            foreach (var list in valueLists.Elements("ValueList"))
            {
                var name = list.Attribute("Name")?.Value;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var keys = list.Elements("ValueListItem")
                    .Select(x => x.Attribute("Key")?.Value)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (keys.Count > 0)
                    dict[name!] = keys;
            }
        }

        return dict;
    }

    private static Dictionary<string, FallbackText> LoadFallbackTextMap(string? path)
    {
        var map = new Dictionary<string, FallbackText>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return map;

        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root is null)
                return map;

            var ns = root.Name.Namespace;

            LoadFallbackTextMapCore(root.Descendants(ns + "CE"), ns, map);
            LoadFallbackTextMapCore(root.Descendants(ns + "BASECODE"), ns, map);

            // Fallback ohne Namespace (falls Datei keine Namespace-Qualifizierung nutzt)
            if (map.Count == 0)
            {
                LoadFallbackTextMapCore(root.Descendants("CE"), null, map);
                LoadFallbackTextMapCore(root.Descendants("BASECODE"), null, map);
            }
        }
        catch
        {
            // ignore fallback errors
        }

        return map;
    }

    private static void LoadFallbackTextMapCore(IEnumerable<XElement> elements, XNamespace? ns, Dictionary<string, FallbackText> map)
    {
        foreach (var el in elements)
        {
            var code = (ns is null ? el.Element("CE_Code") : el.Element(ns + "CE_Code"))?.Value?.Trim()
                       ?? (ns is null ? el.Element("BC_Code") : el.Element(ns + "BC_Code"))?.Value?.Trim();

            var caption = (ns is null ? el.Element("CE_ChildCaption") : el.Element(ns + "CE_ChildCaption"))?.Value?.Trim()
                          ?? (ns is null ? el.Element("BC_ChildCaption") : el.Element(ns + "BC_ChildCaption"))?.Value?.Trim();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(caption))
                continue;

            if (!map.ContainsKey(code))
            {
                map[code] = new FallbackText
                {
                    Title = caption,
                    Description = null
                };
            }
        }
    }

    private static List<CodeParameter> BuildParameters(
        XElement op,
        Dictionary<string, PlaceholderInfo> placeholders,
        Dictionary<string, List<string>> valueLists,
        CodeDefinition? fallbackDef)
    {
        var list = new List<CodeParameter>();
        foreach (var p in op.Element("Parameters")?.Elements("CatalogOpCodeParameter") ?? Enumerable.Empty<XElement>())
        {
            var placeholder = p.Attribute("Placeholder")?.Value?.Trim() ?? string.Empty;
            var columnId = p.Attribute("ColumnId")?.Value?.Trim() ?? string.Empty;
            var placeholderType = p.Attribute("PlaceholderType")?.Value?.Trim() ?? string.Empty;
            var listClassId = p.Attribute("ListClassId")?.Value?.Trim();

            var info = placeholders.TryGetValue(placeholder, out var ph) ? ph : null;
            var name = info?.Text;
            if (string.IsNullOrWhiteSpace(name))
                name = !string.IsNullOrWhiteSpace(placeholderType) ? placeholderType : placeholder;
            if (string.IsNullOrWhiteSpace(name))
                name = columnId;
            if (string.IsNullOrWhiteSpace(name))
                name = "Parameter";

            var type = ResolveType(info, placeholderType, listClassId, columnId);
            var allowedValues = listClassId is not null && valueLists.TryGetValue(listClassId, out var values)
                ? values
                : null;
            var required = string.Equals(p.Attribute("IsMandatory")?.Value, "true", StringComparison.OrdinalIgnoreCase);

            list.Add(new CodeParameter
            {
                Name = name,
                Type = type,
                AllowedValues = allowedValues,
                Unit = info?.Unit,
                Required = required
            });
        }

        if (list.Count == 0 && fallbackDef is not null && fallbackDef.Parameters.Count > 0)
            list.AddRange(fallbackDef.Parameters.Select(CloneParameter));

        return list;
    }

    private static string ResolveType(PlaceholderInfo? info, string placeholderType, string? listClassId, string columnId)
    {
        if (!string.IsNullOrWhiteSpace(listClassId))
            return "enum";

        if (string.Equals(placeholderType, "FromClock", StringComparison.OrdinalIgnoreCase)
            || string.Equals(placeholderType, "ToClock", StringComparison.OrdinalIgnoreCase))
            return "clock";

        if (!string.IsNullOrWhiteSpace(columnId) && columnId.Contains("CLK", StringComparison.OrdinalIgnoreCase))
            return "clock";

        var dataType = info?.DataType ?? string.Empty;
        if (dataType.Equals("Integer", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("Decimal", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("Double", StringComparison.OrdinalIgnoreCase))
            return "number";

        return "string";
    }

    private static List<string> BuildCategoryPath(XElement op, Dictionary<string, CatalogNavItem> navItems)
    {
        var navId = op.Element("AssignedNavigationItem")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(navId))
            return new List<string>();

        var path = new List<string>();
        var currentId = navId;
        var safety = 0;
        while (!string.IsNullOrWhiteSpace(currentId) && navItems.TryGetValue(currentId, out var item) && safety++ < 32)
        {
            var label = item.Title;
            if (string.IsNullOrWhiteSpace(label))
                label = item.Code ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(label))
                path.Add(label);

            currentId = item.ParentId;
        }

        path.Reverse();
        return path;
    }

    private static List<string> BuildCategoryPathFromGroup(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
            return new List<string>();
        var parts = group.Split(new[] { '/', '>' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? new List<string>() : parts.ToList();
    }

    private static List<string> BuildPrefixPath(string code)
    {
        var value = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (value.Length == 0)
            return new List<string>();

        var list = new List<string>();
        if (value.Length <= 3)
        {
            list.Add(value);
            return list;
        }

        list.Add(value.Substring(0, 3));
        if (value.Length >= 4)
            list.Add(value.Substring(0, 4));
        if (value.Length >= 5)
            list.Add(value.Substring(0, 5));
        if (value.Length > 5)
            list.Add(value);

        return list;
    }

    private static string? DecodeText(XElement? node)
    {
        if (node is null)
            return null;

        var textItem = node.Element("TextItem");
        if (textItem is null)
            return null;

        var raw = textItem.Value?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (raw.Length <= 4 && raw.All(c => c < 128))
            return raw;

        if (IsHex(raw))
            return null;

        return raw;
    }

    private static bool IsHex(string value)
    {
        if (value.Length % 2 != 0)
            return false;
        foreach (var ch in value)
        {
            if (!Uri.IsHexDigit(ch))
                return false;
        }
        return true;
    }

    // NOTE: Hex-Text im WinCan-Katalog ist obfuskiert. Wir dekodieren ihn nicht,
    // damit keine "Muell-Texte" angezeigt werden. Klartext kommt aus JSON-Fallback.

    private static CodeDefinition CloneCode(CodeDefinition def)
        => new()
        {
            Code = def.Code,
            Title = def.Title,
            Group = def.Group,
            CategoryPath = def.CategoryPath is null ? new List<string>() : new List<string>(def.CategoryPath),
            Description = def.Description,
            Parameters = def.Parameters.Select(CloneParameter).ToList(),
            RequiresRange = def.RequiresRange,
            RangeThresholdM = def.RangeThresholdM,
            RangeThresholdText = def.RangeThresholdText
        };

    private static CodeParameter CloneParameter(CodeParameter p)
        => new()
        {
            Name = p.Name,
            Type = p.Type,
            AllowedValues = p.AllowedValues is null ? null : new List<string>(p.AllowedValues),
            Unit = p.Unit,
            Required = p.Required
        };

    private static string NormalizeCode(string? code)
        => (code ?? string.Empty).Trim();

    private static List<CodeDefinition> NormalizeCodes(IReadOnlyList<CodeDefinition> codes)
        => (codes ?? Array.Empty<CodeDefinition>())
            .Where(c => !string.IsNullOrWhiteSpace(c.Code))
            .Select(c => new CodeDefinition
            {
                Code = NormalizeCode(c.Code),
                Title = c.Title?.Trim() ?? string.Empty,
                Group = c.Group?.Trim() ?? "Unbekannt",
                CategoryPath = (c.CategoryPath ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList(),
                Description = c.Description?.Trim(),
                Parameters = c.Parameters ?? new List<CodeParameter>(),
                RequiresRange = c.RequiresRange,
                RangeThresholdM = c.RangeThresholdM,
                RangeThresholdText = c.RangeThresholdText
            })
            .ToList();

    private static string SafeCodeLabel(string code, int row)
        => string.IsNullOrWhiteSpace(code) ? $"Eintrag #{row}" : code;

    private void AddOrUpdate(CodeDefinition def, Dictionary<string, int> counts)
    {
        if (def is null || string.IsNullOrWhiteSpace(def.Code))
            return;

        var code = NormalizeCode(def.Code);
        if (code.Length == 0)
            return;

        def.Code = code;
        counts[code] = counts.TryGetValue(code, out var count) ? count + 1 : 1;

        if (_byCode.TryGetValue(code, out var existing))
        {
            var chosen = ChoosePreferred(existing, def);
            if (!ReferenceEquals(chosen, existing))
                _byCode[code] = chosen;
            return;
        }

        _byCode[code] = def;
    }

    private void FinalizeLoad(Dictionary<string, int> counts)
    {
        _codes.Clear();
        _codes.AddRange(_byCode.Values);
        _codes.Sort((a, b) => string.Compare(a.Code, b.Code, StringComparison.OrdinalIgnoreCase));

        LastLoadWarnings = counts
            .Where(k => k.Value > 1)
            .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .Select(k => $"Duplikat-Code '{k.Key}' ({k.Value}x)")
            .ToList();
    }

    private static CodeDefinition ChoosePreferred(CodeDefinition first, CodeDefinition second)
    {
        var scoreFirst = Score(first);
        var scoreSecond = Score(second);
        if (scoreSecond > scoreFirst)
            return second;
        if (scoreFirst > scoreSecond)
            return first;

        var descFirst = first.Description?.Length ?? 0;
        var descSecond = second.Description?.Length ?? 0;
        if (descSecond > descFirst)
            return second;
        if (descFirst > descSecond)
            return first;

        return first;
    }

    private static int Score(CodeDefinition def)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(def.Title) && !string.Equals(def.Title, def.Code, StringComparison.OrdinalIgnoreCase))
            score += 3;
        if (!string.IsNullOrWhiteSpace(def.Description))
            score += 2;
        if (!string.IsNullOrWhiteSpace(def.Group) && !string.Equals(def.Group, "Unbekannt", StringComparison.OrdinalIgnoreCase))
            score += 1;
        score += Math.Min(def.CategoryPath?.Count ?? 0, 3);
        score += Math.Min(def.Parameters?.Count ?? 0, 3);
        if (def.RequiresRange)
            score += 1;
        if (def.RangeThresholdM is not null)
            score += 1;
        if (!string.IsNullOrWhiteSpace(def.RangeThresholdText))
            score += 1;
        return score;
    }

    private sealed class CatalogNavItem
    {
        public string Id { get; set; } = string.Empty;
        public string? ParentId { get; set; }
        public string? Code { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    private sealed class PlaceholderInfo
    {
        public string Placeholder { get; set; } = string.Empty;
        public string DataType { get; set; } = "String";
        public string PlaceholderType { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public string? Unit { get; set; }
    }

    private sealed class FallbackText
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}

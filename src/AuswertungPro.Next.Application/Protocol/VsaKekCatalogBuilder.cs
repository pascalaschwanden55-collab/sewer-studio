using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AuswertungPro.Next.Application.Protocol;

public static class VsaKekCatalogSources
{
    public const string Ili = "VSA-KEK-2020-ILI";
    public const string Icm = "VSA-KEK-2020-ICM";
    public const string Heading = "VSA-KEK-2020-Heading";
    public const string XtfObserved = "VSA-XTF-Observed";
    public const string WinCanFallback = "WinCan-Fallback";
}

public static partial class VsaKekCatalogBuilder
{
    private const string ChannelType = "Kanal";
    private const string ManholeType = "Schacht";

    private static readonly string[] RequiredChannelQ1 =
    [
        "BAB", "BAC", "BAG", "BAI", "BAJ", "BBA", "BBB", "BBC", "BCA", "BDD"
    ];

    private static readonly string[] OptionalChannelQ2 = ["BCA"];
    private static readonly string[] RequiredManholeQ1 = ["DCA", "DCG"];
    private static readonly string[] OptionalManholeQ2 = ["DCA", "DCG"];

    private static readonly (string Code, string Title)[] OfficialChannelHeadings =
    [
        ("BAA", "Verformung"),
        ("BAD", "Defektes Mauerwerk"),
        ("BAK", "Feststellung der Innenauskleidung"),
        ("BAL", "Schadhafte Reparatur"),
        ("BAM", "Schadhafte Schweissnaht")
    ];

    public static CodeCatalogDocument Build(
        string iliText,
        string? sectionIcmText = null,
        string? manholeIcmText = null,
        IEnumerable<string>? observedXtfTexts = null)
    {
        if (string.IsNullOrWhiteSpace(iliText))
            throw new ArgumentException("ILI-Inhalt fehlt.", nameof(iliText));

        var rules = new Dictionary<string, VsaKekParameterRule>(StringComparer.OrdinalIgnoreCase);
        MergeRules(rules, ParseIcmRules(sectionIcmText, ChannelType));
        MergeRules(rules, ParseIcmRules(manholeIcmText, ManholeType));
        ApplyFallbackQuantificationRules(rules);

        var byCode = new Dictionary<string, CodeDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ParseIliEnum(iliText, "KanalSchadencode", ChannelType))
            AddOrMerge(byCode, BuildIliDefinition(item, rules));

        foreach (var item in ParseIliEnum(iliText, "SchachtSchadencode", ManholeType))
            AddOrMerge(byCode, BuildIliDefinition(item, rules));

        AddOfficialHeadingDefinitions(byCode);
        AddRuleOnlyDefinitions(byCode, rules);
        AddObservedXtfCodes(byCode, observedXtfTexts);

        return new CodeCatalogDocument
        {
            Version = 1,
            Codes = byCode.Values
                .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static CodeDefinition BuildIliDefinition(IliCodeItem item, IReadOnlyDictionary<string, VsaKekParameterRule> rules)
    {
        var canonicalCode = ResolveCanonicalCode(item.Code);
        var baseCode = ResolveBaseCode(item.Code, canonicalCode);
        var title = CleanTitle(item.Title, item.Code);
        var standardAnnotation = ResolveStandardAnnotation(item.Code, item.Title, out title);

        var def = new CodeDefinition
        {
            Code = item.Code,
            Title = title,
            CanonicalCode = canonicalCode,
            Source = VsaKekCatalogSources.Ili,
            IsSelectable = true,
            StandardAnnotation = standardAnnotation,
            Group = $"VSA-KEK 2020/{item.ObjectType}/{baseCode}",
            CategoryPath = ["VSA-KEK 2020", item.ObjectType, baseCode],
            Description = item.Title
        };

        ApplyParameters(def, rules);
        return def;
    }

    private static void AddRuleOnlyDefinitions(
        IDictionary<string, CodeDefinition> byCode,
        IReadOnlyDictionary<string, VsaKekParameterRule> rules)
    {
        foreach (var rule in rules.Values.OrderBy(r => r.BaseCode, StringComparer.OrdinalIgnoreCase))
        {
            if (byCode.ContainsKey(rule.BaseCode))
                continue;

            var title = rule.BaseCode;
            if (string.Equals(rule.BaseCode, "BAG", StringComparison.OrdinalIgnoreCase)
                && byCode.TryGetValue("BAGA", out var baga)
                && !string.IsNullOrWhiteSpace(baga.Title))
            {
                title = baga.Title;
            }

            var def = new CodeDefinition
            {
                Code = rule.BaseCode,
                Title = title,
                CanonicalCode = rule.BaseCode,
                Source = VsaKekCatalogSources.Icm,
                IsSelectable = !string.Equals(rule.BaseCode, "BAG", StringComparison.OrdinalIgnoreCase),
                Group = $"VSA-KEK 2020/{rule.ObjectType}/{rule.BaseCode}",
                CategoryPath = ["VSA-KEK 2020", rule.ObjectType, rule.BaseCode],
                Description = "Regelbasis aus VSA-KEK 2020 ICM-Mapping."
            };
            ApplyParameters(def, rules);
            AddOrMerge(byCode, def);
        }
    }

    private static void AddOfficialHeadingDefinitions(IDictionary<string, CodeDefinition> byCode)
    {
        foreach (var (code, title) in OfficialChannelHeadings)
        {
            if (byCode.ContainsKey(code))
                continue;

            AddOrMerge(byCode, new CodeDefinition
            {
                Code = code,
                Title = title,
                CanonicalCode = code,
                Source = VsaKekCatalogSources.Heading,
                IsSelectable = true,
                Group = $"VSA-KEK 2020/{ChannelType}/{code}",
                CategoryPath = ["VSA-KEK 2020", ChannelType, code],
                Description = "Offizielle VSA-KEK-2020 Basisgruppe."
            });
        }
    }

    private static void AddObservedXtfCodes(
        IDictionary<string, CodeDefinition> byCode,
        IEnumerable<string>? observedXtfTexts)
    {
        if (observedXtfTexts is null)
            return;

        foreach (var xtf in observedXtfTexts)
        {
            if (string.IsNullOrWhiteSpace(xtf))
                continue;

            foreach (Match match in XtfCodeRegex().Matches(xtf))
            {
                var element = match.Groups["element"].Value;
                var objectType = element.Contains("Schacht", StringComparison.OrdinalIgnoreCase)
                    ? ManholeType
                    : ChannelType;
                var code = NormalizeCode(match.Groups["code"].Value);
                if (code.Length == 0 || byCode.ContainsKey(code))
                    continue;

                var baseCode = code.Length >= 3 ? code[..3] : code;
                AddOrMerge(byCode, new CodeDefinition
                {
                    Code = code,
                    Title = $"Beobachteter XTF-Code {code}",
                    CanonicalCode = code,
                    Source = VsaKekCatalogSources.XtfObserved,
                    IsObservedExtension = true,
                    IsSelectable = false,
                    Group = $"VSA-XTF-Observed/{objectType}/{baseCode}",
                    CategoryPath = ["VSA-XTF-Observed", objectType, baseCode],
                    Description = "Im XTF beobachtet, aber nicht in der VSA-KEK-2020-ILI-Code-Liste enthalten."
                });
            }
        }
    }

    private static void AddOrMerge(IDictionary<string, CodeDefinition> byCode, CodeDefinition def)
    {
        if (string.IsNullOrWhiteSpace(def.Code))
            return;

        def.Code = NormalizeCode(def.Code);
        def.CanonicalCode = string.IsNullOrWhiteSpace(def.CanonicalCode)
            ? def.Code
            : NormalizeCode(def.CanonicalCode);

        if (!byCode.TryGetValue(def.Code, out var existing))
        {
            byCode[def.Code] = def;
            return;
        }

        if (string.Equals(existing.Source, VsaKekCatalogSources.Ili, StringComparison.OrdinalIgnoreCase))
            return;

        byCode[def.Code] = def;
    }

    private static IEnumerable<IliCodeItem> ParseIliEnum(string iliText, string enumName, string objectType)
    {
        var start = iliText.IndexOf(enumName, StringComparison.Ordinal);
        if (start < 0)
            yield break;

        var open = iliText.IndexOf('(', start);
        if (open < 0)
            yield break;

        using var reader = new StringReader(iliText[open..]);
        string? lastComment = null;
        var inEnum = false;
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (!inEnum)
            {
                inEnum = trimmed.Contains('(');
                continue;
            }

            if (trimmed.StartsWith(");", StringComparison.Ordinal))
                yield break;

            var comment = ParseIliComment(trimmed);
            if (!string.IsNullOrWhiteSpace(comment))
            {
                lastComment = comment;
                continue;
            }

            var match = IliCodeRegex().Match(trimmed);
            if (!match.Success)
                continue;

            var code = NormalizeCode(match.Groups["code"].Value);
            if (code.Length == 0)
                continue;

            yield return new IliCodeItem(code, CleanTitle(lastComment, code), objectType);
            lastComment = null;
        }
    }

    private static string? ParseIliComment(string line)
    {
        var idx = line.IndexOf("comment", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var firstQuote = line.IndexOf('"', idx);
        if (firstQuote < 0)
            return null;

        var lastQuote = line.LastIndexOf('"');
        if (lastQuote <= firstQuote)
            lastQuote = line.Length;

        return line[(firstQuote + 1)..lastQuote].Trim();
    }

    private static IEnumerable<VsaKekParameterRule> ParseIcmRules(string? icmText, string objectType)
    {
        if (string.IsNullOrWhiteSpace(icmText))
            yield break;

        XDocument doc;
        try
        {
            doc = XDocument.Parse(icmText);
        }
        catch
        {
            yield break;
        }

        foreach (var station in doc.Descendants().Where(e => e.Name.LocalName == "artistStation"))
        {
            var codes = station.Descendants()
                .Where(e => e.Name.LocalName == "setAttribute"
                            && string.Equals((string?)e.Attribute("name"), "Code", StringComparison.OrdinalIgnoreCase))
                .Select(e => NormalizeCode((string?)e.Attribute("toValue")))
                .Where(c => c.Length > 0)
                .Select(c => c.Length >= 3 ? c[..3] : c)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (codes.Count == 0)
                continue;

            var q1 = ResolveParameterPresence(station, "Q1");
            var q2 = ResolveParameterPresence(station, "Q2");
            var hasPosition = station.Descendants().Any(e =>
                e.Name.LocalName == "setPosition"
                || (e.Name.LocalName == "setAttribute"
                    && (((string?)e.Attribute("name"))?.Equals("Pos1", StringComparison.OrdinalIgnoreCase) == true
                        || ((string?)e.Attribute("name"))?.Equals("Pos2", StringComparison.OrdinalIgnoreCase) == true)));
            var hasConnection = station.Descendants().Any(e =>
                e.Name.LocalName == "setAttribute"
                && ((string?)e.Attribute("name"))?.Equals("Connection", StringComparison.OrdinalIgnoreCase) == true);

            foreach (var code in codes)
            {
                yield return new VsaKekParameterRule(
                    BaseCode: code,
                    ObjectType: objectType,
                    Q1: q1,
                    Q2: q2,
                    HasPosition: hasPosition,
                    HasConnection: hasConnection);
            }
        }
    }

    private static VsaKekRulePresence ResolveParameterPresence(XElement station, string key)
    {
        var elements = station.Descendants().Where(e =>
            (e.Name.LocalName == "setAttribute" && string.Equals((string?)e.Attribute("name"), key, StringComparison.OrdinalIgnoreCase))
            || (e.Name.LocalName == "map" && string.Equals((string?)e.Attribute("toAttribute"), key, StringComparison.OrdinalIgnoreCase)));

        var list = elements.ToList();
        if (list.Count == 0)
            return VsaKekRulePresence.None;

        return list.All(e => string.Equals((string?)e.Attribute("isOptional"), "true", StringComparison.OrdinalIgnoreCase))
            ? VsaKekRulePresence.Optional
            : VsaKekRulePresence.Required;
    }

    private static void ApplyFallbackQuantificationRules(IDictionary<string, VsaKekParameterRule> rules)
    {
        foreach (var code in RequiredChannelQ1)
            MergeRule(rules, new VsaKekParameterRule(code, ChannelType, VsaKekRulePresence.Required, VsaKekRulePresence.None, false, false));

        foreach (var code in OptionalChannelQ2)
            MergeRule(rules, new VsaKekParameterRule(code, ChannelType, VsaKekRulePresence.None, VsaKekRulePresence.Optional, false, false));

        foreach (var code in RequiredManholeQ1)
            MergeRule(rules, new VsaKekParameterRule(code, ManholeType, VsaKekRulePresence.Required, VsaKekRulePresence.None, false, false));

        foreach (var code in OptionalManholeQ2)
            MergeRule(rules, new VsaKekParameterRule(code, ManholeType, VsaKekRulePresence.None, VsaKekRulePresence.Optional, false, false));
    }

    private static void MergeRules(
        IDictionary<string, VsaKekParameterRule> target,
        IEnumerable<VsaKekParameterRule> source)
    {
        foreach (var rule in source)
            MergeRule(target, rule);
    }

    private static void MergeRule(IDictionary<string, VsaKekParameterRule> target, VsaKekParameterRule rule)
    {
        if (!target.TryGetValue(rule.BaseCode, out var existing))
        {
            target[rule.BaseCode] = rule;
            return;
        }

        target[rule.BaseCode] = new VsaKekParameterRule(
            rule.BaseCode,
            string.Equals(existing.ObjectType, ManholeType, StringComparison.OrdinalIgnoreCase)
                ? existing.ObjectType
                : rule.ObjectType,
            MergePresence(existing.Q1, rule.Q1),
            MergePresence(existing.Q2, rule.Q2),
            existing.HasPosition || rule.HasPosition,
            existing.HasConnection || rule.HasConnection);
    }

    private static VsaKekRulePresence MergePresence(VsaKekRulePresence left, VsaKekRulePresence right)
    {
        if (left == VsaKekRulePresence.Required || right == VsaKekRulePresence.Required)
            return VsaKekRulePresence.Required;
        if (left == VsaKekRulePresence.Optional || right == VsaKekRulePresence.Optional)
            return VsaKekRulePresence.Optional;
        return VsaKekRulePresence.None;
    }

    private static void ApplyParameters(CodeDefinition def, IReadOnlyDictionary<string, VsaKekParameterRule> rules)
    {
        var baseCode = ResolveBaseCode(def.Code, def.CanonicalCode);
        if (!rules.TryGetValue(baseCode, out var rule))
            return;

        AddQuantification(def, "Quantifizierung 1", "Q1", rule.Q1);
        AddQuantification(def, "Quantifizierung 2", "Q2", rule.Q2);

        if (rule.HasPosition)
        {
            AddParameter(def, new CodeParameter
            {
                Name = "Uhrlage Anfang",
                DataKey = "SchadenlageAnfang",
                Type = "clock",
                Required = false
            });
            AddParameter(def, new CodeParameter
            {
                Name = "Uhrlage Ende",
                DataKey = "SchadenlageEnde",
                Type = "clock",
                Required = false
            });
        }

        if (rule.HasConnection)
        {
            AddParameter(def, new CodeParameter
            {
                Name = "Verbindung",
                DataKey = "Connection",
                Type = "string",
                Required = false
            });
        }
    }

    private static void AddQuantification(CodeDefinition def, string name, string dataKey, VsaKekRulePresence presence)
    {
        if (presence == VsaKekRulePresence.None)
            return;

        AddParameter(def, new CodeParameter
        {
            Name = name,
            DataKey = dataKey,
            Type = "number",
            Required = presence == VsaKekRulePresence.Required
        });
    }

    private static void AddParameter(CodeDefinition def, CodeParameter parameter)
    {
        if (def.Parameters.Any(p =>
                string.Equals(p.DataKey, parameter.DataKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        def.Parameters.Add(parameter);
    }

    private static string ResolveCanonicalCode(string code)
    {
        code = NormalizeCode(code);
        if (string.Equals(code, "BAGA", StringComparison.OrdinalIgnoreCase))
            return "BAG";
        if (code.StartsWith("BDB", StringComparison.OrdinalIgnoreCase) && code.Length > 3)
            return "BDB";
        return code;
    }

    private static string ResolveBaseCode(string code, string? canonicalCode)
    {
        var canonical = NormalizeCode(canonicalCode);
        if (canonical.Length >= 3)
            return canonical[..3];

        code = NormalizeCode(code);
        return code.Length >= 3 ? code[..3] : code;
    }

    private static string? ResolveStandardAnnotation(string code, string title, out string cleanedTitle)
    {
        cleanedTitle = title;
        code = NormalizeCode(code);
        if (!code.StartsWith("BDB", StringComparison.OrdinalIgnoreCase) || code.Length <= 3)
            return null;

        var annotation = code[3..];
        var prefix = annotation + " ";
        if (cleanedTitle.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            cleanedTitle = cleanedTitle[prefix.Length..].Trim();

        return annotation;
    }

    private static string CleanTitle(string? raw, string fallback)
    {
        var title = (raw ?? string.Empty).Trim();
        if (title.Length == 0 || string.Equals(title, "<kein Text>", StringComparison.OrdinalIgnoreCase))
            return NormalizeCode(fallback);

        title = WhitespaceRegex().Replace(title, " ");
        return title.Trim();
    }

    private static string NormalizeCode(string? code)
        => (code ?? string.Empty).Trim().ToUpperInvariant();

    [GeneratedRegex(@"^(?<code>[A-Z]{2,6})\s*,")]
    private static partial Regex IliCodeRegex();

    [GeneratedRegex(@"<(?<element>KanalSchadencode|SchachtSchadencode)>(?<code>[^<]+)</\k<element>>", RegexOptions.IgnoreCase)]
    private static partial Regex XtfCodeRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed record IliCodeItem(string Code, string Title, string ObjectType);

    private sealed record VsaKekParameterRule(
        string BaseCode,
        string ObjectType,
        VsaKekRulePresence Q1,
        VsaKekRulePresence Q2,
        bool HasPosition,
        bool HasConnection);

    private enum VsaKekRulePresence
    {
        None,
        Optional,
        Required
    }
}

public static class VsaKekCatalogArchiveReader
{
    public const string IliEntryName =
        "Bin/Data/Export/CSharp/Interlis/models23_2020_LV95_update2021/VSA_KEK_2020_2_d_LV95-20210503.ili";

    public const string SectionIcmEntryName =
        "Bin/Data/Import/ArtIST/VSA_RL3_2019.icm.xml";

    public const string ManholeIcmEntryName =
        "Bin/Data/Import/ArtIST/VSA_RL3_2019_SCHACHT.icm.xml";

    public static string ReadTextEntry(string archivePath, string entryName)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            throw new ArgumentException("Archivpfad fehlt.", nameof(archivePath));
        if (!File.Exists(archivePath))
            throw new FileNotFoundException($"VSA-KEK-2020-Archiv nicht gefunden: {archivePath}", archivePath);
        if (string.IsNullOrWhiteSpace(entryName))
            throw new ArgumentException("Archiveintrag fehlt.", nameof(entryName));

        var psi = new ProcessStartInfo
        {
            FileName = "tar",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-xOf");
        psi.ArgumentList.Add(archivePath);
        psi.ArgumentList.Add(entryName);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("tar konnte nicht gestartet werden.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Archiveintrag konnte nicht gelesen werden: {entryName}. {error}");

        return output;
    }
}

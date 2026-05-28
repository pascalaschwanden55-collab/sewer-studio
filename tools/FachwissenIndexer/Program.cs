using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;

var options = IndexOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine(IndexOptions.HelpText);
    return 0;
}

if (!Directory.Exists(options.SourceRoot))
{
    Console.Error.WriteLine($"Source folder not found: {options.SourceRoot}");
    return 2;
}

Directory.CreateDirectory(options.OutputRoot);

var files = FachwissenScanner.FindFiles(options).ToList();
if (files.Count == 0)
{
    Console.WriteLine("No supported files found.");
    return 0;
}

var documents = new List<IndexedDocument>();
var chunks = new List<KnowledgeChunk>();

foreach (var file in files)
{
    var doc = DocumentExtractor.Extract(file, options.SourceRoot);
    documents.Add(doc);

    if (doc.Status == "ok")
    {
        var docChunks = Chunker.Split(doc.Text, options.ChunkChars)
            .Select((text, index) => KnowledgeChunk.Create(doc, index + 1, text))
            .ToList();
        chunks.AddRange(docChunks);
    }

    Console.WriteLine($"{doc.Status.ToUpperInvariant(),-5} {doc.Topic,-24} {doc.RelativePath} ({doc.TextLength} chars)");
}

var rules = RuleExtractor.Extract(documents);
var glossary = GlossarySeed.Create();
var sourcePolicy = SourcePolicy.Create();

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
var jsonlOptions = new JsonSerializerOptions
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

WriteJson(Path.Combine(options.OutputRoot, "manifest.json"), new FachwissenManifest(
    CreatedUtc: DateTimeOffset.UtcNow,
    SourceRoot: Path.GetFullPath(options.SourceRoot),
    OutputRoot: Path.GetFullPath(options.OutputRoot),
    FocusOnly: options.FocusOnly,
    DocumentCount: documents.Count,
    ChunkCount: chunks.Count,
    RuleCount: rules.Count,
    Documents: documents.Select(d => d.ToManifestItem()).ToList()));

WriteJson(Path.Combine(options.OutputRoot, "source_policy.json"), sourcePolicy);
WriteJson(Path.Combine(options.OutputRoot, "rules.seed.json"), rules);
WriteJson(Path.Combine(options.OutputRoot, "glossary.seed.json"), glossary);
WriteJsonl(Path.Combine(options.OutputRoot, "chunks.jsonl"), chunks);
WriteReadme(Path.Combine(options.OutputRoot, "README.md"), options, documents, chunks, rules);

Console.WriteLine();
Console.WriteLine($"Documents: {documents.Count}");
Console.WriteLine($"Chunks:    {chunks.Count}");
Console.WriteLine($"Rules:     {rules.Count}");
Console.WriteLine($"Output:    {Path.GetFullPath(options.OutputRoot)}");

return 0;

void WriteJson<T>(string path, T value)
{
    File.WriteAllText(path, JsonSerializer.Serialize(value, jsonOptions), new UTF8Encoding(false));
}

void WriteJsonl<T>(string path, IReadOnlyList<T> values)
{
    using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
    foreach (var value in values)
    {
        writer.WriteLine(JsonSerializer.Serialize(value, jsonlOptions));
    }
}

static void WriteReadme(
    string path,
    IndexOptions options,
    IReadOnlyList<IndexedDocument> documents,
    IReadOnlyList<KnowledgeChunk> chunks,
    IReadOnlyList<FachwissenRule> rules)
{
    var ok = documents.Count(d => d.Status == "ok");
    var poor = documents.Count(d => d.ExtractionQuality == "poor");
    var empty = documents.Count(d => d.Status == "empty");
    var errors = documents.Count(d => d.Status == "error");
    var nonAuthoritativeCodeDocs = documents.Count(d => d.Authority == SourceAuthority.NonAuthoritativeCodeReference);

    var sb = new StringBuilder();
    sb.AppendLine("# Fachwissen-Index");
    sb.AppendLine();
    sb.AppendLine("Dieser Ordner wird vom `tools/FachwissenIndexer` erzeugt.");
    sb.AppendLine("Er ist die lokale, nachvollziehbare Vorstufe fuer RAG und spaeteres KI-Training.");
    sb.AppendLine();
    sb.AppendLine("## Stand");
    sb.AppendLine();
    sb.AppendLine($"- Quelle: `{Path.GetFullPath(options.SourceRoot)}`");
    sb.AppendLine($"- Fokusmodus: `{options.FocusOnly}`");
    sb.AppendLine($"- Dokumente gelesen: `{documents.Count}`");
    sb.AppendLine($"- Erfolgreich extrahiert: `{ok}`");
    sb.AppendLine($"- Schwache Extraktion: `{poor}`");
    sb.AppendLine($"- Leer/nicht extrahiert: `{empty}`");
    sb.AppendLine($"- Fehler: `{errors}`");
    sb.AppendLine($"- Nicht als Code-Quelle genutzt: `{nonAuthoritativeCodeDocs}`");
    sb.AppendLine($"- Text-Chunks: `{chunks.Count}`");
    sb.AppendLine($"- Seed-Regeln: `{rules.Count}`");
    sb.AppendLine();
    sb.AppendLine("## Dateien");
    sb.AppendLine();
    sb.AppendLine("- `manifest.json`: Dokumentliste mit Quelle, Thema, Qualitaet und Status");
    sb.AppendLine("- `source_policy.json`: Quellenregel, welche Dateien als Code-Quelle gelten duerfen");
    sb.AppendLine("- `chunks.jsonl`: Textabschnitte fuer Retrieval/RAG");
    sb.AppendLine("- `rules.seed.json`: erste fachliche Regeln aus den Dokumenten");
    sb.AppendLine("- `glossary.seed.json`: Start-Glossar fuer Kanal-TV und Zustandsbeurteilung");
    sb.AppendLine();
    sb.AppendLine("## Naechster technischer Schritt");
    sb.AppendLine();
    sb.AppendLine("Die Chunks koennen in eine eigene Fachwissen-Retrieval-Tabelle oder einen Vektorindex uebernommen werden.");
    sb.AppendLine("Die bestehende `KnowledgeBase.db` ist aktuell primaer fuer verifizierte Trainingssamples ausgelegt, nicht fuer ganze Fachtexte.");
    sb.AppendLine();
    sb.AppendLine("## Wichtige Abgrenzung");
    sb.AppendLine();
    sb.AppendLine("Schadencodierungs-PDFs in diesem Import sind nicht fuehrend fuer aktuelle VSA-Codes.");
    sb.AppendLine("Sie werden nur als Kontext fuer Aufbau, Ablauf und Datenabgabe einer Aufnahme verwendet.");
    sb.AppendLine("Massgeblich fuer Codes bleiben die implementierten Kataloge im Programm.");
    File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
}

public sealed record IndexOptions(
    string SourceRoot,
    string OutputRoot,
    bool FocusOnly,
    int MaxDocuments,
    int ChunkChars,
    bool ShowHelp)
{
    public static string HelpText => """
        FachwissenIndexer

        Usage:
          dotnet run --project tools/FachwissenIndexer/FachwissenIndexer.csproj -- [options]

        Options:
          --source <path>       Source folder. Default: D:\Fachwissen
          --output <path>       Output folder. Default: Knowledge\fachwissen
          --all                 Process all supported documents, not only Kanal-TV focus folders.
          --max-docs <number>   Limit document count. Default: 0 (no limit)
          --chunk-chars <num>   Approximate chunk size. Default: 1800
          --help                Show help.
        """;

    public static IndexOptions Parse(string[] args)
    {
        var source = @"D:\Fachwissen";
        var output = Path.Combine("Knowledge", "fachwissen");
        var focusOnly = true;
        var maxDocs = 0;
        var chunkChars = 1800;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--source" when i + 1 < args.Length:
                    source = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--all":
                    focusOnly = false;
                    break;
                case "--max-docs" when i + 1 < args.Length:
                    maxDocs = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--chunk-chars" when i + 1 < args.Length:
                    chunkChars = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--help":
                case "-h":
                case "/?":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown or incomplete argument: {arg}");
            }
        }

        return new IndexOptions(source, output, focusOnly, maxDocs, Math.Max(500, chunkChars), showHelp);
    }
}

public static class FachwissenScanner
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".md", ".txt", ".yaml", ".yml", ".json", ".csv", ".html", ".htm"
    };

    private static readonly string[] FocusFolders =
    [
        "2_1_4_7_3 Kanal-TV",
        "2_1_4_7_4 Zustandsbeurteilung",
        "2_1_4_7_8 Erfassungsrichtlinien"
    ];

    public static IEnumerable<FileInfo> FindFiles(IndexOptions options)
    {
        var root = new DirectoryInfo(options.SourceRoot);
        var query = root.EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(f.Extension))
            .Where(f => !f.Name.StartsWith("~$", StringComparison.Ordinal))
            .Where(f => !f.Attributes.HasFlag(FileAttributes.System))
            .Where(f => !options.FocusOnly || IsInFocusFolder(root.FullName, f.FullName))
            .OrderBy(f => TopicFromPath(root.FullName, f.FullName), StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.FullName, StringComparer.OrdinalIgnoreCase);

        return options.MaxDocuments > 0 ? query.Take(options.MaxDocuments) : query;
    }

    public static string TopicFromPath(string sourceRoot, string path)
    {
        var relative = Path.GetRelativePath(sourceRoot, path);
        var first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? "root" : first;
    }

    private static bool IsInFocusFolder(string sourceRoot, string path)
    {
        var relative = Path.GetRelativePath(sourceRoot, path);
        return FocusFolders.Any(folder => relative.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
    }
}

public static class DocumentExtractor
{
    public static IndexedDocument Extract(FileInfo file, string sourceRoot)
    {
        var relative = Path.GetRelativePath(sourceRoot, file.FullName);
        var topic = FachwissenScanner.TopicFromPath(sourceRoot, file.FullName);
        var docId = CreateDocumentId(relative);

        try
        {
            var text = file.Extension.ToLowerInvariant() switch
            {
                ".pdf" => ExtractPdf(file.FullName),
                ".docx" => ExtractDocx(file.FullName),
                ".html" or ".htm" => StripHtml(File.ReadAllText(file.FullName, Encoding.UTF8)),
                _ => File.ReadAllText(file.FullName, Encoding.UTF8)
            };

            text = TextNormalizer.Normalize(text);
            var quality = EstimateQuality(text, file.Extension);
            var status = text.Trim().Length == 0 ? "empty" : "ok";

            return new IndexedDocument(
                DocumentId: docId,
                Title: Path.GetFileNameWithoutExtension(file.Name),
                SourcePath: file.FullName,
                RelativePath: relative,
                Extension: file.Extension,
                Topic: topic,
                Authority: SourceClassifier.GetAuthority(relative),
                IntendedUse: SourceClassifier.GetIntendedUse(relative),
                Bytes: file.Length,
                LastWriteTimeUtc: file.LastWriteTimeUtc,
                Status: status,
                ExtractionQuality: quality,
                Message: status == "empty" ? "No text extracted." : null,
                Text: text);
        }
        catch (Exception ex)
        {
            return new IndexedDocument(
                DocumentId: docId,
                Title: Path.GetFileNameWithoutExtension(file.Name),
                SourcePath: file.FullName,
                RelativePath: relative,
                Extension: file.Extension,
                Topic: topic,
                Authority: SourceClassifier.GetAuthority(relative),
                IntendedUse: SourceClassifier.GetIntendedUse(relative),
                Bytes: file.Length,
                LastWriteTimeUtc: file.LastWriteTimeUtc,
                Status: "error",
                ExtractionQuality: "failed",
                Message: $"{ex.GetType().Name}: {ex.Message}",
                Text: "");
        }
    }

    private static string ExtractPdf(string path)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(path);
        foreach (var page in doc.GetPages())
        {
            var text = page.Text;
            if (string.IsNullOrWhiteSpace(text))
                continue;

            sb.AppendLine($"[page {page.Number}]");
            sb.AppendLine(text);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string ExtractDocx(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var sb = new StringBuilder();
        foreach (var partName in new[] { "word/document.xml", "word/header1.xml", "word/footer1.xml" })
        {
            var entry = archive.GetEntry(partName);
            if (entry is null)
                continue;

            using var stream = entry.Open();
            var xml = XDocument.Load(stream);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

            foreach (var paragraph in xml.Descendants(w + "p"))
            {
                var text = string.Concat(paragraph.Descendants(w + "t").Select(t => t.Value));
                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine(text.Trim());
            }
        }
        return sb.ToString();
    }

    private static string StripHtml(string text)
    {
        text = Regex.Replace(text, "<script[\\s\\S]*?</script>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", " ");
        return System.Net.WebUtility.HtmlDecode(text);
    }

    private static string EstimateQuality(string text, string extension)
    {
        var trimmed = text.Trim();
        if (trimmed.Length < 200)
            return "poor";

        var letterCount = trimmed.Count(char.IsLetter);
        var ratio = (double)letterCount / Math.Max(1, trimmed.Length);
        if (ratio < 0.35)
            return "mixed";

        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) && trimmed.Length < 800)
            return "poor";

        return "good";
    }

    private static string CreateDocumentId(string relativePath)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(relativePath.ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}

public static class TextNormalizer
{
    private static readonly (string Bad, string Good)[] Replacements =
    [
        ("\u00c3\u00a4", "\u00e4"),
        ("\u00c3\u00b6", "\u00f6"),
        ("\u00c3\u00bc", "\u00fc"),
        ("\u00c3\u201e", "\u00c4"),
        ("\u00c3\u2013", "\u00d6"),
        ("\u00c3\u0153", "\u00dc"),
        ("\u00c3\u0178", "\u00df"),
        ("\u00c2\u00ab", "\""),
        ("\u00c2\u00bb", "\""),
        ("\u00c2\u00b0", "\u00b0"),
        ("\u00e2\u20ac\u201c", "-"),
        ("\u00e2\u20ac\u201d", "-"),
        ("\u00e2\u20ac\u2122", "'"),
        ("\u00e2\u20ac\u017e", "\""),
        ("\u00e2\u20ac\u0153", "\""),
        ("\u00e2\u20ac\u009d", "\""),
        ("\u00e2\u2013\u00aa", "-")
    ];

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        foreach (var (bad, good) in Replacements)
            text = text.Replace(bad, good, StringComparison.Ordinal);

        text = text.Replace('\u00a0', ' ');
        text = Regex.Replace(text, "[ \\t]+", " ");
        text = Regex.Replace(text, "\\r\\n|\\r", "\n");
        text = Regex.Replace(text, "\\n{3,}", "\n\n");
        return text.Trim();
    }
}

public static class SourceClassifier
{
    public static string GetAuthority(string relativePath)
    {
        var p = NormalizePath(relativePath);
        if (p.Contains("schadencodierung", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("datentransfervernehmlassung", StringComparison.OrdinalIgnoreCase))
        {
            return SourceAuthority.NonAuthoritativeCodeReference;
        }

        return SourceAuthority.SupportingFachwissen;
    }

    public static string GetIntendedUse(string relativePath)
    {
        var p = NormalizePath(relativePath);
        if (p.Contains("schadencodierung", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("datentransfervernehmlassung", StringComparison.OrdinalIgnoreCase))
        {
            return "Nur Kontext fuer Aufbau, Ablauf, Datenabgabe und historische Struktur einer Aufnahme; nicht fuer aktuelle Schadenscode-Definitionen verwenden.";
        }

        if (p.Contains("zustandsbeurteilung", StringComparison.OrdinalIgnoreCase))
            return "Kontext fuer Zustandsbeurteilung und Plausibilitaet.";

        if (p.Contains("erfassungsrichtlinien", StringComparison.OrdinalIgnoreCase))
            return "Kontext fuer Erfassungsregeln, Aufnahmevorgaben und Datenabgabe.";

        if (p.Contains("kanal-tv", StringComparison.OrdinalIgnoreCase))
            return "Kontext fuer Ablauf und Technik der Kanalfernsehaufnahme.";

        return "Allgemeines Fachwissen.";
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/');
}

public static class SourceAuthority
{
    public const string SupportingFachwissen = "supporting_fachwissen";
    public const string NonAuthoritativeCodeReference = "non_authoritative_for_current_codes";
}

public static class Chunker
{
    public static IReadOnlyList<string> Split(string text, int chunkChars)
    {
        var paragraphs = Regex.Split(text, "\\n{2,}|(?<=\\.)\\n")
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);

        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length > chunkChars)
            {
                Flush();
                foreach (var piece in SplitLong(paragraph, chunkChars))
                    chunks.Add(piece);
                continue;
            }

            if (current.Length > 0 && current.Length + paragraph.Length + 2 > chunkChars)
                Flush();

            if (current.Length > 0)
                current.AppendLine().AppendLine();
            current.Append(paragraph);
        }

        Flush();
        return chunks;

        void Flush()
        {
            if (current.Length == 0)
                return;

            chunks.Add(current.ToString().Trim());
            current.Clear();
        }
    }

    private static IEnumerable<string> SplitLong(string text, int chunkChars)
    {
        for (var offset = 0; offset < text.Length; offset += chunkChars)
        {
            var len = Math.Min(chunkChars, text.Length - offset);
            yield return text.Substring(offset, len).Trim();
        }
    }
}

public static class RuleExtractor
{
    private static readonly RuleTemplate[] Templates =
    [
        new("kanaltv_datenabgabe_interlis", "Datenabgabe", "VSA-KEK und Interlis-Abgabe",
            "Nach der Inspektion sollen die strukturierten Daten digital abgegeben werden, insbesondere VSA-KEK und Interlis-Dateien mit Datenstruktur und Dateninhalt.",
            ["vsa-kek", "interlis"]),
        new("kanaltv_protokolle_einzeln", "Datenabgabe", "Protokolle pro Objekt",
            "Kanalfernsehprotokolle, Schachtprotokolle und Dichtheitspruefungsprotokolle sollen einzeln pro Haltung, Schacht oder DP abgegeben werden.",
            ["kanalfernsehprotokolle", "schachtprotokolle"]),
        new("kanaltv_inspection_viewer_normal", "Datenabgabe", "Inspektions-DB Normal-Viewer",
            "Bei DB-Abgaben soll der Normal-Viewer mitgeliefert werden, nicht nur der Light-Viewer.",
            ["inspection-viewer", "normal-viewer"]),
        new("kanaltv_gep_zonen", "GEP", "GEP-Aufnahmen nach Zonen trennen",
            "GEP-Aufnahmen sollen zonenweise als separate Projekte gefuehrt werden; Strassennamen sind pro Haltung korrekt zu erfassen.",
            ["gep", "zonen", "strassenn"]),
        new("kanaltv_rohranfang_rohrende", "Aufnahmetechnik", "Rohranfang und Rohrende dokumentieren",
            "Der Rohranfang soll moeglichst mit Video und Foto dokumentiert werden; am Rohrende soll abgeschwenkt oder ein Gegenfoto erstellt werden, damit die Verbindung zum Schacht nachvollziehbar ist.",
            ["rohranfang", "rohrende"]),
        new("kanaltv_nullpunkt_bezug", "Aufnahmetechnik", "Nullpunkt und Bezugspunkt",
            "Kanalabschnitte sind ab Rohranfang als Nullpunkt 0.00 m aufzunehmen; der Bezugspunkt fuer den Rohranfang soll definiert werden.",
            ["nullpunkt", "bezugspunkt"]),
        new("kanaltv_fahren_schwenken", "Aufnahmetechnik", "Fahren und Schwenken trennen",
            "Bei konventioneller Kanalfernsehaufnahmetechnik soll grundsaetzlich entweder gefahren oder geschwenkt werden; vor dem Weiterfahren ist in axiale Sicht und an die Ausgangsposition zurueckzukehren.",
            ["fahren", "schwenken", "axiale"]),
        new("kanaltv_anschluss_zoom", "Aufnahmetechnik", "Anschluss hineinzoomen",
            "Bei einem Anschluss soll wenn moeglich in den Anschluss hineingezoomt werden.",
            ["anschluss", "hineinzuzoomen"]),
        new("kanaltv_rohrmaterial", "Stammdaten", "Rohrmaterial korrekt erfassen",
            "Das Rohrmaterial soll korrekt angegeben werden, zum Beispiel PVC, PP oder PE.",
            ["rohrmaterial", "pvc"]),
        new("kanaltv_sanierungsabnahme_ganze_haltung", "Sanierungsabnahme", "Ganze Haltung aufnehmen",
            "Bei Sanierungsabnahmen ist die ganze Haltung abzufahren und aufzunehmen, auch wenn nur eine einzelne Stelle saniert wurde.",
            ["sanierungsabnahmen", "ganze haltung"]),
        new("kanaltv_schachtfotos", "Schacht", "Schachtfotos Mindestumfang",
            "Im Schachtprotokoll sollen mindestens Situationsfoto und Schachtfotos erstellt werden; starke Schaeden sollen separat fotografiert werden.",
            ["schachtfotos", "situationsfoto"]),
        new("kanaltv_datenqualitaet_pruefen", "Qualitaet", "Datenqualitaet pruefen",
            "Die Datenmodellkonformitaet ist vor der Abgabe zu pruefen; fachliche Qualitaet und Vollstaendigkeit sollen ebenfalls kontrolliert werden.",
            ["datenmodellkonform", "vollstaendigkeit"]),
        new("kanaltv_untersuchungsgrund", "Codierung", "Untersuchungsgrund normieren",
            "Der Untersuchungsgrund soll aus einem kontrollierten Wertebereich stammen, zum Beispiel Garantieabnahme, Neubauabnahme, Sanierungsabnahme oder Zustandskontrolle.",
            ["grund", "garantieabnahme", "zustandskontrolle"]),
        new("kanaltv_datenherrschaft", "Datenhaltung", "Datenherrschaft",
            "Die Datenherrschaft liegt bei der Standortgemeinde; die ausfuehrende Firma soll in den Untersuchungsdaten hinterlegt werden.",
            ["datenherrschaft", "standortgemeinde"])
    ];

    public static List<FachwissenRule> Extract(IReadOnlyList<IndexedDocument> documents)
    {
        var result = new List<FachwissenRule>();
        foreach (var template in Templates)
        {
            var match = documents
                .Where(d => d.Status == "ok")
                .Where(d => d.Authority != SourceAuthority.NonAuthoritativeCodeReference)
                .Select(d => new { Document = d, Score = KeywordScore(d.SearchText, template.Keywords) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Document.TextLength)
                .FirstOrDefault();

            if (match is null)
                continue;

            result.Add(new FachwissenRule(
                RuleId: template.RuleId,
                Category: template.Category,
                Title: template.Title,
                RuleText: template.RuleText,
                SourceDocumentId: match.Document.DocumentId,
                SourcePath: match.Document.SourcePath,
                EvidenceKeywords: template.Keywords,
                Confidence: match.Score >= template.Keywords.Count ? 0.9 : 0.65));
        }
        return result;
    }

    private static int KeywordScore(string text, IReadOnlyList<string> keywords)
        => keywords.Count(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}

public static class GlossarySeed
{
    public static IReadOnlyList<GlossaryTerm> Create() =>
    [
        new("Haltung", "Kanalabschnitt zwischen zwei Knoten, typischerweise zwischen zwei Schaechten.", ["Kanal-TV", "Protokoll", "Videozuordnung"]),
        new("Schacht", "Zugangsbauwerk zur Kanalisation; in der Inspektion oft Start- oder Endpunkt einer Haltung.", ["Kanal-TV", "Schachtprotokoll"]),
        new("Stationierung", "Meterposition innerhalb einer Haltung, gemessen ab definiertem Nullpunkt.", ["Video", "Protokoll", "VSA-Code"]),
        new("VSA-KEK", "Schweizer Datenmodell/Katalog fuer Kanalinspektionsdaten und Schadencodierung.", ["Datenaustausch", "Interlis"]),
        new("Interlis", "Datenaustauschformat fuer strukturierte Geodaten und VSA-Kanalinspektionsdaten.", ["XTF", "ILI", "Datenabgabe"]),
        new("Rohranfang", "Startpunkt der Haltungsaufnahme und Bezug fuer 0.00 m.", ["Aufnahmetechnik"]),
        new("Rohrende", "Ende der Haltungsaufnahme; Verbindung zum Schacht soll dokumentiert sein.", ["Aufnahmetechnik"]),
        new("Anschluss", "Seitlicher Zulauf oder Anschlussleitung in eine Haltung.", ["Kanal-TV", "Schadencodierung"]),
        new("Sanierungsabnahme", "Inspektion nach einer Sanierung zur Dokumentation des aktuellen Zustands.", ["Sanierung", "Qualitaet"]),
        new("Streckenschaden", "Schaden, der sich ueber eine Laenge erstreckt und mit Anfang/Ende oder Laenge dokumentiert wird.", ["VSA-Code", "Quantifizierung"]),
        new("Code-Katalog", "Fuehrende Quelle fuer aktuelle Schadenscodes im Programm; importierte Alt-PDFs dienen nur als Kontext und duerfen Codes nicht ueberschreiben.", ["VSA-Code", "Implementierung", "Qualitaet"])
    ];
}

public static class SourcePolicy
{
    public static FachwissenSourcePolicy Create() => new(
        CurrentCodeAuthority: "Die aktuellen VSA-/Schadenscodes kommen aus den implementierten Katalogen im Programm, nicht aus importierten Alt-PDFs.",
        AuthoritativeCodeFiles:
        [
            "src/AuswertungPro.Next.UI/Data/vsa_codes.json",
            "src/AuswertungPro.Next.UI/Data/classification_channels.json",
            "src/AuswertungPro.Next.UI/Data/classification_manholes.json",
            "src/AuswertungPro.Next.UI/Data/vsa_quantification_profile.json"
        ],
        NonAuthoritativePatterns:
        [
            "Schadencodierung*.pdf",
            "*DatentransferVernehmlassung*.pdf",
            "*2018*Schadencodierung*"
        ],
        Rules:
        [
            "Importierte Schadencodierungs-PDFs duerfen keine implementierten Code-Definitionen ueberschreiben.",
            "Diese PDFs duerfen fuer Aufbau, Ablauf, Datenabgabe und historische Struktur einer Kanal-TV-Aufnahme verwendet werden.",
            "Bei Konflikten zwischen Fachwissen-Import und Programm-Katalog gewinnt der Programm-Katalog.",
            "Neue Codeaenderungen muessen explizit in den implementierten Katalogen gepflegt und getestet werden."
        ]);
}

public sealed record FachwissenSourcePolicy(
    string CurrentCodeAuthority,
    IReadOnlyList<string> AuthoritativeCodeFiles,
    IReadOnlyList<string> NonAuthoritativePatterns,
    IReadOnlyList<string> Rules);

public sealed record FachwissenManifest(
    DateTimeOffset CreatedUtc,
    string SourceRoot,
    string OutputRoot,
    bool FocusOnly,
    int DocumentCount,
    int ChunkCount,
    int RuleCount,
    IReadOnlyList<DocumentManifestItem> Documents);

public sealed record DocumentManifestItem(
    string DocumentId,
    string Title,
    string SourcePath,
    string RelativePath,
    string Extension,
    string Topic,
    string Authority,
    string IntendedUse,
    long Bytes,
    DateTime LastWriteTimeUtc,
    string Status,
    string ExtractionQuality,
    int TextLength,
    string? Message);

public sealed record IndexedDocument(
    string DocumentId,
    string Title,
    string SourcePath,
    string RelativePath,
    string Extension,
    string Topic,
    string Authority,
    string IntendedUse,
    long Bytes,
    DateTime LastWriteTimeUtc,
    string Status,
    string ExtractionQuality,
    string? Message,
    string Text)
{
    public int TextLength => Text.Length;
    public string SearchText => Text.ToLowerInvariant();

    public DocumentManifestItem ToManifestItem() => new(
        DocumentId,
        Title,
        SourcePath,
        RelativePath,
        Extension,
        Topic,
        Authority,
        IntendedUse,
        Bytes,
        LastWriteTimeUtc,
        Status,
        ExtractionQuality,
        TextLength,
        Message);
}

public sealed record KnowledgeChunk(
    string ChunkId,
    string DocumentId,
    int ChunkNumber,
    string Title,
    string Topic,
    string SourcePath,
    string RelativePath,
    string SourceType,
    string Authority,
    string IntendedUse,
    string Text)
{
    public static KnowledgeChunk Create(IndexedDocument doc, int chunkNumber, string text)
    {
        return new KnowledgeChunk(
            ChunkId: $"{doc.DocumentId}-{chunkNumber:0000}",
            DocumentId: doc.DocumentId,
            ChunkNumber: chunkNumber,
            Title: doc.Title,
            Topic: doc.Topic,
            SourcePath: doc.SourcePath,
            RelativePath: doc.RelativePath,
            SourceType: doc.Extension.TrimStart('.').ToLowerInvariant(),
            Authority: doc.Authority,
            IntendedUse: doc.IntendedUse,
            Text: text);
    }
}

public sealed record FachwissenRule(
    string RuleId,
    string Category,
    string Title,
    string RuleText,
    string SourceDocumentId,
    string SourcePath,
    IReadOnlyList<string> EvidenceKeywords,
    double Confidence);

public sealed record RuleTemplate(
    string RuleId,
    string Category,
    string Title,
    string RuleText,
    IReadOnlyList<string> Keywords);

public sealed record GlossaryTerm(
    string Term,
    string Definition,
    IReadOnlyList<string> Tags);

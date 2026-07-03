using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>Ergebnis der KI-Zweitmeinung zu einem PDF.</summary>
public sealed record PdfKiKlassifikation(
    PdfDokumentTyp Typ,
    string? SchachtVon,
    string? SchachtBis,
    string? Datum);

/// <summary>
/// R4: KI-Schiedsrichter fuer PDFs, die die deterministische Typ-Erkennung (R1)
/// NICHT sicher zuordnen kann. Der eigentliche LLM-Aufruf (Qwen3-VL via Ollama,
/// striktes JSON-Schema) wird als Delegate injiziert — Prompt-Bau und
/// Antwort-Parsing sind pur und getestet. Ergebnisse sind immer nur ein
/// VORSCHLAG und werden im Report als "per KI" gekennzeichnet.
/// </summary>
public sealed class PdfKiSchiedsrichter
{
    /// <summary>Striktes JSON-Schema fuer die Ollama-Antwort (format-Parameter).</summary>
    public const string JsonSchema = """
{
  "type": "object",
  "properties": {
    "typ": { "type": "string", "enum": ["TvProtokoll", "Dichtheitspruefung", "PlanSituation", "Deckblatt", "Unbekannt"] },
    "schacht_von": { "type": ["string", "null"] },
    "schacht_bis": { "type": ["string", "null"] },
    "datum": { "type": ["string", "null"] }
  },
  "required": ["typ", "schacht_von", "schacht_bis", "datum"]
}
""";

    private readonly Func<string, CancellationToken, Task<string>> _llmJsonCall;

    /// <param name="llmJsonCall">Fuehrt den LLM-Aufruf aus (Prompt → JSON-Antwort gemaess <see cref="JsonSchema"/>).</param>
    public PdfKiSchiedsrichter(Func<string, CancellationToken, Task<string>> llmJsonCall)
        => _llmJsonCall = llmJsonCall ?? throw new ArgumentNullException(nameof(llmJsonCall));

    /// <summary>
    /// Klassifiziert ein PDF anhand seines Textanfangs. Null bei fehlendem Text,
    /// LLM-Fehler oder unbrauchbarer Antwort — der Aufrufer behandelt das als
    /// "weiterhin unbekannt" (kein stiller Zwang).
    /// </summary>
    public async Task<PdfKiKlassifikation?> KlassifiziereAsync(string pdfPath, CancellationToken ct)
    {
        string? text;
        try
        {
            text = PdfDokumentTypErkennung.ReadPdfTextPrefix(pdfPath, maxPages: 2);
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
            return null; // Scan ohne Textebene — bewusst kein Rendering in V1

        try
        {
            var antwort = await _llmJsonCall(BautPrompt(text), ct).ConfigureAwait(false);
            return ParseAntwort(antwort);
        }
        catch
        {
            return null; // Ollama nicht erreichbar/Timeout — Import laeuft ohne KI weiter
        }
    }

    /// <summary>Prompt fuer die Klassifikation (deutsch, mit Kontext der Kanalinspektion).</summary>
    internal static string BautPrompt(string pdfText)
    {
        var gekuerzt = pdfText.Length > 4000 ? pdfText[..4000] : pdfText;
        return
            "Du klassifizierst Dokumente aus der Kanalinspektion (Schweiz, VSA/SIA). " +
            "Moegliche Typen: TvProtokoll (Kanalfernseh-Inspektionsprotokoll mit Beobachtungen/VSA-Codes), " +
            "Dichtheitspruefung (Druck-/Dichtheitspruefprotokoll nach SIA 190), " +
            "PlanSituation (Plan/Situationsplan/Karte), Deckblatt, Unbekannt. " +
            "Extrahiere falls vorhanden die Schachtnummern der Pruefstrecke (von/bis) und das Datum (TT.MM.JJJJ). " +
            "Antworte NUR mit JSON nach dem vorgegebenen Schema.\n\n" +
            "Dokumenttext:\n" + gekuerzt;
    }

    /// <summary>Parst die LLM-Antwort tolerant; null bei unbrauchbarem JSON.</summary>
    internal static PdfKiKlassifikation? ParseAntwort(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var typText = LiesString(root, "typ");
            if (!Enum.TryParse<PdfDokumentTyp>(typText, ignoreCase: true, out var typ))
                typ = PdfDokumentTyp.Unbekannt;

            return new PdfKiKlassifikation(
                typ,
                LiesString(root, "schacht_von"),
                LiesString(root, "schacht_bis"),
                LiesString(root, "datum"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? LiesString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return null;
        var wert = el.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(wert) ? null : wert;
    }
}

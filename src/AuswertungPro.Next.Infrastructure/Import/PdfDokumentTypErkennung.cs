using System;
using System.IO;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

public enum PdfDokumentTyp
{
    Unbekannt,
    TvProtokoll,
    Dichtheitspruefung,
    PlanSituation,
    Deckblatt,
    Schachtprotokoll,

    /// <summary>
    /// Aushaerteprotokoll eines Inliners (Lampenzug, Temperaturen, Chargen-Nr.).
    /// Gehoert wie die Dichtheitspruefung zur Haltung, nicht zum Schacht.
    /// </summary>
    Aushaerteprotokoll
}

/// <summary>
/// Zentrale PDF-Typ-Erkennung fuer den Ein-Knopf-Import.
/// Ziel: Plaene, Dichtheitspruefungen und TV-Protokolle nie mit
/// unterschiedlichen lokalen Heuristiken verwechseln.
/// </summary>
public static class PdfDokumentTypErkennung
{
    private static IPdfTextPrefixReader _textPrefixReader = new PdfTextPrefixReaderService();

    public static IPdfTextPrefixReader TextPrefixReader => Volatile.Read(ref _textPrefixReader);

    public static void UseTextPrefixReader(IPdfTextPrefixReader reader)
        => Volatile.Write(
            ref _textPrefixReader,
            reader ?? throw new ArgumentNullException(nameof(reader)));

    public static PdfDokumentTyp ErkenneDatei(string path, int maxPages = 6)
    {
        var text = ReadPdfTextPrefix(path, maxPages);
        return ErkenneText(text, Path.GetFileName(path));
    }

    public static PdfDokumentTyp ErkenneText(string? text, string? fileName = null)
    {
        var hasText = !string.IsNullOrWhiteSpace(text);

        // Aushaerteprotokoll zuerst: Es traegt dieselben Kopfdaten wie die
        // Dichtheitspruefung (Rohrdurchmesser, Druckdiagramm) und wuerde sonst je nach
        // Schreibweise dort hineinrutschen.
        if (LooksLikeAushaerteprotokoll(text, fileName))
            return PdfDokumentTyp.Aushaerteprotokoll;

        // Dichtheitspruefung gewinnt vor normalem TV-Protokoll:
        // KINS/DP-PDFs enthalten oft Haltungspaare, aber keine TV-Tabelle.
        if (LooksLikeDichtheitspruefung(text, fileName))
            return PdfDokumentTyp.Dichtheitspruefung;

        if (LooksLikeTvProtokoll(text))
            return PdfDokumentTyp.TvProtokoll;

        // Schachtmasse und Anschlussnummern dürfen nicht als Haltungspaar in
        // den teuren Haltungs-/OCR-Lauf gelangen. Gemischte TV-Berichte bleiben
        // durch die vorherige positive TV-Erkennung weiterhin berücksichtigt.
        if (HasShaftMarkers(text))
            return PdfDokumentTyp.Schachtprotokoll;

        if (LooksLikePlanSituation(text, fileName, hasText))
            return PdfDokumentTyp.PlanSituation;

        if (LooksLikeDeckblatt(text, fileName))
            return PdfDokumentTyp.Deckblatt;

        return PdfDokumentTyp.Unbekannt;
    }

    public static string? ReadPdfTextPrefix(string path, int maxPages = 6)
        => TextPrefixReader.ReadPdfTextPrefix(path, maxPages);

    internal static bool HasShaftMarkers(string? text)
        => ContainsAny(text, "Schachtprotokoll", "Schachtinspektion", "Schachtbericht", "SCHACHTPRO",
            "Schacht Nr", "SchachtNr", "Schacht-Nr", "Schachtnummer");

    /// <summary>
    /// Titel des Aushaerteprotokolls, tolerant gegen die Stelle des Umlauts.
    ///
    /// Belegt am Buerglen-Bestand: Dieselbe Titelzeile las das OCR als
    /// <c>Aush\u00e4rteprotokoll</c>, <c>Aushirteprotokoll</c> und <c>Aushfarteprotokoll</c> \u2014
    /// der Umlaut in der grossen Schrift ist unzuverlaessig. Ohne diese Toleranz fielen
    /// drei von neun Protokollen still aus der Verteilung.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex AushaerteTitelRegex = new(
        @"Aush.{0,2}rt(e|ungs)?protokoll",
        System.Text.RegularExpressions.RegexOptions.Compiled
        | System.Text.RegularExpressions.RegexOptions.IgnoreCase
        | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// Aushaerteprotokoll des Inliners. Erkannt wird der Titel (umlauttolerant) oder das
    /// umlautfreie Begriffspaar Linertyp + Lampenleistung, das es nur in diesem Protokoll
    /// gibt. Ein einzelnes der beiden Woerter genuegt bewusst nicht.
    /// </summary>
    private static bool LooksLikeAushaerteprotokoll(string? text, string? fileName)
    {
        if (ContainsAny(fileName, "aushaert", "aush\u00e4rt"))
            return true;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        return AushaerteTitelRegex.IsMatch(text)
               || (ContainsAny(text, "Linertyp") && ContainsAny(text, "Lampenleistung"));
    }

    /// <summary>
    /// "DP" als eigenes Wort im Dateinamen \u2014 dieselbe Konvention, nach der der
    /// Dichtheits-Verteiler schon seine Ordner erkennt (<c>048473_DP_Gross</c>). Ein
    /// blosses "dp" mitten im Wort (Adapterplan, DPS) zaehlt ausdruecklich nicht.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex DpKuerzelRegex = new(
        @"(^|[_\-\s])DP($|[_\-\s])",
        System.Text.RegularExpressions.RegexOptions.Compiled
        | System.Text.RegularExpressions.RegexOptions.IgnoreCase
        | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static bool LooksLikeDichtheitspruefung(string? text, string? fileName)
    {
        if (ContainsAny(fileName, "dicht"))
            return true;

        if (string.IsNullOrWhiteSpace(text))
        {
            // Reiner Scan ohne Textebene: Dann ist das Kuerzel der einzige Beleg, den es
            // gibt. Ist Text lesbar, entscheidet weiterhin ausschliesslich der Inhalt —
            // "DP" im Namen allein bleibt zu wenig (bewusste Regel, R1-R3).
            return !string.IsNullOrWhiteSpace(fileName)
                   && DpKuerzelRegex.IsMatch(Path.GetFileNameWithoutExtension(fileName));
        }

        return ContainsAny(
                   text,
                   "Dichtheitspruefung",
                   "Dichtheitspr\u00fcfung",
                   // Titel der realen GKS-Pruefberichte; sie nennen das Wort
                   // "Dichtheit" nirgends.
                   "Druckpruefprotokoll",
                   "Druckpr\u00fcfprotokoll",
                   "SIA190",
                   "SIA 190",
                   // Reale Schreibweisen aus KIT-Bauinspekt-Pruefberichten. Ohne sie
                   // blieb ein ganzer Pruefbericht "Unbekannt" und wurde nie verteilt.
                   // Die letzte Form ist die wichtigste: der produktive PdfPig-Leser
                   // liefert den Seitentext OHNE Leerzeichen ("...gemaRSIANorm190:2017").
                   "SIA Norm 190",
                   "SIANorm 190",
                   "SIANorm190",
                   "VSA RL Dicht")
               || (ContainsAny(text, "von Schacht:", "nach Schacht:", "bis Schacht:",
                       "vonSchacht:", "nachSchacht:", "bisSchacht:")
                   && ContainsAny(text, "Pruefdruck", "Pr\u00fcfdruck", "Pruefstrecke", "Pr\u00fcfstrecke"));
    }

    private static bool LooksLikeTvProtokoll(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return ContainsAny(
            text,
            "Haltungsinspektion",
            "Haltungsbilder",
            "Leitungs-Stammdaten",
            "Leitungsbericht",
            "Leitungsgrafik",
            "Leitungsbildbericht",
            "Insp.-Datum",
            "Kanalinspektion",
            "Kanalfernsehprotokoll",
            "VSA-Code",
            "Kanalschadencode");
    }

    private static bool LooksLikePlanSituation(string? text, string? fileName, bool hasText)
    {
        if (ContainsAny(fileName, "plan", "situation", "situationsplan", "netzplan"))
            return true;

        if (!hasText || string.IsNullOrWhiteSpace(text))
            return false;

        return ContainsAny(
            text,
            "Leitungsende",
            "Dachwasser angeschlossen",
            "Situationsplan",
            "Sanierungsplan",
            "Netzplan");
    }

    private static bool LooksLikeDeckblatt(string? text, string? fileName)
        => ContainsAny(fileName, "deckblatt", "titelblatt")
           || ContainsAny(text, "Deckblatt", "Titelblatt", "Projektuebersicht", "Projekt\u00fcbersicht");

    private static bool ContainsAny(string? value, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        foreach (var needle in needles)
        {
            if (value.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

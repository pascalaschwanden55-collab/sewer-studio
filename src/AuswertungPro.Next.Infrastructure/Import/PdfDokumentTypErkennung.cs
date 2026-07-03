using System;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Import;

public enum PdfDokumentTyp
{
    Unbekannt,
    TvProtokoll,
    Dichtheitspruefung,
    PlanSituation,
    Deckblatt
}

/// <summary>
/// Zentrale PDF-Typ-Erkennung fuer den Ein-Knopf-Import.
/// Ziel: Plaene, Dichtheitspruefungen und TV-Protokolle nie mit
/// unterschiedlichen lokalen Heuristiken verwechseln.
/// </summary>
public static class PdfDokumentTypErkennung
{
    public static PdfDokumentTyp ErkenneDatei(string path, int maxPages = 6)
    {
        var text = ReadPdfTextPrefix(path, maxPages);
        return ErkenneText(text, Path.GetFileName(path));
    }

    public static PdfDokumentTyp ErkenneText(string? text, string? fileName = null)
    {
        var hasText = !string.IsNullOrWhiteSpace(text);

        // Dichtheitspruefung gewinnt vor normalem TV-Protokoll:
        // KINS/DP-PDFs enthalten oft Haltungspaare, aber keine TV-Tabelle.
        if (LooksLikeDichtheitspruefung(text, fileName))
            return PdfDokumentTyp.Dichtheitspruefung;

        if (LooksLikeTvProtokoll(text))
            return PdfDokumentTyp.TvProtokoll;

        if (LooksLikePlanSituation(text, fileName, hasText))
            return PdfDokumentTyp.PlanSituation;

        if (LooksLikeDeckblatt(text, fileName))
            return PdfDokumentTyp.Deckblatt;

        return PdfDokumentTyp.Unbekannt;
    }

    public static string? ReadPdfTextPrefix(string path, int maxPages = 6)
    {
        try
        {
            using var document = PdfDocument.Open(path);
            return string.Join(
                "\n",
                document.GetPages()
                    .Take(Math.Max(1, maxPages))
                    .Select(page => page.Text));
        }
        catch
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch
            {
                return null;
            }
        }
    }

    private static bool LooksLikeDichtheitspruefung(string? text, string? fileName)
    {
        if (ContainsAny(fileName, "dicht"))
            return true;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        return ContainsAny(
                   text,
                   "Dichtheitspruefung",
                   "Dichtheitspr\u00fcfung",
                   "SIA190",
                   "SIA 190",
                   "VSA RL Dicht")
               || (ContainsAny(text, "von Schacht:", "nach Schacht:")
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

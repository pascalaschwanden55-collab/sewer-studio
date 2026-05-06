using System;
using System.Text;

namespace AuswertungPro.Next.Application.Ai.Training.Services;

/// <summary>
/// Phase 5.3: Pure statische Helper aus PdfProtocolExtractor extrahiert.
/// Identifiziert Nicht-Inspektions-PDFs (Rechnungen, Dichtheitspruefungen)
/// und dekodiert verschluesselte PDF-Texte.
/// </summary>
public static class PdfProtocolHelpers
{
    /// <summary>Dateinamen-Muster die KEINE Inspektionsprotokolle sind.</summary>
    public static readonly string[] NonProtocolKeywords =
        ["faktura", "rechnung", "offerte", "angebot", "lieferschein",
         "quittung", "mahnung", "vertrag", "auftrag", "kostenvor",
         "linerdatenblatt", "linerbestellung", "aushärtungsprotokoll",
         "einbauprotokoll", "injektion", "situation", "lageplan",
         "schlussrechnung", "bestellung", "datenblatt",
         // V4.3: Dichtheitspruefungen (DP) sind keine Inspektionsprotokolle.
         "_dp", " dp", "-dp", "dichtheit", "luftpr"];

    /// <summary>Text-Marker die zeigen dass das PDF keine Inspektion sondern eine Pruefung ist.</summary>
    public static readonly string[] NonProtocolTextMarkers =
        ["dichtheitsprüfung", "dichtheitspruefung", "sia190", "sia 190",
         "rohrleitungsprüfung", "luftprüfung", "luftpruefung",
         "prüfdruck", "prüfresultat"];

    /// <summary>
    /// Versucht einen verschluesselten PDF-Text zu dekodieren (Caesar-Shift).
    /// Manche PDFs haben falsche CMaps und liefern verschobene Zeichen.
    /// </summary>
    public static string TryDecodeShiftedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        string[] knownWords =
        {
            "Leitung", "Video", "Foto", "Zustand", "Material",
            "Schacht", "Kanal", "Haltung", "Inspektion", "Dimension",
            "Profil", "Rohr", "Position", "Entf", "Strasse", "Wetter"
        };

        int existingMatches = CountWordMatches(text, knownWords);
        if (existingMatches >= 3)
            return text;

        int bestShift = 0;
        int bestCount = existingMatches;

        for (int shift = 1; shift <= 60; shift++)
        {
            var decoded = ShiftAllChars(text, shift);
            int count = CountWordMatches(decoded, knownWords);
            if (count > bestCount)
            {
                bestCount = count;
                bestShift = shift;
            }
        }

        if (bestShift > 0 && bestCount >= 3)
            return ShiftAllChars(text, bestShift);

        return text;
    }

    private static int CountWordMatches(string text, string[] words)
    {
        int count = 0;
        foreach (var word in words)
        {
            if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    private static string ShiftAllChars(string text, int shift)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch == '\r' || ch == '\n' || ch == '\t' || ch == ' ')
                sb.Append(ch);
            else
                sb.Append((char)(ch + shift));
        }
        return sb.ToString();
    }
}

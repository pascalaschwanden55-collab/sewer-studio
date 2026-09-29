namespace AuswertungPro.Next.Application.Import;

/// <summary>Welcher Textleser eine PDF-Seite geliefert hat.</summary>
public enum PdfLeserArt
{
    /// <summary>Das externe, geprueft geeignete pdftotext.</summary>
    PdfToText = 0,

    /// <summary>Der mitgelieferte eingebaute Leser (PdfPig).</summary>
    Eingebaut = 1
}

/// <summary>Urteil ueber ein gefundenes pdftotext.</summary>
/// <param name="Geeignet">true, wenn dieses Programm den Text spaltentreu liefert.</param>
/// <param name="Hersteller">Erkannter Hersteller, etwa "Poppler" oder "Xpdf".</param>
/// <param name="Version">Erkannte Hauptversion, 0 wenn unbekannt.</param>
/// <param name="Grund">Klartext fuer Bericht und Protokoll.</param>
public sealed record PdfLeserUrteil(bool Geeignet, string Hersteller, int Version, string Grund);

/// <summary>
/// Entscheidet anhand der Versionsausgabe von <c>pdftotext -v</c>, ob dieses Programm
/// verwendet werden darf. Reine Regel ohne Dateizugriff.
///
/// Anlass (gemessen 19.09.2026 an 264 SchachtPro-Protokollen aus Goeschenen):
/// Die Anschlusstabelle eines Schachtprotokolls enthaelt Kennung, Uhrzeit, Tiefe,
/// Durchmesser, Typ, Medium, Material und Zustand nebeneinander. Xpdf 4.00 zerreisst
/// diese Zeilen vertikal — es liest nur 73,2 % der Tabellen vollstaendig, verliert 63 von
/// 704 Anschluessen UND ordnet den gelesenen Anschluessen fremde Werte zu (Schacht 10039:
/// Einlauf 1 bekommt Tiefe und Durchmesser von Einlauf 2). Poppler 25.07 liefert dieselben
/// Dateien vollstaendig (704 von 704). Der eingebaute Leser ebenfalls (704 von 704).
///
/// Ein falsch zugeordneter Messwert ist schlimmer als ein fehlender: Er sieht im Programm
/// richtig aus. Deshalb ist die Regel fail-closed — ein unbekanntes Programm wird NICHT
/// verwendet, sondern der mitgelieferte eingebaute Leser.
/// </summary>
public static class PdfLeserEignung
{
    /// <summary>
    /// Kleinste Poppler-Hauptversion, die verwendet werden darf. Gemessen ist 25.07;
    /// die Untergrenze ist bewusst konservativ gewaehlt und nicht einzeln nachgemessen.
    /// Wer sie senkt, misst vorher eine echte Anschlusstabelle nach.
    /// </summary>
    public const int KleinstePopplerVersion = 21;

    /// <summary>Beurteilt die Ausgabe von <c>pdftotext -v</c> (sie erscheint auf stderr).</summary>
    public static PdfLeserUrteil Beurteile(string? versionsausgabe)
    {
        var text = (versionsausgabe ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return new PdfLeserUrteil(false, "unbekannt", 0,
                "pdftotext hat keine Versionsangabe geliefert. Es wird der eingebaute Leser verwendet.");
        }

        // Xpdf meldet sich als "pdftotext version 4.00" mit "Copyright ... Glyph & Cog".
        // Die Versionsnummern beider Hersteller ueberschneiden sich, deshalb zaehlt der Hersteller zuerst.
        var istXpdf = text.Contains("Glyph & Cog", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Xpdf", StringComparison.OrdinalIgnoreCase);
        var istPoppler = text.Contains("Poppler", StringComparison.OrdinalIgnoreCase);

        var version = LiesHauptversion(text);

        if (istXpdf && !istPoppler)
        {
            return new PdfLeserUrteil(false, "Xpdf", version,
                $"Das gefundene pdftotext stammt von Xpdf (Version {VersionText(version)}). "
                + "Es zerreisst die Anschlusstabelle der Schachtprotokolle. Es wird der eingebaute Leser verwendet.");
        }

        if (!istPoppler)
        {
            return new PdfLeserUrteil(false, "unbekannt", version,
                "Das gefundene pdftotext stammt von einem unbekannten Hersteller. "
                + "Es wird der eingebaute Leser verwendet.");
        }

        if (version < KleinstePopplerVersion)
        {
            return new PdfLeserUrteil(false, "Poppler", version,
                $"Poppler {VersionText(version)} ist älter als die geprüfte Fassung "
                + $"{KleinstePopplerVersion}. Es wird der eingebaute Leser verwendet.");
        }

        return new PdfLeserUrteil(true, "Poppler", version,
            $"Poppler {VersionText(version)} ist geprüft geeignet.");
    }

    private static string VersionText(int version) => version > 0 ? version.ToString() : "unbekannt";

    /// <summary>Liest die erste Zahlengruppe einer Zeile mit "version" als Hauptversion.</summary>
    private static int LiesHauptversion(string text)
    {
        foreach (var zeile in text.Split('\n'))
        {
            if (!zeile.Contains("version", StringComparison.OrdinalIgnoreCase))
                continue;

            var ziffern = 0;
            var gefunden = false;
            foreach (var zeichen in zeile)
            {
                if (char.IsDigit(zeichen))
                {
                    gefunden = true;
                    ziffern = (ziffern * 10) + (zeichen - '0');
                    if (ziffern > 100000)
                        return 0;
                }
                else if (gefunden)
                {
                    return ziffern;
                }
            }

            if (gefunden)
                return ziffern;
        }

        return 0;
    }
}

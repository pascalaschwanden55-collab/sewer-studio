using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>Woher eine im Bauteil stehende Objektkennung stammt.</summary>
public enum KennungsHerkunft
{
    /// <summary>Keine Kennung oder keine gueltige SIA405-Form — sie sagt nichts aus.</summary>
    Keine = 0,

    /// <summary>
    /// Aus dem Kataster nachgeschlagen oder von Hand bestaetigt. Diese Kennung ist
    /// die des Katasters und wird geschuetzt.
    /// </summary>
    BestaetigtesGeonis = 1,

    /// <summary>
    /// Von SewerStudio selbst beim Neu-Export erzeugt (Praefix <c>chSST</c>). Sie
    /// beschreibt kein Katasterobjekt und darf einen Katastertreffer nicht blockieren.
    /// </summary>
    LokaleExportkennung = 2,

    /// <summary>
    /// Die TID aus einer importierten Datei. Ob sie eine echte GEONIS-Kennung
    /// oder eine vom Exporteur erzeugte Kennung ist, ist dadurch nicht belegt.
    /// </summary>
    Dateikennung = 3,

    /// <summary>Gueltige Form, aber die Herkunft ist nicht belegt.</summary>
    Unbekannt = 4
}

/// <summary>
/// Bestimmt die Herkunft einer Objektkennung.
///
/// Anlass (Audit 2026-09-05): Der Plan schloss bisher aus der blossen FORM auf die
/// Quelle — „gueltige SIA405-Form heisst, sie stammt aus einer neueren Katasterquelle".
/// Das ist nicht belegt. Ebenso wenig beweist die Herkunft XTF/XTF405, dass eine
/// TID ersetzbar wäre: Auch GEONIS liefert seine Kennungen in XTF-Dateien.
/// Bei abweichenden Kennungen bleibt die Übernahme deshalb ein Prüffall.
///
/// Belege in dieser Reihenfolge:
///
/// 1. <b>Praefix.</b> <c>chSST</c> vergibt SewerStudios eigener Neu-Export.
/// 2. <b>Feldherkunft.</b> <see cref="FieldSource.Kataster"/> oder eine Handeingabe
///    heisst bestaetigt; <see cref="FieldSource.Xtf"/> und <see cref="FieldSource.Xtf405"/>
///    heissen: aus einer Datei uebernommen.
///
/// Reine Rechnung: kein Datei-, Netz- oder UI-Zugriff.
/// </summary>
public static class KatasterKennungHerkunft
{
    /// <summary>Praefix der von SewerStudio beim Neu-Export erzeugten Kennungen.</summary>
    public const string EigenesExportPraefix = "chSST";

    public static KennungsHerkunft Bestimme(string? kennung, FieldSource quelle, bool handgesetzt)
    {
        var text = (kennung ?? "").Trim();
        if (!SiaObjektkennung.IstGueltig(text))
            return KennungsHerkunft.Keine;

        if (text.StartsWith(EigenesExportPraefix, StringComparison.Ordinal))
            return KennungsHerkunft.LokaleExportkennung;

        if (handgesetzt || quelle is FieldSource.Kataster or FieldSource.Manual)
            return KennungsHerkunft.BestaetigtesGeonis;

        if (quelle is FieldSource.Xtf or FieldSource.Xtf405 or FieldSource.Ili)
            return KennungsHerkunft.Dateikennung;

        return KennungsHerkunft.Unbekannt;
    }

    /// <summary>
    /// Darf diese Kennung einen geprueften, eindeutigen Katastertreffer blockieren?
    ///
    /// Bestätigte Katasterkennungen sind geschützt. Unbekannte Kennungen und
    /// importierte XTF-TIDs blockieren bei Widerspruch ebenfalls als Prüffall.
    /// </summary>
    public static bool BlockiertUebernahme(KennungsHerkunft herkunft)
        => herkunft is KennungsHerkunft.BestaetigtesGeonis or KennungsHerkunft.Unbekannt
            or KennungsHerkunft.Dateikennung;
}

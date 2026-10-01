namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Quelle eines Feldwerts. Priorität (hoch → niedrig):
/// Manual > Xtf/Xtf405 > Ili > Pdf/Spro > Legacy > Protocol > Unknown.
///
/// Die Zahlen sind nur Speicherwerte und bilden diese Reihenfolge NICHT ab
/// (Pdf = 7 steht ueber Xtf405 = 5). Massgebend ist allein die Rangtabelle in
/// MergeEngine.GetPriority.
/// </summary>
public enum FieldSource
{
    Unknown = 0,
    Legacy = 1,
    Protocol = 2,
    Xtf = 3,
    Xtf405 = 5,
    Ili = 6,
    Pdf = 7,

    /// <summary>SchachtPro-Archiv (.spro). Wie <see cref="Pdf"/> ein Protokollimport,
    /// aber eine eigene Quelle - sonst waere spaeter nicht unterscheidbar, woher ein
    /// Schachtwert stammt.</summary>
    Spro = 8,
    Manual = 10,

    /// <summary>
    /// Aus einem Kataster uebernommen: GeoShop, QGIS oder WebGIS. Ergaenzt, was die Kanalfirma
    /// nicht liefert; ein spaeterer Import der Kanalfirma ersetzt ihn (Entscheid Pascal
    /// 23.09.2026 abends, <see cref="KatasterFeldschutz"/>). Nur eine Handmarkierung schuetzt.
    /// </summary>
    Kataster = 11,

    /// <summary>
    /// Aus der Grundbuchauskunft nachgeschlagen und vom Bearbeiter bestaetigt.
    /// Geschuetzt ist der Wert nur ueber die Handmarkierung (userEdited).
    /// </summary>
    Grundbuch = 12
}

/// <summary>Regeln ueber die Herkunft eines Feldwerts.</summary>
public static class FieldSourceRegeln
{
    /// <summary>
    /// Stammt der Wert aus einem Import der Kanalfirma (Inspektionsdaten, Protokoll)? Diese Werte sind
    /// der Ist-Zustand (Entscheid Pascal 23.09.2026 abends): Holen und GeoShop ueberschreiben sie nie,
    /// und weichen sie vom WebGIS ab, werden sie beim Senden zum Anhaken vorgeschlagen.
    /// </summary>
    public static bool IstKanalfirma(FieldSource quelle)
        => quelle is FieldSource.Legacy or FieldSource.Protocol or FieldSource.Xtf or FieldSource.Xtf405
            or FieldSource.Ili or FieldSource.Pdf or FieldSource.Spro;
}






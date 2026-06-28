// Reine Hilfsklasse fuer das Zusammenfuehren zweier OutEntry-Eintraege
// nach Source-Prioritaet (Xtf > Pdf > Fdb).
// Kein IO, keine externe Abhaengigkeit — nur deterministischer Merge.

/// <summary>
/// Fasst zwei <see cref="OutEntry"/>-Eintraege zu einem zusammen.
/// Bei Feldern, fuer die beide Eintraege einen Wert liefern, gewinnt die
/// hoehere Quellprioritaet (Xtf=1 &gt; Pdf=2 &gt; Fdb=3 &gt; unbekannt=99).
/// Bei gleicher Prioritaet bleibt der bisherige Wert erhalten.
/// </summary>
internal static class OutEntryMerger
{
    /// <summary>
    /// Liefert den numerischen Rang einer Quell-Bezeichnung.
    /// Kleinerer Rang = hoehere Prioritaet.
    /// </summary>
    internal static int Rank(string? source) => source switch
    {
        "Xtf" => 1,
        "Pdf" => 2,
        "Fdb" => 3,
        _      => 99,
    };

    /// <summary>
    /// Normalisiert einen rohen Haltungs-Schluessel, indem Leerzeichen und
    /// verschiedene Strich-Varianten durch einen einheitlichen Bindestrich
    /// ersetzt werden.
    /// </summary>
    internal static string NormalizeKey(string raw)
        => (raw ?? string.Empty)
            .Replace(" ", "")
            .Replace("/", "-")
            .Replace("–", "-") // En-Dash
            .Replace("—", "-"); // Em-Dash

    /// <summary>
    /// Fuehrt <paramref name="cur"/> und <paramref name="next"/> zu einem
    /// neuen <see cref="OutEntry"/> zusammen. Pro Feld gewinnt der Kandidat
    /// mit der hoeheren Quellprioritaet; bei gleichem Rang bleibt
    /// <paramref name="cur"/> erhalten (first-wins).
    /// Felder ohne eigene Quellangabe (Profilbreite_mm, Nutzungsart,
    /// Laenge_m, Strasse, Ort) werden per erstem vorhandenem Wert befuellt.
    /// </summary>
    internal static OutEntry MergeOut(OutEntry cur, OutEntry next)
    {
        // Material
        string? mat    = cur.Material;
        string? matSrc = cur.MaterialSource;
        if (!string.IsNullOrWhiteSpace(next.Material)
            && (string.IsNullOrWhiteSpace(mat) || Rank(next.MaterialSource) < Rank(matSrc)))
        {
            mat    = next.Material;
            matSrc = next.MaterialSource;
        }

        // DN_mm
        int?    dn    = cur.DN_mm;
        string? dnSrc = cur.DnSource;
        if (next.DN_mm is > 0 && (dn is null or 0 || Rank(next.DnSource) < Rank(dnSrc)))
        {
            dn    = next.DN_mm;
            dnSrc = next.DnSource;
        }

        // Geometrie
        string? geo    = cur.Geometrie;
        string? geoSrc = cur.GeometrieSource;
        if (!string.IsNullOrWhiteSpace(next.Geometrie)
            && (string.IsNullOrWhiteSpace(geo) || Rank(next.GeometrieSource) < Rank(geoSrc)))
        {
            geo    = next.Geometrie;
            geoSrc = next.GeometrieSource;
        }

        return cur with
        {
            Material        = mat,
            MaterialSource  = matSrc,
            DN_mm           = dn,
            DnSource        = dnSrc,
            Geometrie       = geo,
            GeometrieSource = geoSrc,
            Profilbreite_mm = cur.Profilbreite_mm ?? next.Profilbreite_mm,
            Nutzungsart     = cur.Nutzungsart     ?? next.Nutzungsart,
            Laenge_m        = cur.Laenge_m        ?? next.Laenge_m,
            Strasse         = cur.Strasse         ?? next.Strasse,
            Ort             = cur.Ort             ?? next.Ort,
        };
    }
}

/// <summary>
/// Konsolidierter Ausgabe-Eintrag fuer eine Haltung, der alle relevanten
/// Stammdatenfelder aus verschiedenen Quellen zusammenfasst.
/// </summary>
internal sealed record OutEntry(
    string? Material,
    string? MaterialSource,
    int?    DN_mm,
    string? DnSource,
    int?    Profilbreite_mm,
    string? Geometrie,
    string? GeometrieSource,
    string? Nutzungsart,
    double? Laenge_m,
    string? Strasse,
    string? Ort,
    string  ProvenanceRoot);

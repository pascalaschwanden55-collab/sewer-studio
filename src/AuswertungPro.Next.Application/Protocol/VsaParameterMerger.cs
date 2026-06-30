namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Fuegt VSA-KEK-Werte und ihre WinCan-Aliase in ein Parameter-Dictionary ein.
/// Logik aus ObservationCatalogViewModel.MergeVsaParameters extrahiert, verhaltensneutral.
/// Beschreibungen/Aliase fliessen in den WinCan-Export – verhaltenskritisch.
/// </summary>
public static class VsaParameterMerger
{
    /// <summary>
    /// Schreibt VSA-KEK-Felder und ihre WinCan-Aliase in das uebergebene Dictionary.
    /// Vorhandene Eintraege bleiben unveraendert, wenn der Wert leer ist.
    /// </summary>
    public static void Merge(
        Dictionary<string, string> parameters,
        string? vsaDistanz,
        string? vsaVideo,
        string? vsaUhrVon,
        string? vsaUhrBis,
        string? vsaQ1,
        string? vsaQ2,
        string? vsaStrecke,
        bool vsaVerbindung,
        string? vsaAnsicht,
        string? vsaEz,
        string? vsaSchachtbereich,
        string? vsaAnmerkung)
    {
        AddAliases(parameters, vsaDistanz, "vsa.distanz", "Distance");
        AddAliases(parameters, vsaVideo, "vsa.video", "TimeCtr");
        AddAliases(parameters, vsaUhrVon, "vsa.uhr.von", "ClockPos1");
        AddAliases(parameters, vsaUhrBis, "vsa.uhr.bis", "ClockPos2");
        AddAliases(parameters, vsaQ1, "vsa.q1", "Q1", "Quantifizierung1");
        AddAliases(parameters, vsaQ2, "vsa.q2", "Q2", "Quantifizierung2");
        Add(parameters, "vsa.strecke", vsaStrecke);
        if (vsaVerbindung)
            parameters["vsa.verbindung"] = "ja";
        Add(parameters, "vsa.ansicht", vsaAnsicht);
        Add(parameters, "vsa.ez", vsaEz);
        Add(parameters, "vsa.schachtbereich", vsaSchachtbereich);
        Add(parameters, "vsa.anmerkung", vsaAnmerkung);
    }

    private static void Add(Dictionary<string, string> parameters, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parameters[key] = value.Trim();
    }

    private static void AddAliases(Dictionary<string, string> parameters, string? value, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        foreach (var key in keys)
            Add(parameters, key, value);
    }
}

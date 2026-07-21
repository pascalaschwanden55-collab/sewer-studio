namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Fuegt VSA-KEK-Werte und ihre WinCan-Aliase in ein Parameter-Dictionary ein.
/// Logik aus ObservationCatalogViewModel.MergeVsaParameters extrahiert, verhaltensneutral.
/// Beschreibungen/Aliase fliessen in den WinCan-Export – verhaltenskritisch.
/// </summary>
public static class VsaParameterMerger
{
    private static readonly string[] CodeAliases = ["vsa.code", "Code"];
    private static readonly string[] DistanceAliases = ["vsa.distanz", "Distance"];
    private static readonly string[] VideoAliases = ["vsa.video", "TimeCtr"];
    private static readonly string[] ClockFromAliases = ["vsa.uhr.von", "ClockPos1"];
    private static readonly string[] ClockToAliases = ["vsa.uhr.bis", "ClockPos2"];
    private static readonly string[] Q1Aliases = ["vsa.q1", "Q1", "Quantifizierung1"];
    private static readonly string[] Q2Aliases = ["vsa.q2", "Q2", "Quantifizierung2"];

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
        AddAliases(parameters, vsaDistanz, DistanceAliases);
        AddAliases(parameters, vsaVideo, VideoAliases);
        AddAliases(parameters, vsaUhrVon, ClockFromAliases);
        AddAliases(parameters, vsaUhrBis, ClockToAliases);
        AddAliases(parameters, vsaQ1, Q1Aliases);
        AddAliases(parameters, vsaQ2, Q2Aliases);
        Add(parameters, "vsa.strecke", vsaStrecke);
        if (vsaVerbindung)
            parameters["vsa.verbindung"] = "ja";
        Add(parameters, "vsa.ansicht", vsaAnsicht);
        Add(parameters, "vsa.ez", vsaEz);
        Add(parameters, "vsa.schachtbereich", vsaSchachtbereich);
        Add(parameters, "vsa.anmerkung", vsaAnmerkung);
    }

    /// <summary>
    /// Erzeugt einen bereinigten, case-insensitiven Parameter-Snapshot und spiegelt
    /// Werte zwischen den kanonischen VSA-Schluesseln und ihren alten Aliasen.
    /// </summary>
    public static Dictionary<string, string> NormalizeAliases(
        IReadOnlyDictionary<string, string> parameters,
        string authoritativeCode)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in parameters)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;

            var value = kv.Value?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            result[kv.Key.Trim()] = value;
        }

        MirrorAliases(result, CodeAliases);
        MirrorAliases(result, DistanceAliases);
        MirrorAliases(result, VideoAliases);
        MirrorAliases(result, ClockFromAliases);
        MirrorAliases(result, ClockToAliases);
        MirrorAliases(result, Q1Aliases);
        MirrorAliases(result, Q2Aliases);

        if (!string.IsNullOrWhiteSpace(authoritativeCode))
        {
            var code = authoritativeCode.Trim();
            foreach (var key in CodeAliases)
                result[key] = code;
        }

        return result;
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

    private static void MirrorAliases(Dictionary<string, string> parameters, IReadOnlyList<string> keys)
    {
        string? value = null;
        foreach (var key in keys)
        {
            if (parameters.TryGetValue(key, out var candidate)
                && !string.IsNullOrWhiteSpace(candidate))
            {
                value = candidate;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (var key in keys)
            parameters[key] = value;
    }
}

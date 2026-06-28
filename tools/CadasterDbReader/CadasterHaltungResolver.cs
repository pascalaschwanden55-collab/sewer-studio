namespace CadasterDbReader;

/// <summary>
/// Loest Stammdaten-Paare und Topologie-Rohdaten in fertige CadasterTopologyHolding-Eintraege auf.
/// Kein IO, keine Datenbankzugriffe — nimmt nur Dictionaries und Listen.
/// </summary>
internal static class CadasterHaltungResolver
{
    /// <summary>
    /// Erzeugt aus Stammdaten-Paaren und Topologie-Zeilen eine sortierte Haltungsliste.
    /// Fliessrichtung und Strikt-Modus werden aus dem Stammdaten-Kontext abgeleitet.
    /// </summary>
    public static List<CadasterTopologyHolding> Resolve(
        Dictionary<string, List<CadasterStammdatenPair>> stammdatenByPair,
        List<CadasterRawTopologyPair> topologyRows,
        List<string> globalWarnings)
    {
        var haltungen = new List<CadasterTopologyHolding>();
        var stammdatenGroups = stammdatenByPair
            .SelectMany(p => p.Value)
            .GroupBy(p => CadasterTopologyConventions.UnorderedPairKey(p.StartObjName, p.EndObjName))
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (stammdatenGroups.Count > 0)
        {
            foreach (var group in stammdatenGroups)
            {
                var matches = group.ToList();
                var first = matches[0];
                var warnings = new List<string>();
                var directedPairs = matches
                    .Select(p => $"{CadasterTopologyConventions.CleanNodeId(p.StartObjName)}-{CadasterTopologyConventions.CleanNodeId(p.EndObjName)}")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var schachtOben = CadasterTopologyConventions.CleanNodeId(first.StartObjName);
                var schachtUnten = CadasterTopologyConventions.CleanNodeId(first.EndObjName);
                var fliessrichtungQuelle = "cadaster_pair_name";

                if (directedPairs.Count > 1)
                {
                    var ordered = matches
                        .SelectMany(p => new[] { CadasterTopologyConventions.CleanNodeId(p.StartObjName), CadasterTopologyConventions.CleanNodeId(p.EndObjName) })
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                        .Take(2)
                        .ToList();
                    schachtOben = ordered.ElementAtOrDefault(0) ?? schachtOben;
                    schachtUnten = ordered.ElementAtOrDefault(1) ?? schachtUnten;
                    fliessrichtungQuelle = "unsicher";
                    warnings.Add("Mehrere Stammdaten-Richtungen (cadaster) fuer dieselbe Haltung; strikt-Modus.");
                }

                if (matches.Count > 1 && directedPairs.Count == 1)
                    warnings.Add($"Mehrere Stammdaten-Zeilen fuer {schachtOben}-{schachtUnten}; erster Eintrag verwendet.");

                if (string.IsNullOrWhiteSpace(schachtOben) || string.IsNullOrWhiteSpace(schachtUnten))
                {
                    globalWarnings.Add($"Stammdaten {first.ObjName}: leerer Schachtname, uebersprungen.");
                    continue;
                }

                if (fliessrichtungQuelle == "unsicher")
                    globalWarnings.Add($"{schachtOben}-{schachtUnten}: Fliessrichtung unsicher, strikt-Modus.");

                var ht = new CadasterTopologyHolding
                {
                    HaltungPk = $"GISOBJECT:{first.Id}",
                    CanonicalFolderName = $"{schachtOben}-{schachtUnten}",
                    SchachtOben = schachtOben,
                    SchachtUnten = schachtUnten,
                    FliessrichtungQuelle = fliessrichtungQuelle,
                    LaengeM = first.LengthM,
                    AlternativeHaltungIds = CadasterTopologyConventions.BuildAlternativeHoldingIds(schachtOben, schachtUnten),
                    VideoDateinamenAusDb = [],
                    Inspektionen = [],
                    Warnings = warnings
                };
                CadasterTopologyConventions.ApplyTopologyConventions(ht);
                haltungen.Add(ht);
            }

            return haltungen
                .OrderBy(h => h.CanonicalFolderName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Fallback: Haltungsnamen aus GISOBJECT-Topologie-Paarnamen ableiten.
        foreach (var pairName in topologyRows
                     .SelectMany(r => new[] { r.StartObjName, r.EndObjName })
                     .Where(name => CadasterTopologyConventions.TrySplitHoldingPair(name, out _, out _))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            CadasterTopologyConventions.TrySplitHoldingPair(pairName, out var schachtOben, out var schachtUnten);
            var ht = new CadasterTopologyHolding
            {
                HaltungPk = $"GISOBJECT_FALLBACK:{StableId(pairName)}",
                CanonicalFolderName = $"{schachtOben}-{schachtUnten}",
                SchachtOben = schachtOben,
                SchachtUnten = schachtUnten,
                FliessrichtungQuelle = "cadaster_gis_pair_name",
                LaengeM = null,
                AlternativeHaltungIds = CadasterTopologyConventions.BuildAlternativeHoldingIds(schachtOben, schachtUnten),
                VideoDateinamenAusDb = [],
                Inspektionen = [],
                Warnings = ["Keine Lt/Sc-Stammdatenzeile gefunden; aus GISOBJECT-Paarnamen abgeleitet."]
            };
            CadasterTopologyConventions.ApplyTopologyConventions(ht);
            haltungen.Add(ht);
        }

        return haltungen
            .OrderBy(h => h.CanonicalFolderName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // SHA256-basierter stabiler Kurzbezeichner — identisch mit StableId in Program.cs.
    private static string StableId(string text)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).Substring(0, 12).ToLowerInvariant();
    }
}

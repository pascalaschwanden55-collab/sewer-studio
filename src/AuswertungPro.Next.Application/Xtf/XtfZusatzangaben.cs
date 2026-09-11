using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>Freigegebene Sachangaben ohne Standardfeld. Keine Pfade, IDs oder internen Metadaten.</summary>
public static class XtfZusatzangaben
{
    public const string Modell = "SewerStudio_Zusatz_2026";
    public const string Topic = Modell + ".Zusatzdaten";
    public const string Klasse = Topic + ".Zusatzangabe";

    public static IReadOnlyList<string> Felder { get; } = Array.AsReadOnly(new[]
    {
        FieldKeys.ShaftShape, FieldKeys.InfiltrationType, "Tiefe", "Schachttiefe", "Dimension",
        "Material", "Funktion", FieldKeys.ShaftDimension1Mm, FieldKeys.ShaftDimension2Mm,
        FieldKeys.LoadClass, FieldKeys.InspectionYear, FieldKeys.PrimaryDamages,
        FieldKeys.RenovationDecision, FieldKeys.RecommendedRehabilitationMeasures, FieldKeys.Cost,
        FieldKeys.RehabilitationExecutor, FieldKeys.LinerRenovationCount, FieldKeys.LinerRenovationMeters,
        FieldKeys.ConnectionsToGrout, FieldKeys.RepairSleeve, FieldKeys.LinerEndSleeve,
        FieldKeys.ShortLinerRepair, "Erneuerung_Neubau_m", FieldKeys.SlopePromille,
        "Inspektionsrichtung", "Pruefungsresultat", "Referenzpruefung", "Gewaesserschutz",
        "Grundwasserspiegel", "VSA_Zustandsnote_D", "VSA_Zustandsnote_S", "VSA_Zustandsnote_B",
        "VSA_Geschaetzt", "Abdeckung Stk.", FieldKeys.Remarks, FieldKeys.Street, FieldKeys.GrossCost,
        FieldKeys.ProfileType, FieldKeys.ClearWidthMm,
        FieldKeys.UsageType, FieldKeys.ConditionClass, FieldKeys.OperatingStatus, FieldKeys.RehabilitationNeed,
        FieldKeys.ConstructionYear, FieldKeys.PipeMaterial, FieldKeys.NominalDiameterMm,
        FieldKeys.HoldingLengthMeters, FieldKeys.HierarchicalFunction, FieldKeys.ConnectionType,
        FieldKeys.BeddingEncasement, FieldKeys.HydraulicFunction, FieldKeys.PositionAccuracy
    });

    private static readonly IReadOnlyDictionary<string, string> Standardfelder = new Dictionary<string, string>
    {
        ["Material"] = "Material", ["Funktion"] = "Funktion", ["Dimension"] = "Dimension1",
        [FieldKeys.ShaftDimension1Mm] = "Dimension1", [FieldKeys.ShaftDimension2Mm] = "Dimension2",
        [FieldKeys.InfiltrationType] = "Art", [FieldKeys.Remarks] = "Bemerkung",
        [FieldKeys.Street] = "Standortname", [FieldKeys.GrossCost] = "Bruttokosten",
        [FieldKeys.UsageType] = "Nutzungsart_Ist", [FieldKeys.ConditionClass] = "BaulicherZustand",
        [FieldKeys.OperatingStatus] = "Status", [FieldKeys.RehabilitationNeed] = "Sanierungsbedarf",
        [FieldKeys.ConstructionYear] = "Baujahr", [FieldKeys.PipeMaterial] = "Material",
        [FieldKeys.NominalDiameterMm] = "Lichte_Hoehe", [FieldKeys.HoldingLengthMeters] = "LaengeEffektiv",
        [FieldKeys.HierarchicalFunction] = "FunktionHierarchisch", [FieldKeys.ConnectionType] = "Verbindungsart",
        [FieldKeys.BeddingEncasement] = "Bettung_Umhuellung", [FieldKeys.HydraulicFunction] = "FunktionHydraulisch",
        [FieldKeys.PositionAccuracy] = "Lagebestimmung"
    };

    public static bool IstErlaubt(string feld) => Felder.Contains(feld, StringComparer.Ordinal);
    public static string? Standardfeld(string feld) => Standardfelder.GetValueOrDefault(feld);

    public static string Schachtfeld(SchachtRecord record, string feld)
    {
        var normal = SchachtFeldnamen.Feld(record, feld);
        if (!string.IsNullOrWhiteSpace(record.GetFieldValue(normal))) return normal;
        var alias = feld switch
        {
            FieldKeys.RenovationDecision => "Ja/Nein",
            FieldKeys.RecommendedRehabilitationMeasures => "Massnahmen",
            _ => feld
        };
        return SchachtFeldnamen.Feld(record, alias);
    }

    public static XtfNeuPlan Ergaenze(XtfNeuPlan plan, Project projekt)
    {
        var objekte = plan.Objekte.ToList();
        var hinweise = plan.Hinweise.ToList();
        var ids = new XtfNeuKennungen(projekt.Id.ToString("N"));
        foreach (var gruppe in projekt.SchaechteData.GroupBy(s => XtfSchachtPlanBuilder.Wert(s, "Schachtnummer")?.Trim(), StringComparer.Ordinal))
        {
            if (gruppe.Count() != 1) continue;
            var s = gruppe.Single();
            var klasse = AbwasserbauwerkVokabular.Klasse(XtfSchachtPlanBuilder.Wert(s, FieldKeys.ShaftStructureType), XtfSchachtPlanBuilder.Wert(s, "Funktion"));
            ErgaenzeObjekt(gruppe.Key, klasse, key => s.GetFieldValue(Schachtfeld(s, key)));
        }
        foreach (var gruppe in projekt.Data.GroupBy(h => h.GetFieldValue(FieldKeys.HoldingName)?.Trim(), StringComparer.Ordinal))
        {
            if (gruppe.Count() != 1) continue;
            var h = gruppe.Single();
            ErgaenzeObjekt(gruppe.Key, "Haltung", h.GetFieldValue);
        }
        var anzahl = objekte.Count - plan.Objekte.Count;
        if (anzahl > 0)
            hinweise.Add($"{anzahl} Zusatzangaben in derselben XTF. Das Zusatzmodell {Modell}.ili wird mitgeliefert. FME muss diese Angaben ausdruecklich zuordnen; leere Felder loeschen nichts.");
        return plan with { Objekte = objekte, Hinweise = hinweise };

        void ErgaenzeObjekt(string? name, string? klasse, Func<string, string?> wert)
        {
            var treffer = plan.Objekte.Where(o => o.Klasse == klasse && o.Felder.Any(f => f.Key == "Bezeichnung" && f.Value == name)).ToArray();
            if (treffer.Length != 1) return;
            var ziel = treffer[0];
            var standard = ziel.Felder.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
            if (klasse == "Haltung")
            {
                var kanalTid = ziel.Verweise.FirstOrDefault(v => v.Name == "AbwasserbauwerkRef")?.ZielTid;
                var kanal = plan.Objekte.FirstOrDefault(o => o.Klasse == "Kanal" && o.Tid == kanalTid);
                if (kanal is not null) standard.UnionWith(kanal.Felder.Select(f => f.Key));
            }
            foreach (var feld in Felder)
            {
                if (Standardfelder.TryGetValue(feld, out var norm) && standard.Contains(norm)) continue;
                var text = wert(feld)?.Trim();
                if (string.IsNullOrEmpty(text)) continue;
                objekte.Add(new XtfNeuObjekt("Zusatzangabe", ids.Fuer("Zusatzangabe", ziel.Tid + "|" + feld),
                    [new("ObjektTid", ziel.Tid), new("Feld", feld), new("Wert", text)], [], ImTopicZusatz: true));
            }
        }
    }
}

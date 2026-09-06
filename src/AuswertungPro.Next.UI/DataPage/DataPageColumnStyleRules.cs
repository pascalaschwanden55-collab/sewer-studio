using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova-Etappe 2 (Inventar 4.3, NUM_COLS): Namensspalte fett, Zahlenspalten rechtsbuendig in
/// der Datenschrift. Reine Regel; die Spaltenfabriken wenden sie nur an.
/// </summary>
public static class DataPageColumnStyleRules
{
    private static readonly HashSet<string> Zahlen = new(StringComparer.Ordinal)
    {
        FieldKeys.NominalDiameterMm, FieldKeys.ClearWidthMm, FieldKeys.HoldingLengthMeters,
        FieldKeys.ConstructionYear, "VSA_Zustandsnote_D", "VSA_Zustandsnote_S", "VSA_Zustandsnote_B",
        FieldKeys.LinerRenovationMeters, FieldKeys.LinerRenovationCount, FieldKeys.ConnectionsToGrout,
        FieldKeys.RepairSleeve, FieldKeys.LinerEndSleeve, FieldKeys.ShortLinerRepair,
        "Erneuerung_Neubau_m", FieldKeys.Cost, FieldKeys.GrossCost, FieldKeys.SlopePromille,
        FieldKeys.ShaftDimension1Mm, FieldKeys.ShaftDimension2Mm
    };

    public static bool IstNamensspalte(string feld) => string.Equals(feld, FieldKeys.HoldingName, StringComparison.Ordinal);

    public static bool IstZahlenspalte(string feld) => Zahlen.Contains(feld);
}

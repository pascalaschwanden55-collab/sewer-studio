using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova-Etappe 2 (Inventar 4.3, NUM_COLS): Namensspalte fett, Zahlenspalten rechtsbuendig in
/// der Datenschrift. Reine Regel; die Spaltenfabriken wenden sie nur an.
///
/// Nova-Etappe 2b: deckt zusaetzlich Baujahr, alle Sanierungsmengen (Renovierung Inliner,
/// Anschluesse verpressen, Reparatur Manschette/Kurzliner, Linerendmanschette, Erneuerung
/// Neubau) sowie beide Schachtmasse ab. Ein im Task-1-Brief genanntes Feld "Tiefe_m" gibt es
/// weder in <see cref="FieldKeys"/> noch in <see cref="FieldCatalog"/> — es wird bewusst nicht
/// aufgenommen (siehe Bericht Nova-Etappe 2b, Task 1).
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

    /// <summary>
    /// Nova-Etappe 2b: Hoechstens drei Zeilen je Tabellenzelle (rund 18 px Zeilenhoehe).
    ///
    /// Anlass ist Pascals Bild vom 07.09.: Ein Zellinhalt mit Zeilenumbruechen ("Primaere
    /// Schaeden", "Empfohlene Massnahmen") zog die ganze Zeile auf und liess in "Alle Spalten"
    /// nur sechs Haltungen sichtbar. Der Wert wird nur angezeigt begrenzt — gespeicherter Text,
    /// Bearbeitung und Export bleiben vollstaendig.
    /// </summary>
    public const double MaximaleZellenhoehe = 54;

    public static bool IstNamensspalte(string feld) => string.Equals(feld, FieldKeys.HoldingName, StringComparison.Ordinal);

    public static bool IstZahlenspalte(string feld) => Zahlen.Contains(feld);
}

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Zentrale Feldnamen, die im Projektformat persistiert werden.
/// Werte nicht umbenennen: sie sind Teil der gespeicherten Projektdateien.
/// </summary>
public static class FieldKeys
{
    public const string HoldingName = "Haltungsname";
    public const string Street = "Strasse";
    public const string PipeMaterial = "Rohrmaterial";
    public const string NominalDiameterMm = "DN_mm";
    public const string UsageType = "Nutzungsart";
    public const string HoldingLengthMeters = "Haltungslaenge_m";
    public const string ConditionClass = "Zustandsklasse";
    public const string RenovationDecision = "Sanieren_JaNein";
    public const string RecommendedRehabilitationMeasures = "Empfohlene_Sanierungsmassnahmen";
    public const string Cost = "Kosten";
    public const string Owner = "Eigentuemer";
    public const string RehabilitationExecutor = "Ausgefuehrt_durch";
    public const string WorkflowStatus = "Offen_abgeschlossen";
    public const string InspectionYear = "Datum_Jahr";
    public const string Remarks = "Bemerkungen";
    public const string Link = "Link";
    public const string LinerRenovationCount = "Renovierung_Inliner_Stk";
    public const string LinerRenovationMeters = "Renovierung_Inliner_m";
    public const string ConnectionsToGrout = "Anschluesse_verpressen";
    public const string RepairSleeve = "Reparatur_Manschette";
    public const string LinerEndSleeve = "Linerendmanschette_LEM";
    public const string ShortLinerRepair = "Reparatur_Kurzliner";
    public const string SlopePromille = "Gefaelle_Promille";
    public const string PdfPath = "PDF_Path";
    public const string PdfEigen = "PDF_Eigen";
    public const string PdfAll = "PDF_All";
}

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
    public const string PrimaryDamages = "Primaere_Schaeden";
    public const string ConditionClass = "Zustandsklasse";
    public const string RenovationDecision = "Sanieren_JaNein";
    public const string RecommendedRehabilitationMeasures = "Empfohlene_Sanierungsmassnahmen";
    public const string Cost = "Kosten";
    public const string Owner = "Eigentuemer";
    public const string RehabilitationExecutor = "Ausgefuehrt_durch";

    /// <summary>
    /// Belastungsklasse der Schachtabdeckung nach EN 124 (A15 bis F900). Steht neben
    /// "Abdeckung Stk." und sagt, welche Last die Abdeckung tragen darf.
    /// </summary>
    public const string LoadClass = "Belastungsklasse";
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

    /// <summary>
    /// Funktionale Hierarchie nach SIA405 (<c>Kanal.FunktionHierarchisch</c>), zweistufig
    /// als <c>PAA.Sammelkanal</c> oder <c>SAA.Liegenschaftsentwaesserung</c>.
    /// Der Feldname bestand schon vor den Exportfeldern und bleibt unveraendert.
    /// </summary>
    public const string HierarchicalFunction = "FunktionHierarchisch";

    /// <summary>
    /// Rohrverbindung nach SIA405 (<c>Kanal.Verbindungsart</c>), etwa <c>Steckmuffen</c>.
    /// </summary>
    public const string ConnectionType = "Verbindungsart";

    /// <summary>
    /// Bettung und Umhuellung nach SIA405 (<c>Kanal.Bettung_Umhuellung</c>),
    /// etwa <c>SIA_Typ2</c> oder <c>in_Kanal_aufgehaengt</c>.
    /// </summary>
    public const string BeddingEncasement = "Bettung_Umhuellung";

    /// <summary>
    /// Profiltyp nach SIA405. Er haengt dort nicht am Kanal, sondern an der eigenen
    /// Klasse <c>Rohrprofil</c>, auf die die Haltung ueber <c>RohrprofilRef</c> zeigt.
    /// </summary>
    public const string ProfileType = "Profiltyp";

    /// <summary>
    /// Lichte Breite in Millimetern — bei Ei-, Maul- und Rechteckprofilen die zweite
    /// Abmessung neben <see cref="NominalDiameterMm"/>.
    ///
    /// Bewusst OHNE Ziel in der XTF: <c>SIA405_ABWASSER_2020_1_LV95</c> kennt an der
    /// Klasse <c>Haltung</c> nur <c>Lichte_Hoehe</c>. Das Feld dient der Dokumentation
    /// und dem WinCan-Weg und wird nicht exportiert.
    /// </summary>
    public const string ClearWidthMm = "Lichte_Breite_mm";
    public const string PdfPath = "PDF_Path";
    public const string PdfEigen = "PDF_Eigen";
    public const string PdfAll = "PDF_All";
}

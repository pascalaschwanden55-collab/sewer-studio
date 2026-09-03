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
    /// Abmessung neben <see cref="NominalDiameterMm"/>. In SIA405 entsteht daraus am
    /// Rohrprofil das Hoehen-Breiten-Verhaeltnis.
    /// </summary>
    public const string ClearWidthMm = "Lichte_Breite_mm";

    // --- Die uebrigen Felder der Kataster-Infobox (2026-09-02) ---
    // Fuenf davon haben in SIA405 ein Ziel und gehen in die Revision; die sechs
    // Herkunftsangaben darunter bleiben reine Programmfelder.

    /// <summary>Betriebszustand nach SIA405 (<c>Abwasserbauwerk.Status</c>), 5 Werte.</summary>
    public const string OperatingStatus = "Status";

    /// <summary>Sanierungsbedarf nach SIA405 (<c>Abwasserbauwerk.Sanierungsbedarf</c>), 6 Werte.</summary>
    public const string RehabilitationNeed = "Sanierungsbedarf";

    /// <summary>Hydraulische Funktion nach SIA405 (<c>Kanal.FunktionHydraulisch</c>), 12 Werte.</summary>
    public const string HydraulicFunction = "FunktionHydraulisch";

    /// <summary>Lagegenauigkeit nach SIA405 (<c>Haltung.Lagebestimmung</c>), 3 Werte.</summary>
    public const string PositionAccuracy = "Lagebestimmung";

    /// <summary>Baujahr nach SIA405 (<c>Abwasserbauwerk.Baujahr</c>), 1800 bis 2100.</summary>
    public const string ConstructionYear = "Baujahr";

    /// <summary>Form des Schachts nach der Urner GEONIS-Auswahl.</summary>
    public const string ShaftShape = "Schachtform";

    /// <summary>
    /// Bruttokosten des Bauwerks aus dem Kataster (<c>Abwasserbauwerk.Bruttokosten</c>).
    /// NICHT zu verwechseln mit <see cref="Cost"/> — das sind die von SewerStudio
    /// gerechneten Sanierungskosten. Beide Felder stehen bewusst nebeneinander.
    /// </summary>
    public const string GrossCost = "Bruttokosten";

    /// <summary>
    /// Kennung des Objekts im Kataster. Reines Programmfeld: Die XTF kennt dafuer kein
    /// Attribut, die Identitaet dort ist die TID.
    /// </summary>
    public const string CadastreObjectId = "Objekt_ID";

    /// <summary>
    /// Datenherr aus dem Kataster. Reines Programmfeld — in SIA405 ist das ein Verweis
    /// auf eine Organisation, und SewerStudio ist nicht der Datenherr dieser Leitungen.
    /// </summary>
    public const string DataOwner = "Datenherr";

    /// <summary>Datenlieferant aus dem Kataster. Reines Programmfeld, wie <see cref="DataOwner"/>.</summary>
    public const string DataSupplier = "Datenlieferant";

    /// <summary>
    /// Organisation aus dem Kataster. Reines Programmfeld; im Abwassernetz des Kantons
    /// ist die Spalte bei allen 110297 Leitungen leer.
    /// </summary>
    public const string CadastreOrganisation = "Organisation";

    /// <summary>
    /// Letzte Aenderung aus dem Kataster. Reines Programmfeld: In der Revision fuehrt
    /// <c>XtfRevisionWriter</c> dieses Feld selbst nach, wo die Datei es fuehrt.
    /// </summary>
    public const string CadastreLastChange = "Letzte_Aenderung";

    /// <summary>
    /// Aktualisierungsdatum des Katasterauszugs. Reines Programmfeld — SIA405 kennt
    /// dieses Feld nicht, es ist die Buchhaltung des QGIS-Exports.
    /// </summary>
    public const string CadastreUpdatedAt = "Aktualisierungsdatum";

    /// <summary>
    /// Schachtabmessung 1 in Millimetern — laut GEONIS-Maske das GROESSTE Innenmass.
    /// Getrennt vom bestehenden Textfeld <c>Dimension</c> ("600 mm", "1100 x 900 mm"),
    /// das Excel-Bericht, PDF- und SchachtPro-Import weiter fuehren.
    ///
    /// Der Name ist bewusst lesbar: Am Schacht ist der Feldname zugleich die
    /// Spaltenueberschrift in der Tabelle.
    /// </summary>
    public const string ShaftDimension1Mm = "Dimension 1 mm";

    /// <summary>Schachtabmessung 2 in Millimetern — das KLEINSTE Innenmass.</summary>
    public const string ShaftDimension2Mm = "Dimension 2 mm";
    public const string PdfPath = "PDF_Path";
    public const string PdfEigen = "PDF_Eigen";
    public const string PdfAll = "PDF_All";
}

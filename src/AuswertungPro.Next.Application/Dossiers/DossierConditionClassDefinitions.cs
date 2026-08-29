using System.Collections.ObjectModel;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Lesbare Bedeutung einer im Eigentuemerdossier verwendeten Zustandsklasse.
/// Der gespeicherte Wert bleibt die Ziffer 0 bis 4; <see cref="Code"/> ist die
/// im Dossier sichtbare Schreibweise Z0 bis Z4.
/// </summary>
public sealed record DossierConditionClassDefinition(
    string Value,
    string Name,
    string Orientation,
    string Description)
{
    public string Code => "Z" + Value;
}

/// <summary>
/// Gemeinsame, WPF- und PDF-freie Erklaerung der Zustandsklassen.
/// Z0 ist der schlechteste, Z4 der beste Zustand.
/// </summary>
public static class DossierConditionClassDefinitions
{
    public const string PdfHeading = "Zustandsklassen Z0 bis Z4";
    public const string PdfSubtitle = "Erklärung zum Eigentümerdossier";
    public const int PdfRequiredPageCount = 1;
    public const string PdfRequiredPageMarker =
        "SEWERSTUDIO_DOSSIER_ZUSTANDSKLASSEN_PFLICHTBLATT_V1";
    public const string ClassificationBasisHeading = "Wie die Einstufung entsteht";
    public const string ClassificationBasisSource =
        "Grundlage: VSA, Zustandsbeurteilung von Entwässerungsanlagen, "
        + "Kapitel 2.2-2.3 (PDF-Seite 12 / Dokumentseite 10).";
    public const string NotCalculatedNote =
        "Ein Strich (-) bedeutet: Es ist keine Berechnung gemäss der Richtlinie "
        + "vorhanden. Dieser Status ist nicht mit Z4 gleichzusetzen.";
    public static IReadOnlyList<string> ClassificationBasisNotes { get; } =
        new ReadOnlyCollection<string>(
        [
            "Grundlage sind vollständige Bauwerksdaten sowie korrekt erfasste "
            + "Befundcodes mit Ausmass und Lage.",
            "Fehlende oder fehlerhafte Angaben sind vor der Klassifizierung zu "
            + "korrigieren. Die rechnerisch ermittelte Zustandsklasse muss eine "
            + "qualifizierte Fachperson prüfen.",
            "Die Zeitspanne rechts dient nur als Orientierung. Zustandsklasse und "
            + "Dringlichkeitszahl haben keine feste Zuordnung. Schutzbereiche, Nutzung, "
            + "Grundwasserlage und Netzbedeutung beeinflussen die "
            + "Sanierungsdringlichkeit - nicht die Zustandsklasse."
        ]);

    public static IReadOnlyList<DossierConditionClassDefinition> All { get; } =
        new ReadOnlyCollection<DossierConditionClassDefinition>(
        [
            new(
                "0",
                "Nicht mehr funktionstüchtig",
                "Sofort (innerhalb eines Jahres)",
                "Das Abwasserbauwerk ist bereits oder demnächst nicht mehr durchgängig "
                + "und ist undicht, da es eingestürzt, vollständig verwurzelt ist "
                + "und/oder andere Abflusshindernisse bestehen."),
            new(
                "1",
                "Starke Defizite",
                "Kurzfristig (innerhalb der nächsten 3 Jahre)",
                "Es bestehen Defizite, bei welchen die statische Sicherheit, Hydraulik "
                + "oder Dichtheit nicht mehr gewährleistet ist: Rohrbrüche axial oder "
                + "radial, Rohrdeformationen, visuell sichtbare Wassereintritte oder "
                + "Wasseraustritte, Löcher in der Rohrwand, stark vorstehende seitliche "
                + "Anschlüsse, stark ausgewaschene Rohrwandung etc."),
            new(
                "2",
                "Mittlere Defizite",
                "Mittelfristig (innerhalb der nächsten 8 Jahre)",
                "Defizite, welche die Statik, Hydraulik oder Dichtheit beeinträchtigen: "
                + "breite Rohrfugen, nicht verputzte seitliche Anschlüsse, Risse, leichte "
                + "Abflusshindernisse wie Verkalkungen, vorstehende seitliche Anschlüsse, "
                + "leichte Rohrwandbeschädigungen, einzelne Wurzeleinwüchse, Rohrwandung "
                + "ausgewaschen usw."),
            new(
                "3",
                "Leichte Defizite",
                "Langfristig (mehr als 8 Jahre)",
                "Es bestehen Defizite oder Vorkommnisse, welche für die Dichtheit, "
                + "Hydraulik oder Rohrstatik einen unbedeutenden Einfluss haben: breite "
                + "Rohrfugen, schlecht verputzte seitliche Anschlüsse, leichte Deformation "
                + "bei Kunststoffleitungen, leichte Auswaschungen der Rohrwandung etc."),
            new(
                "4",
                "Keine Defizite",
                "Keine Sanierungsmassnahmen bis zur nächsten Zustandserfassung "
                + "und Zustandsbeurteilung erforderlich",
                "Keine relevanten Defizite festgestellt.")
        ]);
}

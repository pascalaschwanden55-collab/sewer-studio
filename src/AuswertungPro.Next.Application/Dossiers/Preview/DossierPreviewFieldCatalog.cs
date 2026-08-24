using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers.Preview;

/// <summary>Wie ein Feld in der Vorschau bearbeitet wird.</summary>
public enum DossierPreviewFieldKind
{
    /// <summary>Einzeilige Eingabe.</summary>
    Text,

    /// <summary>Mehrzeilige Eingabe.</summary>
    MultiLine,

    /// <summary>Dateiauswahl, zum Beispiel der Uebersichtsplan.</summary>
    File,

    /// <summary>Zeilenliste mit Reihenfolge.</summary>
    Rows,

    /// <summary>
    /// Wird aus anderen Angaben berechnet und ist hier nicht aenderbar. Der
    /// Hinweis sagt, wo der Wert herkommt — ein Feld, das stumm nichts tut,
    /// waere schlimmer als gar keines.
    /// </summary>
    Derived
}

/// <summary>
/// Ein bearbeitbares Feld der Vorschau. <paramref name="Key"/> ist der
/// Platzhalter der Vorlage; mehrere Felder duerfen sich einen Platzhalter
/// teilen (PLZ und Ort bilden gemeinsam eine Zeile).
/// </summary>
public sealed record DossierPreviewField(
    string Key,
    string Label,
    DossierPreviewFieldKind Kind,
    Func<string> Read,
    Action<string>? Write,
    string Hint = "");

/// <summary>
/// Verbindet die Platzhalter der Vorlage mit den Angaben, aus denen sie
/// entstehen. Reine Logik: kein Word, keine Oberflaeche.
///
/// Der Katalog ist die einzige Stelle, an der diese Zuordnung steht. Fehlt ein
/// Platzhalter hier, erscheint er in der Vorschau als "wird berechnet" statt
/// als Feld, das nichts bewirkt.
/// </summary>
public static class DossierPreviewFieldCatalog
{
    public static IReadOnlyList<DossierPreviewField> Build(
        DossierAreaSettings area, DossierDefinition dossier)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(dossier);

        return new List<DossierPreviewField>
        {
            Text("Gebietstitel", "Gebietstitel",
                () => area.AreaTitle, w => area.AreaTitle = w),
            Text("Gebiet_Ort", "Zweite Deckblattzeile",
                () => area.AreaLocation, w => area.AreaLocation = w),
            Text("Parzellen_Zeile", "Parzellen-Nr.",
                () => dossier.ParcelNumbers, w => dossier.ParcelNumbers = w),
            Derived("Eigentuemer_Block", "Eigentümer auf dem Deckblatt",
                "Entsteht aus der Tabelle „Eigentumsverhältnisse“."),
            Text("Adresse_Zeile", "Strasse",
                () => dossier.Address, w => dossier.Address = w),
            Text("Adresse_Zeile", "Haus-Nr.",
                () => dossier.HouseNumbers, w => dossier.HouseNumbers = w),
            Text("Ort_Zeile", "PLZ",
                () => dossier.PostalCode, w => dossier.PostalCode = w),
            Text("Ort_Zeile", "Ort",
                () => dossier.Town, w => dossier.Town = w),
            Derived("Datum", "Datum", "Immer das heutige Datum."),
            Text("Revision", "Revision",
                () => dossier.Revision, w => dossier.Revision = w),
            Text("Projekt_Nr", "Proj. Nr. AWU",
                () => area.ProjectNumber, w => area.ProjectNumber = w),
            Text("Gezeichnet", "Gez.",
                () => area.DrawnBy, w => area.DrawnBy = w),

            Rows("Aenderungen", "Änderungswesen"),
            Derived("Datum_Lang", "Erstellungsdatum", "Immer das heutige Datum."),
            Text("Autoren", "Autoren",
                () => area.Authors, w => area.Authors = w),

            File("Uebersichtsplan", "Übersichtsplan",
                () => dossier.OverviewPlanPath, w => dossier.OverviewPlanPath = w),

            Rows("Eigentuemer", "Eigentumsverhältnisse"),

            Derived("Haltungen_Text", "Betroffene Leitungen",
                "Entsteht aus den gewählten Leitungen des Projekts."),
            Derived("Haltungen_Summe", "Zusammenzug", "Anzahl, Länge und Kosten der Leitungen."),

            Rows("Themen", "Themen der Informationstabelle"),
            MultiLine("Aktennotiz", "Für die Aktennotiz",
                () => dossier.FileNote, w => dossier.FileNote = w),
            Text("Rueckmeldung", "Rückmeldefrist",
                () => dossier.ResponseDeadlineOverride ?? area.ResponseDeadline,
                w => dossier.ResponseDeadlineOverride = string.IsNullOrWhiteSpace(w) ? null : w),
            Text("Fusszeile", "Fusszeile",
                () => area.FooterLine, w => area.FooterLine = w)
        };
    }

    /// <summary>
    /// Die Felder genau einer Seite, in der Reihenfolge des Katalogs. Ein
    /// Platzhalter ohne Eintrag im Katalog wird sichtbar als berechnet
    /// gemeldet, statt lautlos zu fehlen.
    /// </summary>
    public static IReadOnlyList<DossierPreviewField> ForPage(
        IReadOnlyList<DossierPreviewField> alle, DossierPreviewPage page)
    {
        ArgumentNullException.ThrowIfNull(alle);
        ArgumentNullException.ThrowIfNull(page);

        var ergebnis = alle
            .Where(f => page.FieldKeys.Contains(f.Key, StringComparer.Ordinal))
            .ToList();

        foreach (var key in page.FieldKeys)
        {
            if (!alle.Any(f => string.Equals(f.Key, key, StringComparison.Ordinal)))
                ergebnis.Add(Derived(key, key, "Wird aus anderen Angaben berechnet."));
        }

        return ergebnis;
    }

    private static DossierPreviewField Text(
        string key, string label, Func<string> read, Action<string> write)
        => new(key, label, DossierPreviewFieldKind.Text, () => read() ?? "", write);

    private static DossierPreviewField MultiLine(
        string key, string label, Func<string> read, Action<string> write)
        => new(key, label, DossierPreviewFieldKind.MultiLine, () => read() ?? "", write);

    private static DossierPreviewField File(
        string key, string label, Func<string> read, Action<string> write)
        => new(key, label, DossierPreviewFieldKind.File, () => read() ?? "", write);

    private static DossierPreviewField Rows(string key, string label)
        => new(key, label, DossierPreviewFieldKind.Rows, () => "", null);

    private static DossierPreviewField Derived(string key, string label, string hint)
        => new(key, label, DossierPreviewFieldKind.Derived, () => "", null, hint);
}

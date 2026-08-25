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
    string Hint = "",
    Func<bool>? IsOverridden = null,
    Action? Reset = null,
    string StyleKey = "")
{
    /// <summary>
    /// Wahr, wenn dieses Dossier hier etwas Eigenes fuehrt statt des
    /// berechneten Werts.
    /// </summary>
    public bool Overridden => IsOverridden?.Invoke() ?? false;

    public bool CanReset => Reset is not null;

    public string FormattingKey => StyleKey.Length > 0 ? StyleKey : Key;
}

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
    /// <summary>
    /// <paramref name="computed"/> liefert den berechneten Wert einer Stelle.
    /// Er ist die Vorgabe; sobald das Dossier etwas Eigenes fuehrt, gilt das.
    /// </summary>
    public static IReadOnlyList<DossierPreviewField> Build(
        DossierAreaSettings area,
        DossierDefinition dossier,
        Func<string, string>? computed = null)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(dossier);

        var berechnet = computed ?? (_ => string.Empty);

        DossierPreviewField Eigen(
            string key, string label, DossierPreviewFieldKind kind, string hint)
            => new(
                key,
                label,
                kind,
                () => dossier.FieldOverrides.TryGetValue(key, out var wert)
                    ? wert
                    : berechnet(key),
                wert => dossier.FieldOverrides[key] = wert ?? string.Empty,
                hint,
                () => dossier.FieldOverrides.ContainsKey(key),
                () => dossier.FieldOverrides.Remove(key));

        return new List<DossierPreviewField>
        {
            Text("Gebietstitel", "Gebietstitel",
                () => area.AreaTitle, w => area.AreaTitle = w),
            Text("Gebiet_Ort", "Zweite Deckblattzeile",
                () => area.AreaLocation, w => area.AreaLocation = w),
            Text("Parzellen_Zeile", "Parzellen-Nr.",
                () => dossier.ParcelNumbers, w => dossier.ParcelNumbers = w),
            // Eine eigene Karte je zusätzlichem Punkt. Word kennt diese
            // externen Beilagen nicht und kann ihnen keine Seitenzahl geben;
            // nummeriert werden sie beim Export fortlaufend nach den Kapiteln.
            Rows("Verzeichnis_Beilagen", "Inhaltsverzeichnis ergänzen"),
            Eigen("Eigentuemer_Block", "Eigentümer auf dem Deckblatt",
                DossierPreviewFieldKind.MultiLine,
                "Entsteht sonst aus der Tabelle „Eigentumsverhältnisse“."),
            Eigen("Adresse_Zeile", "Strasse und Haus-Nr.", DossierPreviewFieldKind.Text,
                "Entsteht sonst aus Strasse und Hausnummer."),
            Eigen("Ort_Zeile", "PLZ und Ort", DossierPreviewFieldKind.Text,
                "Entsteht sonst aus PLZ und Ort."),
            Eigen("Datum", "Datum", DossierPreviewFieldKind.Text,
                "Sonst das heutige Datum."),
            Text("Revision", "Revision",
                () => dossier.Revision, w => dossier.Revision = w),
            Text("Projekt_Nr", "Proj. Nr. AWU",
                () => area.ProjectNumber, w => area.ProjectNumber = w),
            Text("Gezeichnet", "Gez.",
                () => area.DrawnBy, w => area.DrawnBy = w),

            Rows("Aenderungen", "Änderungswesen"),
            Eigen("Datum_Lang", "Erstellungsdatum", DossierPreviewFieldKind.Text,
                "Sonst das heutige Datum."),
            Text("Autoren", "Autoren",
                () => area.Authors, w => area.Authors = w),

            File("Uebersichtsplan", "Übersichtsplan",
                () => dossier.OverviewPlanPath, w => dossier.OverviewPlanPath = w),

            Rows("Eigentuemer", "Eigentumsverhältnisse"),

            Eigen("Haltungen_Text", "Betroffene Leitungen",
                DossierPreviewFieldKind.MultiLine,
                "Entsteht sonst aus den gewählten Leitungen des Projekts."),
            Eigen("Haltungen_Summe", "Zusammenzug", DossierPreviewFieldKind.Text,
                "Sonst Anzahl, Länge und Kosten der Leitungen."),
            Eigen("Schaechte_Text", "Betroffene Schächte",
                DossierPreviewFieldKind.MultiLine,
                "Entsteht sonst aus den Schächten der gewählten Leitungen."),

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
        IReadOnlyList<DossierPreviewField> alle,
        DossierPreviewPage page,
        DossierDefinition? dossier = null,
        Func<string, string>? computed = null)
    {
        ArgumentNullException.ThrowIfNull(alle);
        ArgumentNullException.ThrowIfNull(page);

        var ergebnis = alle
            .Where(f => page.FieldKeys.Contains(f.Key, StringComparer.Ordinal))
            .ToList();

        foreach (var key in page.FieldKeys)
        {
            if (alle.Any(f => string.Equals(f.Key, key, StringComparison.Ordinal)))
                continue;

            // Auch eine Stelle, die der Katalog nicht kennt, muss von Hand zu
            // fuellen sein — sonst gaebe es im Blatt einen Platz ohne Eingabe.
            if (dossier is null)
            {
                ergebnis.Add(Derived(key, key, "Wird aus anderen Angaben berechnet."));
                continue;
            }

            var berechnet = computed ?? (_ => string.Empty);
            var eigener = key;

            ergebnis.Add(new DossierPreviewField(
                eigener,
                eigener,
                DossierPreviewFieldKind.MultiLine,
                () => dossier.FieldOverrides.TryGetValue(eigener, out var wert)
                    ? wert
                    : berechnet(eigener),
                wert => dossier.FieldOverrides[eigener] = wert ?? string.Empty,
                "Wird sonst aus anderen Angaben berechnet.",
                () => dossier.FieldOverrides.ContainsKey(eigener),
                () => dossier.FieldOverrides.Remove(eigener)));
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

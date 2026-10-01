using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Stellt ein gespeichertes Dossier-Dokument auf die aktuelle Formatversion um.
/// Reine Logik ohne Dateisystem.
///
/// Version 1 kannte je Liegenschaft genau einen Eigentuemer in Einzelfeldern.
/// Version 2 hat eine Zeilenliste. Die Einzelfelder bleiben erhalten — sie
/// speisen weiterhin das Deckblatt, und ein Wegwerfen waere Datenverlust.
/// Version 5 speichert additive Zeichenformatierungen. Alte Dokumente brauchen
/// dafuer keine Ableitung; leere Formatlisten bedeuten weiterhin Vorlagenformat.
/// Version 6 fuehrt die zusaetzlichen Verzeichniszeilen der Beilagen.
/// Version 7 ergänzt deren frei bearbeitbare Seitenzahlen.
/// Version 8 verbindet Titel und Seitenzahl zu einem untrennbaren Eintrag.
/// </summary>
public static class DossierDocumentMigration
{
    /// <summary>
    /// Version, ab der die Eigentuemerzeilen im Dokument selbst stehen. Nur
    /// Dateien darunter brauchen die Ableitung aus den Altfeldern.
    /// </summary>
    private const int OwnersStoredFromVersion = 2;

    /// <summary>
    /// Version, ab der die Themen der Tabelle "Informationen" als Liste im
    /// Dokument stehen. Darunter werden sie einmalig aus den Einzelfeldern
    /// abgeleitet — mit derselben festen Grenze wie oben und aus demselben
    /// Grund: ein bewusst geloeschtes Thema darf nie zurueckkehren.
    /// </summary>
    private const int TopicsStoredFromVersion = 4;

    /// <summary>
    /// Die Standardthemen eines Gebiets in der Reihenfolge des Originaldossiers.
    /// Zweiter Wert ist das Altfeld, aus dem der Text stammt (leer = neu).
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultTopicTitles = new[]
    {
        "Ausführungstermin",
        "Ansprechpartner",
        "Unternehmer",
        "Örtliche Bauleitung",
        "Behinderungen, Zugänge, Verkehrsführung, Fussgängerführung",
        "Ausgangslage",
        DossierTopicTitles.Schaeden,
        DossierTopicTitles.Sanierungskonzept,
        DossierTopicTitles.Kostenschaetzung + " Abwasser Uri",
        "Bemerkungen",
        "Beilagen"
    };

    /// <summary>
    /// Vorbelegte Texte fuer neue Gebietsthemen. Nur dort, wo eine Vorgabe
    /// wirklich hilft — der Rest bleibt leer statt erfunden.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultTopicTexts =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Stehende Texte aus der Wordvorlage. Sie werden von Gebiet zu Gebiet
            // weitergetragen und nur wenig geaendert. Der Ort steht als Platzhalter
            // darin, damit ein Dossier eines anderen Gebiets nicht die falsche
            // Strasse nennt; aufgeloest wird er beim Fuellen wie jeder Platzhalter.
            ["Ausgangslage"] = "Abwasser Uri (AWU) hat gemäss kantonalem Umweltgesetz (KUG) als Betreiberin der öffentlichen Abwasseranlagen im Kanton Uri die einwandfreie Funktion der Kanalisationsleitungen zu gewährleisten. Ebenfalls hat AWU die Abwasseranlagen der Gemeinden und Privaten, die nicht der Groberschliessung dienen zu beaufsichtigen. Die Abwasseranlagen im öffentlichen Bereich{{Gebiet_Perimeter}} sowie die angrenzenden privaten Liegenschaften wurden kontrolliert, einschliesslich der Abwasserschächte. Diverse Schäden wurden an den Leitungen und Schächten festgestellt. Diese Schäden erfordern eine Sanierung, um die ordnungsgemässe Funktionalität, Dichtheit und Sicherheit des Systems wiederherzustellen.",

            ["Behinderungen, Zugänge, Verkehrsführung, Fussgängerführung"] =
                "Die Zugänge sollten normal möglich sein, wenn nötig werden Provisorien für die Zugänge erstellt.",

            ["Bemerkungen"] = "Allfällige Leistungen von Versicherungen sind vor der Sanierung durch die Eigentümer abzuklären.",

            ["Beilagen"] =
                "Situation Liegenschaft GIS\n"
                + "Situation Abwasserleitungen der TV-Aufnahmen\n"
                + "TV-Haltungsprotokolle\n"
                + "Offerte"
        };


    /// <summary>
    /// Die Standardthemen mit ihren stehenden Texten. Ein Gebiet ohne Themen erhaelt
    /// diese Liste - sonst bleibt die Tabelle "Informationen Sanierung" im fertigen
    /// Dossier leer. Die Regel stand bisher nur im Gebietsfenster und wirkte deshalb
    /// nur, wenn jemand den Dialog oeffnete und speicherte.
    /// </summary>
    public static List<DossierTopicRow> BuildDefaultTopics()
        => DefaultTopicTitles
            .Select(titel => new DossierTopicRow
            {
                Title = titel,
                Text = DefaultTopicTexts.TryGetValue(titel, out var text) ? text : string.Empty
            })
            .ToList();

    public static bool NeedsTopicDerivation(int schemaVersion)
        => schemaVersion < TopicsStoredFromVersion;

    /// <summary>
    /// Die Ableitung aus den Altfeldern ist eine EINMALIGE Umstellung von
    /// Version 1 auf Version 2. Sie darf bei einem neueren Dokument nie erneut
    /// laufen: sonst kommt eine bewusst geloeschte Eigentuemerzeile beim
    /// naechsten Laden zurueck, oder ein neu angelegtes Dossier bekommt eine
    /// Zeile, die niemand eingegeben hat.
    ///
    /// Die Grenze ist bewusst fest auf Version 1 gebunden und NICHT auf
    /// "kleiner als die aktuelle Version": bei der naechsten Versionserhoehung
    /// wuerde die Ableitung sonst fuer jede Version-2-Datei erneut laufen.
    /// </summary>
    public static bool NeedsOwnerDerivation(int schemaVersion)
        => schemaVersion < OwnersStoredFromVersion;

    public static DossierDocument MigrateToCurrent(DossierDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Area ??= new DossierAreaSettings();
        document.Dossiers ??= new List<DossierDefinition>();

        var isLegacyDocument = NeedsOwnerDerivation(document.SchemaVersion);
        var brauchtThemen = NeedsTopicDerivation(document.SchemaVersion);
        var brauchtVerzeichnisEintraege = document.SchemaVersion < 8;

        document.Area.Topics ??= new List<DossierTopicRow>();
        foreach (var topic in document.Area.Topics.Where(topic => topic is not null))
            topic.StyleRanges ??= new List<DossierTextStyleRange>();

        if (brauchtThemen && document.Area.Topics.Count == 0)
            document.Area.Topics.AddRange(BuildAreaTopics(document.Area));

        // Auch ein aktuelles Gebiet kann ohne Themen dastehen (real: Projekt
        // Feldliweg, 0 Themen bei 15 Dossiers). Dann gilt dieselbe Regel wie im
        // Gebietsfenster. Ein Gebiet MIT Themen wird nie ergaenzt oder ueberschrieben.
        //
        // Das geschieht GENAU EINMAL. Danach ist das Gebiet eingerichtet, und eine
        // leere Liste ist eine Entscheidung des Benutzers - kein fehlender Stand.
        // Ohne diese Unterscheidung waere "alle Themen loeschen" nicht speicherbar:
        // beim naechsten Laden staenden alle elf wieder da.
        if (!document.Area.TopicsInitialized)
        {
            if (document.Area.Topics.Count == 0)
                document.Area.Topics.AddRange(BuildDefaultTopics());

            document.Area.TopicsInitialized = true;
        }

        foreach (var dossier in document.Dossiers)
        {
            dossier.Owners ??= new List<DossierOwnerRow>();
            dossier.Topics ??= new List<DossierTopicRow>();
            dossier.FieldStyles ??= new Dictionary<string, List<DossierTextStyleRange>>();
            dossier.Changes ??= new List<DossierChangeRow>();

            // Auch die Verweislisten. Eine von Hand bearbeitete Datei mit
            // "HoldingIds": null liess die Berechnung des Dossierstands
            // abstuerzen — die Umstellung ist die Stelle, an der so etwas
            // aufgefangen gehoert, nicht jede spaetere Schleife einzeln.
            dossier.HoldingIds ??= new List<Guid>();
            dossier.ShaftNumbers ??= new List<string>();
            dossier.DismissedHoldingIds ??= new List<Guid>();
            dossier.DismissedShaftNumbers ??= new List<string>();
            dossier.HiddenChapters ??= new List<string>();
            dossier.FieldOverrides ??= new Dictionary<string, string>();
            dossier.TextOverrides ??= new Dictionary<string, string>();
            dossier.TocChapterPages ??= new Dictionary<string, string>();
            dossier.TocAttachmentLines ??= new List<string>();
            dossier.TocAttachmentPageNumbers ??= new List<string>();
            dossier.TocAttachments = (dossier.TocAttachments ?? new List<DossierTocAttachment>())
                .Where(punkt => punkt is not null)
                .ToList();

            if (brauchtVerzeichnisEintraege && dossier.TocAttachments.Count == 0)
            {
                for (var index = 0; index < dossier.TocAttachmentLines.Count; index++)
                {
                    dossier.TocAttachments.Add(new DossierTocAttachment
                    {
                        Title = dossier.TocAttachmentLines[index] ?? string.Empty,
                        PageNumber = index < dossier.TocAttachmentPageNumbers.Count
                            ? dossier.TocAttachmentPageNumbers[index] ?? string.Empty
                            : null
                    });
                }
            }

            // Ab Schema 8 existiert nur noch die gemeinsame Objektliste. Die
            // alten parallelen Listen werden nach der einmaligen Übernahme
            // geleert, damit sie nie wieder auseinanderlaufen können.
            dossier.TocAttachmentLines = null;
            dossier.TocAttachmentPageNumbers = null;

            foreach (var punkt in dossier.TocAttachments)
                punkt.Title ??= string.Empty;

            foreach (var topic in dossier.Topics.Where(topic => topic is not null))
                topic.StyleRanges ??= new List<DossierTextStyleRange>();
            foreach (var owner in dossier.Owners.Where(owner => owner is not null))
                owner.FieldStyles ??= new Dictionary<string, List<DossierTextStyleRange>>();
            foreach (var change in dossier.Changes.Where(change => change is not null))
                change.FieldStyles ??= new Dictionary<string, List<DossierTextStyleRange>>();

            if (brauchtThemen && dossier.Topics.Count == 0)
                dossier.Topics.AddRange(BuildDossierTopics(dossier));

            if (!isLegacyDocument)
                continue;

            // Wer schon Zeilen hat, wird nicht angefasst.
            if (dossier.Owners.Count > 0)
                continue;

            var row = BuildRowFromLegacyFields(dossier);
            if (row is not null)
                dossier.Owners.Add(row);
        }

        document.SchemaVersion = DossierDocument.CurrentSchemaVersion;
        return document;
    }

    /// <summary>
    /// Die Gebietsthemen aus den bisherigen Einzelfeldern. Themen ohne Altfeld
    /// entstehen leer — sie sind neu und werden von Hand gefuellt.
    /// </summary>
    private static List<DossierTopicRow> BuildAreaTopics(DossierAreaSettings area)
    {
        var texte = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Ausführungstermin"] = Trim(area.ExecutionDate),
            ["Ansprechpartner"] = Trim(area.ContactPerson),
            ["Unternehmer"] = Trim(area.Contractor),
            ["Örtliche Bauleitung"] = Trim(area.SiteManagement),
            ["Behinderungen, Zugänge, Verkehrsführung, Fussgängerführung"] =
                Trim(area.Obstructions)
        };

        var themen = DefaultTopicTitles
            .Select(titel => new DossierTopicRow
            {
                Title = titel,
                Text = texte.TryGetValue(titel, out var text) && text.Length > 0
                    ? text
                    : DefaultTopicTexts.TryGetValue(titel, out var vorgabe) ? vorgabe : string.Empty
            })
            .ToList();

        // Die zwei Erklaertexte des bisherigen Aufbaus gehen sonst verloren.
        AppendIfSet(themen, "Hausanschluss Abwasser", area.HouseConnectionText);
        AppendIfSet(themen, "Meteorwasser", area.StormWaterText);

        return themen;
    }

    /// <summary>
    /// Die abweichenden Themen eines Dossiers aus seinen bisherigen Feldern.
    /// Nur was wirklich gefuellt war, wird zur Zeile.
    /// </summary>
    private static List<DossierTopicRow> BuildDossierTopics(DossierDefinition dossier)
    {
        var themen = new List<DossierTopicRow>();
        AppendIfSet(themen, "Bauvorgang", dossier.ConstructionProcess);
        AppendIfSet(themen, "Bemerkungen", dossier.Remarks);
        AppendIfSet(themen, "Beilagen", dossier.Attachments);
        return themen;
    }

    private static void AppendIfSet(List<DossierTopicRow> themen, string titel, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        themen.Add(new DossierTopicRow { Title = titel, Text = text.Trim() });
    }

    /// <summary>
    /// Liefert null, wenn in den Altfeldern nichts steht — eine leere Zeile in
    /// der Tabelle waere schlechter als gar keine.
    /// </summary>
    private static DossierOwnerRow? BuildRowFromLegacyFields(DossierDefinition dossier)
    {
        var name = JoinInline(dossier.OwnerName, dossier.OwnerAddress);

        if (!string.IsNullOrWhiteSpace(dossier.ContactName))
        {
            var responsibility = "Zuständigkeit: " + dossier.ContactName.Trim();
            name = name.Length == 0 ? responsibility : name + "\n" + responsibility;
        }

        var row = new DossierOwnerRow
        {
            HouseNumber = Trim(dossier.HouseNumbers),
            ParcelNumber = Trim(dossier.ParcelNumbers),
            Name = name,
            Phone = Trim(dossier.ContactPhone),
            Mail = Trim(dossier.ContactMail),
            Occupancy = Trim(dossier.Occupancy)
        };

        return row.HasContent ? row : null;
    }

    private static string Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string JoinInline(params string?[] parts)
        => string.Join(
            " ",
            parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
}

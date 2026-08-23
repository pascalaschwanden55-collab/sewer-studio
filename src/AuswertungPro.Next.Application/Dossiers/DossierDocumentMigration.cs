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
/// </summary>
public static class DossierDocumentMigration
{
    /// <summary>
    /// Version, ab der die Eigentuemerzeilen im Dokument selbst stehen. Nur
    /// Dateien darunter brauchen die Ableitung aus den Altfeldern.
    /// </summary>
    private const int OwnersStoredFromVersion = 2;

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

        foreach (var dossier in document.Dossiers)
        {
            dossier.Owners ??= new List<DossierOwnerRow>();

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

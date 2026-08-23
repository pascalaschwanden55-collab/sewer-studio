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
    public static DossierDocument MigrateToCurrent(DossierDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Area ??= new DossierAreaSettings();
        document.Dossiers ??= new List<DossierDefinition>();

        // Die Ableitung aus den Altfeldern ist eine EINMALIGE Umstellung von
        // Version 1 auf Version 2. Bei einem bereits aktuellen Dokument darf
        // sie nie erneut laufen: sonst kommt eine von Pascal geloeschte Zeile
        // beim naechsten Laden zurueck, oder ein neu angelegtes Dossier
        // bekommt eine Zeile, die er nie eingegeben hat.
        var isLegacyDocument = document.SchemaVersion < DossierDocument.CurrentSchemaVersion;

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

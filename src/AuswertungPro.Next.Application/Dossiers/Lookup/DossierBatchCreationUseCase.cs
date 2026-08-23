using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>Ein bestaetigter Vorschlag samt der angehakten Leitungen.</summary>
public sealed record DossierCreationSelection(
    DossierProposal Proposal,
    IReadOnlyList<string> SelectedHoldingDesignations);

/// <summary>
/// Macht aus bestaetigten Vorschlaegen Dossiers. Reine Umwandlung: kein
/// Dateizugriff, kein Netz. Der Aufrufer speichert.
/// </summary>
public static class DossierBatchCreationUseCase
{
    public static IReadOnlyList<DossierDefinition> Build(
        IReadOnlyList<DossierCreationSelection> selections,
        IReadOnlyDictionary<string, Guid> holdingIdsByName)
    {
        ArgumentNullException.ThrowIfNull(selections);
        ArgumentNullException.ThrowIfNull(holdingIdsByName);

        var ergebnis = new List<DossierDefinition>();

        foreach (var auswahl in selections)
        {
            var vorschlag = auswahl.Proposal;

            // Ein gesperrter Vorschlag darf nie ein Dossier werden.
            if (!vorschlag.Selectable || vorschlag.Registry is null)
                continue;

            var dossier = new DossierDefinition
            {
                Name = vorschlag.SuggestedName,
                ParcelNumbers = vorschlag.Parcel.Number,
                Municipality = vorschlag.Parcel.Municipality,
                MunicipalityBfsNr = vorschlag.Parcel.BfsNr,
                Address = vorschlag.Registry.BuildingStreet,
                HouseNumbers = vorschlag.Registry.BuildingHouseNumber,
                PostalCode = vorschlag.Registry.PostalCode,
                Town = vorschlag.Registry.Town
            };

            // Das Deckblatt speist sich weiterhin aus diesen Feldern.
            var erster = vorschlag.Registry.Owners.FirstOrDefault();
            if (erster is not null)
            {
                dossier.OwnerName = erster.Name;
                dossier.OwnerAddress = erster.AddressLine;
            }

            foreach (var eigentuemer in vorschlag.Registry.Owners)
            {
                dossier.Owners.Add(new DossierOwnerRow
                {
                    HouseNumber = vorschlag.Registry.BuildingHouseNumber,
                    ParcelNumber = vorschlag.Parcel.Number,
                    Name = eigentuemer.Name,
                    // Telefonnummern werden bewusst nicht ermittelt.
                    Phone = "",
                    Mail = "",
                    Occupancy = ""
                });
            }

            foreach (var bezeichnung in auswahl.SelectedHoldingDesignations)
            {
                if (holdingIdsByName.TryGetValue(bezeichnung, out var id)
                    && !dossier.HoldingIds.Contains(id))
                {
                    dossier.HoldingIds.Add(id);
                }
            }

            ergebnis.Add(dossier);
        }

        return ergebnis;
    }
}

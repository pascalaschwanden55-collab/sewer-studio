using System;
using System.Collections.Generic;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Macht aus einer Parzelle und ihrem Grundbuchauszug die Angaben eines
/// Dossiers.
///
/// Diese Regeln stehen bewusst nur EINMAL: die Stapelanlage und das Anlegen
/// einer einzelnen Liegenschaft muessen dasselbe Ergebnis liefern. Zwei Kopien
/// waeren zwei Wahrheiten, und die zweite faellt erst im Brief an den
/// Eigentuemer auf.
///
/// Reine Umwandlung: kein Dateizugriff, kein Netz.
/// </summary>
public static class DossierFromLandRegistryMapper
{
    /// <summary>
    /// Fuellt die Angaben in ein vorhandenes Dossier. Telefon und Mail bleiben
    /// leer — sie stehen nicht im Grundbuch.
    /// </summary>
    public static void Apply(
        DossierDefinition dossier, ParcelInfo parcel, LandRegistryEntry registry)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(parcel);
        ArgumentNullException.ThrowIfNull(registry);

        dossier.ParcelNumbers = parcel.Number;
        dossier.Municipality = parcel.Municipality;
        dossier.MunicipalityBfsNr = parcel.BfsNr;
        dossier.Address = registry.BuildingStreet;
        dossier.HouseNumbers = registry.BuildingHouseNumber;
        dossier.PostalCode = registry.PostalCode;
        dossier.Town = registry.Town;

        // Das Deckblatt speist sich weiterhin aus diesen Feldern.
        if (registry.Owners.Count > 0)
        {
            dossier.OwnerName = registry.Owners[0].Name;
            dossier.OwnerAddress = registry.Owners[0].AddressLine;
        }

        dossier.Owners = new List<DossierOwnerRow>();
        foreach (var eigentuemer in registry.Owners)
        {
            dossier.Owners.Add(new DossierOwnerRow
            {
                HouseNumber = registry.BuildingHouseNumber,
                ParcelNumber = parcel.Number,
                Name = eigentuemer.Name,
                Phone = "",
                Mail = "",
                Occupancy = ""
            });
        }
    }

    /// <summary>Ein neues Dossier mit Namen und allen Angaben der Parzelle.</summary>
    public static DossierDefinition Build(ParcelInfo parcel, LandRegistryEntry registry)
    {
        ArgumentNullException.ThrowIfNull(parcel);
        ArgumentNullException.ThrowIfNull(registry);

        var dossier = new DossierDefinition
        {
            Name = DossierNameBuilder.Build(
                parcel.Number,
                registry.Owners.Count > 0 ? registry.Owners[0].Name : null)
        };

        Apply(dossier, parcel, registry);
        return dossier;
    }
}

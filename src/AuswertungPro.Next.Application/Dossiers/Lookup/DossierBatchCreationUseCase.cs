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
    /// <param name="projectShaftNumbers">
    /// Die Schaechte des Hauptprojekts. Pflicht und nicht optional: ein
    /// vergessener Wert wuerde hier still zu Dossiers ohne Schaechte fuehren,
    /// und niemand sieht einer leeren Liste an, ob es keine gibt oder ob nur
    /// niemand gesucht hat.
    /// </param>
    public static IReadOnlyList<DossierDefinition> Build(
        IReadOnlyList<DossierCreationSelection> selections,
        IReadOnlyDictionary<string, Guid> holdingIdsByName,
        IReadOnlyList<string> projectShaftNumbers)
    {
        ArgumentNullException.ThrowIfNull(selections);
        ArgumentNullException.ThrowIfNull(holdingIdsByName);
        ArgumentNullException.ThrowIfNull(projectShaftNumbers);

        var ergebnis = new List<DossierDefinition>();

        foreach (var auswahl in selections)
        {
            var vorschlag = auswahl.Proposal;

            // Ein gesperrter Vorschlag darf nie ein Dossier werden.
            if (!vorschlag.Selectable || vorschlag.Registry is null)
                continue;

            // Die Vorbelegung steht nur einmal — dieselbe Regel gilt beim
            // Anlegen einer einzelnen Liegenschaft.
            var dossier = DossierFromLandRegistryMapper.Build(
                vorschlag.Parcel, vorschlag.Registry);

            dossier.Name = vorschlag.SuggestedName;

            foreach (var bezeichnung in auswahl.SelectedHoldingDesignations)
            {
                if (holdingIdsByName.TryGetValue(bezeichnung, out var id)
                    && !dossier.HoldingIds.Contains(id))
                {
                    dossier.HoldingIds.Add(id);
                }
            }

            // Die Schaechte kommen aus denselben zwei Wegen wie bei einer
            // einzeln angelegten Liegenschaft. Ohne diesen Schritt blieben
            // Stapel-Dossiers ganz ohne Schacht.
            dossier.ShaftNumbers = ParcelHoldingAndShaftMatcher.ShaftsForParcel(
                    auswahl.SelectedHoldingDesignations,
                    projectShaftNumbers,
                    vorschlag.Parcel.Number)
                .ToList();

            ergebnis.Add(dossier);
        }

        return ergebnis;
    }
}

using System;
using System.Collections.Generic;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>Was der Benutzer aus der Parzellenabfrage uebernommen hat.</summary>
public sealed record DossierParcelLookupChoice(
    DossierDefinition Dossier,
    IReadOnlyList<string> SelectedHoldingDesignations,
    IReadOnlyList<string> ShaftNumbers);

/// <summary>Was der Mensch beim Nachfuehren angehakt hat.</summary>
public sealed record DossierRefreshChoice(
    IReadOnlyList<RefreshableHolding> Holdings,
    IReadOnlyList<string> Shafts);

/// <summary>
/// Die Fenster des Dossier-Bereichs — als Vertrag statt als acht feste
/// Aufrufe.
///
/// Vorher rief das Cockpit die Fensterklassen unmittelbar auf. Das machte
/// seinen ganzen Ablauf unpruefbar: Anlegen, Nachfuehren, Auswahl und
/// Ruecksetzen liessen sich ohne echte Fenster nicht durchspielen, und die
/// Pruefungen kamen nicht weiter als bis zu den Textbausteinen.
///
/// Jede Methode liefert null oder false, wenn abgebrochen wurde.
/// </summary>
public interface IDossierDialogs
{
    /// <summary>Gemeinde und Parzelle abfragen und daraus vorfuellen.</summary>
    DossierParcelLookupChoice? NewProperty(
        IReadOnlyDictionary<string, Guid> holdingIdsByName,
        IReadOnlyList<string> projectShaftNumbers);

    /// <summary>Stammdaten einer Liegenschaft bearbeiten.</summary>
    bool EditDossier(DossierDefinition definition, bool isNew);

    /// <summary>Gebietsangaben bearbeiten.</summary>
    bool EditArea(DossierAreaSettings area);

    /// <summary>Dossiers fuer die Parzellen des Projekts auf einmal anlegen.</summary>
    IReadOnlyList<DossierDefinition> CreateFromProject(
        IReadOnlyList<string> projectHoldingNames,
        IReadOnlyDictionary<string, Guid> holdingIdsByName,
        IReadOnlyList<string> projectShaftNumbers,
        IReadOnlyList<string> parcelsWithDossier);

    /// <summary>Die Leitungen der Liegenschaft waehlen.</summary>
    List<Guid>? PickHoldings(Project project, IReadOnlyCollection<Guid> chosen);

    /// <summary>Die Schaechte der Liegenschaft waehlen.</summary>
    List<string>? PickShafts(Project project, IReadOnlyCollection<string> chosen);

    /// <summary>Seite fuer Seite ansehen und ausfuellen.</summary>
    (DossierAreaSettings Area, DossierDefinition Dossier)? Preview(
        DossierExportRequest request, string templatePath);

    /// <summary>Zeigen, was das Dossier ergaenzen wuerde.</summary>
    DossierRefreshChoice? Refresh(string dossierName, DossierRefreshProposal proposal);
}

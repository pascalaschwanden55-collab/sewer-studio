using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Eine vorgeschlagene Leitung. <paramref name="Origin"/> sagt, welcher Weg sie
/// gefunden hat — "Lage" oder "Name" — damit in der Liste sichtbar ist, worauf
/// der Vorschlag beruht.
/// </summary>
public sealed record ProposedHolding(
    string Designation,
    bool IsPrivate,
    bool InProject,
    bool Preselected,
    string Origin);

/// <summary>
/// Der Vorschlag fuer ein Dossier. <paramref name="Selectable"/> ist falsch,
/// wenn daraus kein Dossier entstehen darf; <paramref name="SkipReason"/> sagt
/// dann warum.
/// </summary>
public sealed record DossierProposal(
    ParcelInfo Parcel,
    LandRegistryEntry? Registry,
    IReadOnlyList<ProposedHolding> Holdings,
    string SuggestedName,
    bool Selectable,
    string SkipReason);

/// <summary>Das Ergebnis eines Durchlaufs samt sichtbarer Warnungen.</summary>
public sealed record DossierBatchProposalResult(
    IReadOnlyList<DossierProposal> Proposals,
    IReadOnlyList<string> Warnings);

/// <summary>Was der Durchlauf braucht. Reine Eingabe, kein Projektobjekt.</summary>
public sealed record DossierBatchProposalRequest(
    int BfsNr,
    IReadOnlyList<string> ProjectHoldingNames,
    IReadOnlyList<string> ParcelNumbersWithExistingDossier);

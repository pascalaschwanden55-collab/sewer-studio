using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>Eine Leitung, die das Nachfuehren neu anbietet.</summary>
public sealed record RefreshableHolding(string Designation, Guid Id);

/// <summary>
/// Was seit dem letzten Mal dazugekommen ist. Leer heisst: nichts Neues —
/// nicht etwa, dass nichts gefunden wurde.
/// </summary>
public sealed record DossierRefreshProposal(
    IReadOnlyList<RefreshableHolding> NewHoldings,
    IReadOnlyList<string> NewShafts)
{
    public bool HasAnything => NewHoldings.Count > 0 || NewShafts.Count > 0;
}

/// <summary>
/// Fuehrt ein bestehendes Dossier nach, wenn das Projekt spaeter mehr weiss —
/// etwa weil die Schaechte einer Liegenschaft erst nach dem Anlegen erfasst
/// wurden.
///
/// Die Regel ist bewusst eng: ERGAENZEN, nie ersetzen. Was im Dossier steht,
/// bleibt stehen; was einmal abgelehnt wurde, wird nicht erneut angeboten.
/// Texte, Themen, Eigentuemer und Plan fasst dieser Weg gar nicht an.
///
/// Verglichen wird gegen das PROJEKT, nicht gegen den Kanton: gefragt ist,
/// was inzwischen aufgenommen wurde. Deshalb kostet das Nachfuehren keine
/// Abfrage und funktioniert auch ohne Netz.
///
/// Reine Logik: kein Netz, kein Dateizugriff.
/// </summary>
public static class DossierRefreshUseCase
{
    /// <summary>
    /// Was das Dossier ergaenzen wuerde. Aendert nichts — der Mensch
    /// entscheidet danach im Fenster.
    /// </summary>
    public static DossierRefreshProposal Propose(
        DossierDefinition dossier,
        IReadOnlyDictionary<string, Guid> projectHoldingIdsByName,
        IReadOnlyList<string> projectShaftNumbers)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(projectHoldingIdsByName);
        ArgumentNullException.ThrowIfNull(projectShaftNumbers);

        var parzellen = ParcelNumbers(dossier);
        if (parzellen.Count == 0)
            return new DossierRefreshProposal(Array.Empty<RefreshableHolding>(), Array.Empty<string>());

        var vorhandeneLeitungen = new HashSet<Guid>(dossier.HoldingIds ?? new List<Guid>());
        var abgelehnteLeitungen = new HashSet<Guid>(dossier.DismissedHoldingIds ?? new List<Guid>());

        var neueLeitungen = new List<RefreshableHolding>();
        var gesehen = new HashSet<Guid>();

        foreach (var parzelle in parzellen)
        {
            foreach (var name in ParcelHoldingAndShaftMatcher.HoldingsByName(
                         projectHoldingIdsByName.Keys.ToList(), parzelle))
            {
                if (!projectHoldingIdsByName.TryGetValue(name, out var id))
                    continue;

                if (vorhandeneLeitungen.Contains(id) || abgelehnteLeitungen.Contains(id))
                    continue;

                if (gesehen.Add(id))
                    neueLeitungen.Add(new RefreshableHolding(name, id));
            }
        }

        // Die Schaechte richten sich nach dem Stand NACH der Ergaenzung: eine
        // neu gefundene Leitung bringt ihre Knoten mit. Sonst muesste man
        // zweimal nachfuehren, um beides zu bekommen.
        var alleLeitungsnamen = NamesOf(dossier, projectHoldingIdsByName)
            .Concat(neueLeitungen.Select(h => h.Designation))
            .ToList();

        var vorhandeneSchaechte = new HashSet<string>(
            dossier.ShaftNumbers ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var abgelehnteSchaechte = new HashSet<string>(
            dossier.DismissedShaftNumbers ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

        var neueSchaechte = new List<string>();

        foreach (var parzelle in parzellen)
        {
            foreach (var schacht in ParcelHoldingAndShaftMatcher.ShaftsForParcel(
                         alleLeitungsnamen, projectShaftNumbers, parzelle))
            {
                if (vorhandeneSchaechte.Contains(schacht) || abgelehnteSchaechte.Contains(schacht))
                    continue;

                if (!neueSchaechte.Contains(schacht, StringComparer.OrdinalIgnoreCase))
                    neueSchaechte.Add(schacht);
            }
        }

        return new DossierRefreshProposal(neueLeitungen, neueSchaechte);
    }

    /// <summary>
    /// Uebernimmt die angehakten Vorschlaege und merkt sich die abgelehnten.
    ///
    /// Entfernt wird nichts — auch dann nicht, wenn eine Leitung inzwischen
    /// aus dem Projekt verschwunden ist. Ein Dossier, das der Empfaenger schon
    /// hat, soll sich nicht hinter seinem Ruecken leeren.
    /// </summary>
    public static void Apply(
        DossierDefinition dossier,
        IReadOnlyList<RefreshableHolding> acceptedHoldings,
        IReadOnlyList<string> acceptedShafts,
        DossierRefreshProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(acceptedHoldings);
        ArgumentNullException.ThrowIfNull(acceptedShafts);
        ArgumentNullException.ThrowIfNull(proposal);

        dossier.HoldingIds ??= new List<Guid>();
        dossier.ShaftNumbers ??= new List<string>();
        dossier.DismissedHoldingIds ??= new List<Guid>();
        dossier.DismissedShaftNumbers ??= new List<string>();

        var angenommeneLeitungen = new HashSet<Guid>(acceptedHoldings.Select(h => h.Id));
        var angenommeneSchaechte = new HashSet<string>(
            acceptedShafts, StringComparer.OrdinalIgnoreCase);

        foreach (var leitung in proposal.NewHoldings)
        {
            if (angenommeneLeitungen.Contains(leitung.Id))
            {
                if (!dossier.HoldingIds.Contains(leitung.Id))
                    dossier.HoldingIds.Add(leitung.Id);
            }
            else if (!dossier.DismissedHoldingIds.Contains(leitung.Id))
            {
                dossier.DismissedHoldingIds.Add(leitung.Id);
            }
        }

        foreach (var schacht in proposal.NewShafts)
        {
            if (angenommeneSchaechte.Contains(schacht))
            {
                if (!dossier.ShaftNumbers.Contains(schacht, StringComparer.OrdinalIgnoreCase))
                    dossier.ShaftNumbers.Add(schacht);
            }
            else if (!dossier.DismissedShaftNumbers.Contains(schacht, StringComparer.OrdinalIgnoreCase))
            {
                dossier.DismissedShaftNumbers.Add(schacht);
            }
        }
    }

    /// <summary>
    /// Die Parzellennummern des Dossiers. Mehrere Nummern stehen im selben
    /// Feld, getrennt wie sie der Mensch geschrieben hat.
    /// </summary>
    internal static IReadOnlyList<string> ParcelNumbers(DossierDefinition dossier)
        => (dossier.ParcelNumbers ?? string.Empty)
            .Split(new[] { ',', ';', '+', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Die Namen der bereits zugeordneten Leitungen.</summary>
    private static IReadOnlyList<string> NamesOf(
        DossierDefinition dossier, IReadOnlyDictionary<string, Guid> idsByName)
    {
        var vorhanden = new HashSet<Guid>(dossier.HoldingIds ?? new List<Guid>());

        return idsByName
            .Where(paar => vorhanden.Contains(paar.Value))
            .Select(paar => paar.Key)
            .ToList();
    }
}

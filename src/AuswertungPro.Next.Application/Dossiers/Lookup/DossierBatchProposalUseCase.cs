using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Stellt die Dossier-Vorschlaege eines Projekts zusammen.
///
/// Zwei Wege fuehren zu den Parzellen, und beide werden gebraucht:
///   Name  — Knoten der Form "&lt;Parzelle&gt;.&lt;lfd&gt;" nennen ihre Parzelle. Kostet
///           nichts und findet die privaten Hausanschluesse, die der Kanton in
///           seiner oeffentlichen Netzebene groesstenteils nicht fuehrt.
///   Lage  — die Linien der beim Kanton bekannten Haltungen gegen die
///           Parzellenumrisse. Findet zusaetzlich Parzellen ohne solche Knoten.
///
/// Diese Klasse rechnet nur. Sie kennt kein Dateisystem und kein HTTP; die drei
/// Leser sind Abhaengigkeiten und im Test erfunden.
/// </summary>
public sealed class DossierBatchProposalUseCase
{
    private readonly IParcelLookup _parcels;
    private readonly ILandRegistryLookup _registry;
    private readonly ISewerNetworkLookup _network;

    public DossierBatchProposalUseCase(
        IParcelLookup parcels,
        ILandRegistryLookup registry,
        ISewerNetworkLookup network)
    {
        _parcels = parcels ?? throw new ArgumentNullException(nameof(parcels));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _network = network ?? throw new ArgumentNullException(nameof(network));
    }

    public async Task<DossierBatchProposalResult> RunAsync(
        DossierBatchProposalRequest request,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var warnungen = new List<string>();
        var imProjekt = new HashSet<string>(
            request.ProjectHoldingNames.Where(n => !string.IsNullOrWhiteSpace(n)),
            StringComparer.OrdinalIgnoreCase);

        var parzellen = await SammleParzellen(request, warnungen, progress, ct)
            .ConfigureAwait(false);

        var mitDossier = new HashSet<string>(
            request.ParcelNumbersWithExistingDossier, StringComparer.OrdinalIgnoreCase);

        var vorschlaege = new List<DossierProposal>();

        foreach (var parzelle in parzellen)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Parzelle {parzelle.Number}: Eigentümer und Leitungen");

            var aufParzelle = await SicherLesen<IReadOnlyList<NetworkHolding>>(
                async () => await _network.FindOnParcelAsync(parzelle, ct).ConfigureAwait(false),
                $"Leitungen auf Parzelle {parzelle.Number}", warnungen).ConfigureAwait(false)
                ?? Array.Empty<NetworkHolding>();

            var eintrag = await SicherLesen<LandRegistryEntry>(
                () => _registry.ReadAsync(parzelle, ct),
                $"Grundbuchauskunft zu Parzelle {parzelle.Number}", warnungen)
                .ConfigureAwait(false);

            var leitungen = BaueLeitungen(parzelle, aufParzelle, imProjekt, request);
            var (waehlbar, grund) = Beurteile(parzelle, eintrag, mitDossier);

            vorschlaege.Add(new DossierProposal(
                parzelle,
                eintrag,
                leitungen,
                DossierNameBuilder.Build(parzelle.Number, eintrag?.Owners.FirstOrDefault()?.Name),
                waehlbar,
                grund));
        }

        return new DossierBatchProposalResult(vorschlaege, warnungen);
    }

    /// <summary>
    /// Beide Wege, zusammengefuehrt und entdoppelt. Eine aus einem Namen
    /// abgeleitete Nummer zaehlt erst, wenn der Parzellendienst sie bestaetigt.
    /// </summary>
    private async Task<List<ParcelInfo>> SammleParzellen(
        DossierBatchProposalRequest request,
        List<string> warnungen,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var gefunden = new List<ParcelInfo>();

        progress?.Report("Lage der Haltungen beim Kanton abfragen");
        var haltungen = await SicherLesen<IReadOnlyList<NetworkHolding>>(
            async () => await _network.FindByNamesAsync(request.ProjectHoldingNames, ct).ConfigureAwait(false),
            "Lage der Haltungen", warnungen).ConfigureAwait(false)
            ?? Array.Empty<NetworkHolding>();

        var linien = haltungen
            .Select(h => h.GeometryWkt)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToList();

        // Der Aufruf laeuft auch ohne Linien: der Leser kehrt bei leerer Liste
        // sofort und ohne Abfrage zurueck. Ein Waechter hier waere doppelt
        // gemoppelt und wuerde die Absicht nur verschleiern.
        progress?.Report("Parzellen unter den Leitungen suchen");
        var beruehrt = await SicherLesen<IReadOnlyList<ParcelInfo>>(
            async () => await _parcels.FindTouchedAsync(linien, ct).ConfigureAwait(false),
            "Parzellensuche", warnungen).ConfigureAwait(false)
            ?? Array.Empty<ParcelInfo>();

        // Parzellennummern sind je Gemeinde vergeben. Eine Leitung an der
        // Gemeindegrenze liefert sonst eine fremde Parzelle mit derselben
        // Nummer — und damit den falschen Eigentuemer.
        gefunden.AddRange(beruehrt.Where(p => p.BfsNr == request.BfsNr));

        foreach (var nummer in ParcelNumberFromHoldingName.ExtractAll(request.ProjectHoldingNames))
        {
            ct.ThrowIfCancellationRequested();

            if (gefunden.Any(p => p.Number.Equals(nummer, StringComparison.OrdinalIgnoreCase)))
                continue;

            var bestaetigt = await SicherLesen<ParcelInfo>(
                () => _parcels.FindAsync(request.BfsNr, nummer, ct),
                $"Parzelle {nummer}", warnungen).ConfigureAwait(false);

            // Nicht bestaetigt heisst: verwerfen, nicht zeigen.
            if (bestaetigt is not null)
                gefunden.Add(bestaetigt);
        }

        return gefunden
            .GroupBy(p => (p.BfsNr, p.Number))
            .Select(g => g.First())
            .OrderBy(p => p.Number.Length)
            .ThenBy(p => p.Number, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<ProposedHolding> BaueLeitungen(
        ParcelInfo parzelle,
        IReadOnlyList<NetworkHolding> aufParzelle,
        HashSet<string> imProjekt,
        DossierBatchProposalRequest request)
    {
        var ergebnis = new List<ProposedHolding>();

        foreach (var haltung in aufParzelle)
        {
            var inProjekt = imProjekt.Contains(haltung.Designation);
            ergebnis.Add(new ProposedHolding(
                haltung.Designation,
                haltung.IsPrivate,
                inProjekt,
                Preselected: haltung.IsPrivate && inProjekt,
                Origin: "Lage"));
        }

        // Was der Kanton nicht fuehrt, verraet der Knotenname: diese Haltungen
        // sind Hausanschluesse der Parzelle und liegen im Projekt.
        foreach (var name in request.ProjectHoldingNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (!ParcelNumberFromHoldingName.Extract(name)
                    .Any(n => n.Equals(parzelle.Number, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (ergebnis.Any(h => h.Designation.Equals(name, StringComparison.OrdinalIgnoreCase)))
                continue;

            ergebnis.Add(new ProposedHolding(
                name, IsPrivate: true, InProject: true, Preselected: true, Origin: "Name"));
        }

        return ergebnis;
    }

    private static (bool Waehlbar, string Grund) Beurteile(
        ParcelInfo parzelle, LandRegistryEntry? eintrag, HashSet<string> mitDossier)
    {
        if (mitDossier.Contains(parzelle.Number))
            return (false, "Für diese Parzelle gibt es bereits ein Dossier.");

        if (eintrag is null)
            return (false, "Die Grundbuchauskunft konnte nicht gelesen werden.");

        if (eintrag.NoOwnerRegistered || eintrag.Owners.Count == 0)
            return (false, "Im Grundbuch ist kein Eigentümer eingetragen.");

        return (true, string.Empty);
    }

    /// <summary>
    /// Ein Dienstfehler bei einer Parzelle darf den ganzen Lauf nicht beenden.
    /// Ein Abbruch durch den Benutzer dagegen schon — der wird durchgereicht.
    /// </summary>
    private static async Task<T?> SicherLesen<T>(
        Func<Task<T?>> leser, string was, List<string> warnungen) where T : class
    {
        try
        {
            return await leser().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            warnungen.Add($"{was}: {ex.Message}");
            return null;
        }
    }
}

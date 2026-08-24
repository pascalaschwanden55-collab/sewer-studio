using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>Was zu einer einzelnen Parzelle gefunden wurde.</summary>
public sealed record DossierParcelLookupResult(
    DossierDefinition? Dossier,
    ParcelInfo? Parcel,
    IReadOnlyList<ProposedHolding> Holdings,
    IReadOnlyList<string> Warnings)
{
    public bool Found => Dossier is not null;
}

/// <summary>
/// Holt zu Gemeinde und Parzellennummer alles, was die oeffentlichen Dienste
/// des Kantons hergeben: die Parzelle, den Grundbuchauszug mit Adresse und
/// Eigentuemern und die Leitungen, die auf der Parzelle liegen.
///
/// Ein Dienstfehler bleibt ein Fehler und wird gemeldet — er darf nie als
/// "nichts gefunden" durchgehen. Sonst entstuende ein Dossier ohne die Haelfte
/// seiner Angaben, ohne dass es jemandem auffaellt.
/// </summary>
public sealed class DossierParcelLookupUseCase
{
    private readonly IParcelLookup _parcels;
    private readonly ILandRegistryLookup _registry;
    private readonly ISewerNetworkLookup _network;

    public DossierParcelLookupUseCase(
        IParcelLookup parcels,
        ILandRegistryLookup registry,
        ISewerNetworkLookup network)
    {
        _parcels = parcels ?? throw new ArgumentNullException(nameof(parcels));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _network = network ?? throw new ArgumentNullException(nameof(network));
    }

    /// <summary>
    /// <paramref name="projectHoldingNames"/> sind die Leitungen des Projekts.
    /// Sie werden gebraucht, weil der Kanton die privaten Hausanschluesse
    /// grosstenteils nicht fuehrt — deren Knotenname nennt aber die Parzelle.
    /// </summary>
    public async Task<DossierParcelLookupResult> RunAsync(
        int bfsNr,
        string parcelNumber,
        IReadOnlyList<string>? projectHoldingNames = null,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var warnungen = new List<string>();
        var nummer = (parcelNumber ?? string.Empty).Trim();

        if (nummer.Length == 0)
        {
            warnungen.Add("Es wurde keine Parzellennummer angegeben.");
            return Leer(warnungen);
        }

        progress?.Report($"Parzelle {nummer} suchen");

        ParcelInfo? parzelle;
        try
        {
            parzelle = await _parcels.FindAsync(bfsNr, nummer, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            warnungen.Add("Die Parzelle konnte nicht abgefragt werden: " + ex.Message);
            return Leer(warnungen);
        }

        if (parzelle is null)
        {
            warnungen.Add($"In dieser Gemeinde gibt es keine Parzelle {nummer}.");
            return Leer(warnungen);
        }

        progress?.Report("Eigentümer aus dem Grundbuch lesen");

        LandRegistryEntry? eintrag;
        try
        {
            eintrag = await _registry.ReadAsync(parzelle, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            warnungen.Add("Die Grundbuchauskunft konnte nicht gelesen werden: " + ex.Message);
            eintrag = null;
        }

        if (eintrag is null)
        {
            // Ohne Auszug bleibt immerhin die Parzelle selbst.
            var nurParzelle = new DossierDefinition
            {
                Name = DossierNameBuilder.Build(parzelle.Number, null),
                ParcelNumbers = parzelle.Number,
                Municipality = parzelle.Municipality,
                MunicipalityBfsNr = parzelle.BfsNr
            };

            // Ohne Auszug gibt es keine Eigentuemer, die Leitungen des
            // Projekts lassen sich aber trotzdem ueber ihren Namen zuordnen.
            return new DossierParcelLookupResult(
                nurParzelle,
                parzelle,
                BaueLeitungen(Array.Empty<NetworkHolding>(), projectHoldingNames, parzelle.Number),
                warnungen);
        }

        if (eintrag.NoOwnerRegistered)
            warnungen.Add("Im Grundbuch ist für diese Parzelle kein Eigentümer eingetragen.");
        else if (eintrag.Owners.Count == 0)
            warnungen.Add("Der Grundbuchauszug nennt keine Eigentümer.");

        var dossier = DossierFromLandRegistryMapper.Build(parzelle, eintrag);

        progress?.Report("Leitungen auf der Parzelle suchen");

        IReadOnlyList<NetworkHolding> nachLage = Array.Empty<NetworkHolding>();
        try
        {
            nachLage = await _network.FindOnParcelAsync(parzelle, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            warnungen.Add("Die Leitungen konnten nicht abgefragt werden: " + ex.Message);
        }

        return new DossierParcelLookupResult(
            dossier,
            parzelle,
            BaueLeitungen(nachLage, projectHoldingNames, parzelle.Number),
            warnungen);
    }

    /// <summary>
    /// Die Leitungen aus beiden Wegen in einer Liste: zuerst die vom Kanton
    /// bekannten, danach die, die nur ihr Name der Parzelle zuordnet.
    /// Angehakt wird nur, was das Projekt wirklich fuehrt.
    /// </summary>
    private static IReadOnlyList<ProposedHolding> BaueLeitungen(
        IReadOnlyList<NetworkHolding> nachLage,
        IReadOnlyList<string>? projectHoldingNames,
        string parzellenNummer)
    {
        var imProjekt = new HashSet<string>(
            (projectHoldingNames ?? Array.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var ergebnis = new List<ProposedHolding>();
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var leitung in nachLage)
        {
            if (!gesehen.Add(leitung.Designation))
                continue;

            var bekannt = imProjekt.Contains(leitung.Designation);

            ergebnis.Add(new ProposedHolding(
                leitung.Designation,
                leitung.IsPrivate,
                bekannt,
                Preselected: bekannt && leitung.IsPrivate,
                Origin: "Lage"));
        }

        foreach (var name in ParcelHoldingAndShaftMatcher.HoldingsByName(
                     projectHoldingNames, parzellenNummer))
        {
            if (!gesehen.Add(name))
                continue;

            // Ueber den Namen gefunden heisst: privater Hausanschluss. Der
            // Kanton fuehrt diese Leitungen nicht, deshalb steht hier keine
            // Eigentumsangabe zur Verfuegung — angenommen wird privat.
            ergebnis.Add(new ProposedHolding(
                name,
                IsPrivate: true,
                InProject: true,
                Preselected: true,
                Origin: "Name"));
        }

        return ergebnis;
    }

    private static DossierParcelLookupResult Leer(IReadOnlyList<string> warnungen)
        => new(null, null, Array.Empty<ProposedHolding>(), warnungen);
}

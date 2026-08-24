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
    IReadOnlyList<NetworkHolding> Holdings,
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

    public async Task<DossierParcelLookupResult> RunAsync(
        int bfsNr,
        string parcelNumber,
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

            return new DossierParcelLookupResult(
                nurParzelle, parzelle, Array.Empty<NetworkHolding>(), warnungen);
        }

        if (eintrag.NoOwnerRegistered)
            warnungen.Add("Im Grundbuch ist für diese Parzelle kein Eigentümer eingetragen.");
        else if (eintrag.Owners.Count == 0)
            warnungen.Add("Der Grundbuchauszug nennt keine Eigentümer.");

        var dossier = DossierFromLandRegistryMapper.Build(parzelle, eintrag);

        progress?.Report("Leitungen auf der Parzelle suchen");

        IReadOnlyList<NetworkHolding> leitungen = Array.Empty<NetworkHolding>();
        try
        {
            leitungen = await _network.FindOnParcelAsync(parzelle, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            warnungen.Add("Die Leitungen konnten nicht abgefragt werden: " + ex.Message);
        }

        return new DossierParcelLookupResult(dossier, parzelle, leitungen, warnungen);
    }

    private static DossierParcelLookupResult Leer(IReadOnlyList<string> warnungen)
        => new(null, null, Array.Empty<NetworkHolding>(), warnungen);
}

using System;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Ergaenzt Telefon und Mail der Eigentuemer einer Liegenschaft aus dem
/// Verzeichnis.
///
/// Die Regeln stehen hier und nicht im Fenster, weil sie eine GRENZE sind und
/// keine Bequemlichkeit: Die Bedingungen von search.ch untersagen maschinelle
/// Massenabfragen ausdruecklich (siehe <see cref="IDirectoryLookup"/>). Im
/// Fenstercode war das Kontingent von keiner einzigen Pruefung gedeckt.
///
/// Zwei Regeln:
/// <list type="bullet">
/// <item>Hoechstens <see cref="MaxQueriesPerProperty"/> Abfragen je
/// Liegenschaft — eine Handvoll je angelegtem Dossier, nie ein Stapellauf.</item>
/// <item>Nur ein EINDEUTIGER Treffer wird uebernommen. Eine geratene Nummer im
/// Brief an den Eigentuemer ist schlimmer als eine leere Zelle.</item>
/// </list>
/// </summary>
public sealed class OwnerDirectoryLookupUseCase
{
    /// <summary>
    /// Hoechstzahl der Abfragen je Liegenschaft. Ein Stockwerkeigentum hat
    /// selten mehr Parteien; wer mehr braucht, traegt sie von Hand ein.
    /// </summary>
    public const int MaxQueriesPerProperty = 5;

    private readonly IDirectoryLookup _directory;

    public OwnerDirectoryLookupUseCase(IDirectoryLookup directory)
        => _directory = directory ?? throw new ArgumentNullException(nameof(directory));

    /// <summary>
    /// Fuellt leere Telefon- und Mailfelder. Liefert die Zahl der Zeilen, bei
    /// denen etwas uebernommen wurde.
    ///
    /// Ein bereits erfasster Wert bleibt stehen: von Hand eingetragen wiegt
    /// schwerer als ein Verzeichniseintrag.
    /// </summary>
    public async Task<int> FillAsync(
        DossierDefinition dossier,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        ct.ThrowIfCancellationRequested();

        if (!_directory.IsConfigured)
            return 0;

        var gefragt = 0;
        var uebernommen = 0;

        foreach (var eigentuemer in dossier.Owners)
        {
            if (gefragt >= MaxQueriesPerProperty)
                break;

            if (eigentuemer is null || string.IsNullOrWhiteSpace(eigentuemer.Name))
                continue;

            ct.ThrowIfCancellationRequested();
            gefragt++;

            DirectoryLookupResult treffer;
            try
            {
                treffer = await _directory
                    .FindAsync(eigentuemer.Name, dossier.Town ?? string.Empty, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Ein Ausfall bei einer Person darf die uebrigen nicht kosten.
                continue;
            }

            if (treffer.Unique is not { } eintrag)
                continue;

            var etwasGesetzt = false;

            if (string.IsNullOrWhiteSpace(eigentuemer.Phone)
                && !string.IsNullOrWhiteSpace(eintrag.Phone))
            {
                eigentuemer.Phone = eintrag.Phone;
                etwasGesetzt = true;
            }

            if (string.IsNullOrWhiteSpace(eigentuemer.Mail)
                && !string.IsNullOrWhiteSpace(eintrag.Mail))
            {
                eigentuemer.Mail = eintrag.Mail;
                etwasGesetzt = true;
            }

            if (etwasGesetzt)
                uebernommen++;
        }

        return uebernommen;
    }
}

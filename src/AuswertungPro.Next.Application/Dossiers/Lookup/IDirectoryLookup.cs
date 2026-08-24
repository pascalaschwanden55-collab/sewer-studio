using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>Ein Eintrag aus dem Telefonverzeichnis.</summary>
public sealed record DirectoryEntry(
    string Name,
    string Address,
    string PostalCode,
    string Town,
    string Phone,
    string Mail);

/// <summary>Was eine Verzeichnisabfrage ergeben hat.</summary>
public sealed record DirectoryLookupResult(
    IReadOnlyList<DirectoryEntry> Entries,
    string? Unavailable = null)
{
    /// <summary>
    /// Genau ein Treffer. Bei mehreren bleibt das Feld leer: eine geratene
    /// Telefonnummer im Brief an den Eigentuemer ist schlimmer als keine.
    /// </summary>
    public DirectoryEntry? Unique => Entries.Count == 1 ? Entries[0] : null;

    public bool IsUnavailable => Unavailable is not null;
}

/// <summary>
/// Sucht Telefonnummer und Mailadresse zu einem Namen an einem Ort.
///
/// WICHTIG — die Nutzungsbedingungen von search.ch verbieten maschinelle
/// MASSENabfragen ausdruecklich, insbesondere zum Aufbau oder zur
/// Aktualisierung von Adressdatenbanken. Erlaubt ist die einzelne, von einem
/// Menschen ausgeloeste Abfrage ueber die offizielle Schnittstelle mit
/// eigenem Schluessel und mit der Quellenangabe "Swisscom Directories AG".
///
/// Deshalb: Dieser Vertrag wird nur beim Anlegen EINER Liegenschaft verwendet,
/// nie in der Stapelanlage. Die Webseite wird nie ausgelesen.
/// </summary>
public interface IDirectoryLookup
{
    /// <summary>Wahr, wenn ein Schluessel hinterlegt ist.</summary>
    bool IsConfigured { get; }

    /// <summary>Quellenangabe, die neben einem uebernommenen Wert stehen muss.</summary>
    string Attribution { get; }

    Task<DirectoryLookupResult> FindAsync(
        string name, string town, CancellationToken ct = default);
}

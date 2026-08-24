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
/// WICHTIG — geprueft am 2026-08-24 an den Bedingungen von search.ch:
///   - "Maschinelle Massenabfragen, beispielsweise zur Erstellung oder
///     Aktualisierung von Adressdatenbanken" sind ausdruecklich untersagt.
///   - Ebenso "jede Form der Weitergabe der uebermittelten und/oder
///     abgespeicherten Eintraege an Dritte".
///   - "Das monatliche Nutzungskontingent pro Kunde und API-Key umfasst 1000
///     Abfragen. Pro Abfrage werden maximal 20 Resultate ausgeliefert."
///   - "Bei allen Formen der API-Nutzung muss folgende Quellenangabe gemacht
///     werden: «Swisscom Directories AG»".
///
/// local.ch ist kein zweiter Weg: beide Dienste gehoeren derselben Stelle
/// (Swisscom Directories AG / localsearch) und teilen Daten, Schluessel und
/// Bedingungen. Ein eigenes Entwicklerportal unter api.local.ch gibt es nicht
/// mehr.
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

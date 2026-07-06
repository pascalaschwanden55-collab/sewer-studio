using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Verwaltet den sichtbaren Protokoll-Ausschnitt der aktuell gewaehlten Haltung.
/// Der Controller kapselt die Collection und den Reentrancy-Schutz fuer den
/// automatischen VSA-Finding-zu-Protokoll-Sync.
/// </summary>
public sealed class DataPageSelectedProtocolController
{
    private bool _isSyncing;

    public ObservableCollection<ProtocolEntry> Entries { get; } = new();

    public void SyncFromFindings(
        HaltungRecord record,
        IProtocolService protocolService,
        Func<string, string?> resolveTitle,
        Action<HaltungRecord> refreshRecord,
        bool refreshEntries,
        ICodeCatalogProvider codeCatalog)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(protocolService);
        ArgumentNullException.ThrowIfNull(resolveTitle);
        ArgumentNullException.ThrowIfNull(refreshRecord);
        ArgumentNullException.ThrowIfNull(codeCatalog);

        if (_isSyncing)
            return;

        if (record.VsaFindings is null || record.VsaFindings.Count == 0)
            return;

        var needsProtocol = record.Protocol is null
                            || (record.Protocol.Current?.Entries.Count ?? 0) == 0
                            && (record.Protocol.Original?.Entries.Count ?? 0) == 0;
        if (!needsProtocol)
            return;

        _isSyncing = true;
        try
        {
            var entries = VsaFindingToProtocolEntryMapper.BuildEntries(record.VsaFindings, resolveTitle);
            record.Protocol = protocolService.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", entries, null);
            refreshRecord(record);
            if (refreshEntries)
                Refresh(record, codeCatalog);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public void Refresh(HaltungRecord? selected, ICodeCatalogProvider codeCatalog)
    {
        ArgumentNullException.ThrowIfNull(codeCatalog);

        Entries.Clear();

        var list = selected?.Protocol?.Current?.Entries;
        if (list is null || list.Count == 0)
            return;

        foreach (var entry in list.Where(e => !e.IsDeleted))
        {
            if (string.IsNullOrWhiteSpace(entry.Beschreibung) || entry.Beschreibung.Length <= 3)
            {
                if (!string.IsNullOrWhiteSpace(entry.Code) &&
                    codeCatalog.TryGet(entry.Code, out var def) &&
                    !string.IsNullOrWhiteSpace(def.Title))
                {
                    entry.Beschreibung = def.Title;
                }
            }

            Entries.Add(entry);
        }
    }
}

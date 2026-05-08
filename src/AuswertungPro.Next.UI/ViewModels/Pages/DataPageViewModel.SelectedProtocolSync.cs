using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

// SelectedProtocolEntries-Sync: stellt sicher dass die im Grid sichtbare
// Beobachtungs-Liste mit den VSA-Findings + ProtocolDocument-Eintraegen
// uebereinstimmt. Auch BuildEntriesFromFindings (mapped XTF/IBAK-Findings
// in ProtocolEntry) lebt hier, weil es genau dieser Sync-Logik dient.
public sealed partial class DataPageViewModel
{
    private bool _isSyncingSelectedProtocol;

    private void SyncSelectedProtocolFromFindings(HaltungRecord record)
    {
        if (_isSyncingSelectedProtocol)
            return;

        if (record.VsaFindings is null || record.VsaFindings.Count == 0)
            return;

        var needsProtocol = record.Protocol is null
                            || (record.Protocol.Current?.Entries.Count ?? 0) == 0
                            && (record.Protocol.Original?.Entries.Count ?? 0) == 0;
        if (!needsProtocol)
            return;

        _isSyncingSelectedProtocol = true;
        try
        {
            var entries = BuildEntriesFromFindings(record.VsaFindings);
            record.Protocol = App.Resolve<AuswertungPro.Next.Application.Protocol.IProtocolService>().EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", entries, null);
            RefreshRecordInGrid(record);
            if (Selected?.Id == record.Id)
                RefreshSelectedProtocolEntries();
        }
        finally
        {
            _isSyncingSelectedProtocol = false;
        }
    }

    private void RefreshSelectedProtocolEntries()
    {
        SelectedProtocolEntries.Clear();
        var list = Selected?.Protocol?.Current?.Entries;
        if (list is null || list.Count == 0)
            return;

        foreach (var entry in list.Where(e => !e.IsDeleted))
        {
            // Beschreibung aus dem VSA-Katalog auflösen, wenn sie leer ist
            if (string.IsNullOrWhiteSpace(entry.Beschreibung) || entry.Beschreibung.Length <= 3)
            {
                if (!string.IsNullOrWhiteSpace(entry.Code) &&
                    App.Resolve<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>().TryGet(entry.Code, out var def) &&
                    !string.IsNullOrWhiteSpace(def.Title))
                {
                    entry.Beschreibung = def.Title;
                }
            }

            SelectedProtocolEntries.Add(entry);
        }
    }

    private IReadOnlyList<ProtocolEntry> BuildEntriesFromFindings(IEnumerable<VsaFinding> findings)
    {
        var list = new List<ProtocolEntry>();
        foreach (var f in findings)
        {
            var mStart = f.MeterStart ?? f.SchadenlageAnfang;
            var mEnd = f.MeterEnd ?? f.SchadenlageEnde;
            var time = ParseMpegTime(f.MPEG) ?? (f.Timestamp?.TimeOfDay);

            var beschreibung = f.Raw?.Trim() ?? string.Empty;
            // Beschreibung aus dem VSA-Katalog auflösen, wenn Raw leer oder nur Kuerzel
            var code = f.KanalSchadencode?.Trim() ?? string.Empty;
            if ((string.IsNullOrWhiteSpace(beschreibung) || beschreibung.Length <= 3) &&
                !string.IsNullOrWhiteSpace(code) &&
                App.Resolve<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>().TryGet(code, out var codeDef) &&
                !string.IsNullOrWhiteSpace(codeDef.Title))
            {
                beschreibung = codeDef.Title;
            }

            var entry = new ProtocolEntry
            {
                Code = code,
                Beschreibung = beschreibung,
                MeterStart = mStart,
                MeterEnd = mEnd,
                IsStreckenschaden = mStart.HasValue && mEnd.HasValue && mEnd >= mStart,
                Mpeg = f.MPEG,
                Zeit = time,
                Source = ProtocolEntrySource.Imported
            };

            if (!string.IsNullOrWhiteSpace(f.Quantifizierung1) || !string.IsNullOrWhiteSpace(f.Quantifizierung2))
            {
                entry.CodeMeta = new ProtocolEntryCodeMeta
                {
                    Code = entry.Code,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Quantifizierung1"] = f.Quantifizierung1 ?? string.Empty,
                        ["Quantifizierung2"] = f.Quantifizierung2 ?? string.Empty
                    },
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            if (!string.IsNullOrWhiteSpace(f.FotoPath))
                entry.FotoPaths.Add(f.FotoPath);

            list.Add(entry);
        }

        return list;
    }

    private static TimeSpan? ParseMpegTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
            return parsed;

        return null;
    }
}

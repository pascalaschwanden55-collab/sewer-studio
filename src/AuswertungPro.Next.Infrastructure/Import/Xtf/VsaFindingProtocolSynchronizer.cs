using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Uebertraegt importierte VSA-Funde in das Haltungsprotokoll.
/// Die Klasse kennt weder Dateien noch Projekt-Merge oder Importstatistiken.
/// </summary>
internal static class VsaFindingProtocolSynchronizer
{
    public static void Sync(
        HaltungRecord record,
        IReadOnlyList<VsaFinding> findings,
        IProtocolService? protocolService = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(findings);

        if (findings.Count == 0)
            return;

        var hasProtocolEntries = record.Protocol is not null
            && ((record.Protocol.Current?.Entries.Count ?? 0) > 0
                || (record.Protocol.Original?.Entries.Count ?? 0) > 0);

        if (!hasProtocolEntries)
        {
            var entries = BuildImportedEntries(findings);
            if (entries.Count > 0)
            {
                record.Protocol = (protocolService ?? new ProtocolService()).EnsureProtocol(
                    record.GetFieldValue("Haltungsname") ?? "",
                    entries,
                    null);
            }

            return;
        }

        if (record.Protocol is null)
            return;

        SyncRevision(record.Protocol.Original, findings);
        SyncRevision(record.Protocol.Current, findings);
        foreach (var revision in record.Protocol.History)
            SyncRevision(revision, findings);
    }

    private static List<ProtocolEntry> BuildImportedEntries(IReadOnlyList<VsaFinding> findings)
    {
        var entries = new List<ProtocolEntry>(findings.Count);
        foreach (var finding in findings)
        {
            if (string.IsNullOrWhiteSpace(finding.KanalSchadencode))
                continue;

            var meterStart = FindingEntryMatcher.GetFindingMeterStart(finding);
            var meterEnd = FindingEntryMatcher.GetFindingMeterEnd(finding);
            var entry = new ProtocolEntry
            {
                Code = finding.KanalSchadencode.Trim(),
                Beschreibung = finding.Raw?.Trim() ?? string.Empty,
                MeterStart = meterStart,
                MeterEnd = meterEnd,
                IsStreckenschaden = meterStart.HasValue && meterEnd.HasValue && meterEnd >= meterStart,
                Mpeg = finding.MPEG,
                Zeit = XtfValueNormalizer.ParseMpegTime(finding.MPEG) ?? finding.Timestamp?.TimeOfDay,
                Source = ProtocolEntrySource.Imported
            };

            SetCodeMetadata(entry, finding);
            if (!string.IsNullOrWhiteSpace(finding.FotoPath))
                entry.FotoPaths.Add(finding.FotoPath);

            entries.Add(entry);
        }

        return entries;
    }

    private static void SetCodeMetadata(ProtocolEntry entry, VsaFinding finding)
    {
        var clock = VsaFindingClockResolver.Resolve(finding);
        var clockStart = clock.Start?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var clockEnd = clock.End?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(finding.Quantifizierung1)
            && string.IsNullOrWhiteSpace(finding.Quantifizierung2)
            && clockStart is null
            && clockEnd is null)
            return;

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Quantifizierung1"] = finding.Quantifizierung1 ?? string.Empty,
            ["Quantifizierung2"] = finding.Quantifizierung2 ?? string.Empty
        };
        if (!string.IsNullOrWhiteSpace(clockStart))
            parameters["vsa.uhr.von"] = clockStart;
        if (!string.IsNullOrWhiteSpace(clockEnd))
            parameters["vsa.uhr.bis"] = clockEnd;

        entry.CodeMeta = new ProtocolEntryCodeMeta
        {
            Code = entry.Code,
            Parameters = parameters,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static void SyncRevision(ProtocolRevision? revision, IReadOnlyList<VsaFinding> findings)
    {
        if (revision?.Entries is null || revision.Entries.Count == 0)
            return;

        foreach (var entry in revision.Entries)
        {
            if (entry.IsDeleted || entry.Source != ProtocolEntrySource.Imported)
                continue;

            var finding = FindingEntryMatcher.FindBestFindingForEntry(entry, findings);
            if (finding is null)
                continue;

            entry.MeterStart ??= FindingEntryMatcher.GetFindingMeterStart(finding);
            entry.MeterEnd ??= FindingEntryMatcher.GetFindingMeterEnd(finding);
            if (string.IsNullOrWhiteSpace(entry.Beschreibung) && !string.IsNullOrWhiteSpace(finding.Raw))
                entry.Beschreibung = finding.Raw.Trim();
            if (string.IsNullOrWhiteSpace(entry.Mpeg) && !string.IsNullOrWhiteSpace(finding.MPEG))
                entry.Mpeg = finding.MPEG;
            entry.Zeit ??= XtfValueNormalizer.ParseMpegTime(finding.MPEG) ?? finding.Timestamp?.TimeOfDay;

            if (string.IsNullOrWhiteSpace(finding.FotoPath))
                continue;

            entry.FotoPaths ??= new List<string>();
            if (!entry.FotoPaths.Any(path =>
                    string.Equals(path, finding.FotoPath, StringComparison.OrdinalIgnoreCase)))
                entry.FotoPaths.Add(finding.FotoPath);
        }
    }
}

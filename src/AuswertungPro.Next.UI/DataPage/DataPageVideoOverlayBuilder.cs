using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageVideoOverlayBuilder
{
    public static PlayerDamageOverlayData? Build(HaltungRecord record)
    {
        // Haltungslaenge nach der gemeinsamen Leseregel (Deepscan A4).
        if (HaltungFeldwerte.LiesLaenge(record) is not double pipeLength || pipeLength <= 0)
            return null;

        var markers = new List<DamageMarkerInfo>();
        if (record.Protocol?.Current?.Entries is { Count: > 0 } entries)
        {
            foreach (var entry in entries.Where(e => !e.IsDeleted && e.MeterStart.HasValue))
            {
                markers.Add(new DamageMarkerInfo(
                    entry.Code ?? "",
                    entry.Beschreibung,
                    entry.MeterStart!.Value,
                    entry.MeterEnd,
                    entry.IsStreckenschaden));
            }
        }
        else if (record.VsaFindings is { Count: > 0 } findings)
        {
            foreach (var finding in findings)
            {
                var meterStart = finding.MeterStart ?? finding.SchadenlageAnfang;
                if (meterStart is null)
                    continue;

                var meterEnd = finding.MeterEnd ?? finding.SchadenlageEnde;
                markers.Add(new DamageMarkerInfo(
                    finding.KanalSchadencode?.Trim() ?? "",
                    finding.Raw,
                    meterStart.Value,
                    meterEnd,
                    meterEnd.HasValue && meterEnd.Value > meterStart.Value));
            }
        }

        return markers.Count > 0
            ? new PlayerDamageOverlayData(pipeLength, markers)
            : null;
    }
}

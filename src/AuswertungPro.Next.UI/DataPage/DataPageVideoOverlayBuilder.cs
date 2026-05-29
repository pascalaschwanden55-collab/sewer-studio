using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageVideoOverlayBuilder
{
    public static PlayerDamageOverlayData? Build(HaltungRecord record)
    {
        var lengthStr = record.GetFieldValue("Haltungslaenge_m");
        if (!double.TryParse(lengthStr?.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var pipeLength)
            || pipeLength <= 0)
        {
            return null;
        }

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

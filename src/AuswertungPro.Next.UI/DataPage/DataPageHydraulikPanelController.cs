using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageHydraulikPanelRequest(
    double? DnMillimeters,
    string? Material,
    double? WasserstandMillimeters);

/// <summary>
/// Bereitet die Haltungswerte fuer das Hydraulik-Panel vor.
/// Fenster- und ViewModel-Erzeugung bleiben im DataPageViewModel.
/// </summary>
public static class DataPageHydraulikPanelController
{
    public static DataPageHydraulikPanelRequest BuildOpenRequest(HaltungRecord? record)
    {
        if (record is null)
            return new DataPageHydraulikPanelRequest(null, null, null);

        return new DataPageHydraulikPanelRequest(
            DnValueParser.TryParseMillimeters(record.GetFieldValue("DN_mm")),
            record.GetFieldValue("Rohrmaterial"),
            WasserstandMillimeters: null);
    }
}

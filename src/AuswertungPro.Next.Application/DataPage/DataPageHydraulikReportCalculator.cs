using System.Globalization;
using AuswertungPro.Next.Application.Hydraulik;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.DataPage;

public sealed record DataPageHydraulikAvailability(double? DnMm, double? GefaellePromille)
{
    public bool IsAvailable => DnMm is > 0 && double.IsFinite(DnMm.Value)
        && GefaellePromille is > 0 && double.IsFinite(GefaellePromille.Value);
}

public static class DataPageHydraulikReportCalculator
{
    public static double? ParseGefaellePromille(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim().Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public static DataPageHydraulikAvailability ReadAvailability(HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new DataPageHydraulikAvailability(
            DnValueParser.TryParseMillimeters(record.GetFieldValue(FieldKeys.NominalDiameterMm)),
            ParseGefaellePromille(record.GetFieldValue(FieldKeys.SlopePromille)));
    }

    public static HydraulikCalcResult? BuildReportCalculation(
        HaltungRecord record,
        HydraulikPanelSettings panel,
        double? dnMm = null,
        Action? saveSettings = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(panel);

        var availability = ReadAvailability(record);
        if (dnMm.HasValue)
            availability = availability with { DnMm = dnMm };
        if (!availability.IsAvailable)
            return null;

        var dn = availability.DnMm!.Value;
        var material = HydraulikMaterialCatalog.Resolve(
            record.GetFieldValue(FieldKeys.PipeMaterial),
            panel.MaterialKey);
        var kb = panel.IsNeuzustand ? material.KbNeu : material.KbAlt;

        var input = new HydraulikInput(
            DN_mm: dn,
            Wasserstand_mm: dn / 2,
            Gefaelle_Promille: availability.GefaellePromille!.Value,
            Kb: kb,
            AbwasserTyp: "MR",
            Temperatur_C: panel.Temperatur);

        var result = HydraulikEngine.Berechne(input);
        if (result is null)
            return null;

        panel.Dn = dn;
        panel.MaterialKey = material.Key;
        saveSettings?.Invoke();

        return HydraulikCalcResultMapper.ToReportResult(input, result, material.Label);
    }
}

using System.Globalization;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Hydraulik;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageHydraulikAvailability(double? DnMm, double? GefaellePromille)
{
    public bool IsAvailable => DnMm is > 0 && GefaellePromille is > 0;
}

public static class DataPageHydraulikReportCalculator
{
    public static double? ParseDnMm(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal);

        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
            && value > 0)
        {
            return value;
        }

        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value)
            && value > 0)
        {
            return value;
        }

        if (text.Contains(',') && text.Contains('.'))
        {
            var commaAsDecimal = text.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
            if (double.TryParse(commaAsDecimal, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0)
                return value;

            var dotAsDecimal = text.Replace(",", string.Empty, StringComparison.Ordinal);
            if (double.TryParse(dotAsDecimal, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0)
                return value;
        }
        else if (text.Contains(','))
        {
            var normalized = text.Replace(',', '.');
            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0)
                return value;
        }

        var digitsOnly = text.Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);
        if (double.TryParse(digitsOnly, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 50)
            return value;

        return null;
    }

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
            ParseDnMm(record.GetFieldValue("DN_mm")),
            ParseGefaellePromille(record.GetFieldValue("Gefaelle_Promille")));
    }

    public static HydraulikCalcResult? BuildReportCalculation(
        HaltungRecord record,
        AppSettings settings,
        double? dnMm = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(settings);

        var dn = dnMm ?? ParseDnMm(record.GetFieldValue("DN_mm")) ?? 300d;
        var panel = settings.HydraulikPanel ?? new HydraulikPanelSettings();
        var material = ResolveMaterial(record.GetFieldValue("Rohrmaterial"), panel.MaterialKey);
        var kb = panel.IsNeuzustand ? material.KbNeu : material.KbAlt;

        var input = new HydraulikInput(
            DN_mm: dn,
            Wasserstand_mm: dn / 2,
            Gefaelle_Promille: panel.Gefaelle,
            Kb: kb,
            AbwasserTyp: "MR",
            Temperatur_C: panel.Temperatur);

        var result = HydraulikEngine.Berechne(input);
        return result is null
            ? null
            : HydraulikCalcResultMapper.ToReportResult(input, result, material.Label);
    }

    private static MaterialOption ResolveMaterial(string? recordMaterial, string? settingsMaterialKey)
    {
        var material = HydraulikPanelViewModel.Materialien.FirstOrDefault(m =>
                string.Equals(m.Key, settingsMaterialKey, StringComparison.OrdinalIgnoreCase))
            ?? HydraulikPanelViewModel.Materialien[0];

        if (string.IsNullOrWhiteSpace(recordMaterial))
            return material;

        return HydraulikPanelViewModel.Materialien.FirstOrDefault(m =>
                m.Label.Contains(recordMaterial, StringComparison.OrdinalIgnoreCase)
                || m.Key.Equals(recordMaterial, StringComparison.OrdinalIgnoreCase))
            ?? material;
    }
}

using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingDnCalibrationState(
    int NominalDiameterMm,
    PipeCalibration? Calibration,
    string DnText,
    string CalibrationStatusText);

public static class CodingDnCalibrationPolicy
{
    public static CodingDnCalibrationState Build(IReadOnlyDictionary<string, string>? fields)
    {
        if (fields != null
            && fields.TryGetValue("DN_mm", out var rawDn)
            && int.TryParse(rawDn, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dn)
            && dn > 0)
        {
            return new CodingDnCalibrationState(
                dn,
                new PipeCalibration { NominalDiameterMm = dn },
                $"DN: {dn} mm",
                "Nicht kalibriert");
        }

        return new CodingDnCalibrationState(
            0,
            null,
            "DN: unbekannt",
            "Nicht kalibriert");
    }
}

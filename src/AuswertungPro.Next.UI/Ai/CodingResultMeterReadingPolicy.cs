using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai;

public readonly record struct CodingAcceptedMeterReading(double Meter, double TimestampSeconds);

public static class CodingResultMeterReadingPolicy
{
    private const double MaxPlausibleOsdMeter = 500.0;

    public static bool TryAccept(LiveDetection result, out CodingAcceptedMeterReading reading)
    {
        if (result.MeterReading is < 0 or > MaxPlausibleOsdMeter or null)
        {
            reading = default;
            return false;
        }

        reading = new CodingAcceptedMeterReading(result.MeterReading.Value, result.TimestampSeconds);
        return true;
    }
}

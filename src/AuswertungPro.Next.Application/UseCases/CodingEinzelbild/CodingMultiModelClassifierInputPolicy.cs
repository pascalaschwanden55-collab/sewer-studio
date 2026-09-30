namespace AuswertungPro.Next.Application.UseCases.CodingEinzelbild;

public sealed record CodingMultiModelClassifierInput(
    int NominalDiameterMm,
    double CurrentMeter,
    double ReachLength);

/// <summary>
/// Eingabe des Bildklassifikators: ohne Kalibrierung gilt DN 300, die Reichweite ist der
/// Endmeter oder, falls unbekannt, der aktuelle Meter (mindestens 1 m).
/// </summary>
public static class CodingMultiModelClassifierInputPolicy
{
    public static CodingMultiModelClassifierInput Build(
        int? nominalDiameterMm,
        double currentMeter,
        double? endMeter)
    {
        var reachLength = endMeter > 0
            ? endMeter.Value
            : Math.Max(currentMeter, 1);

        return new CodingMultiModelClassifierInput(
            nominalDiameterMm ?? 300,
            currentMeter,
            reachLength);
    }
}

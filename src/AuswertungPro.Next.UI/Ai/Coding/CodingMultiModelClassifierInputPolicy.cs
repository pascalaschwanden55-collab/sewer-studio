namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingMultiModelClassifierInput(
    int NominalDiameterMm,
    double CurrentMeter,
    double ReachLength);

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

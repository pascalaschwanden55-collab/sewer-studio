using AuswertungPro.Next.Infrastructure.Ai.Configuration;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

public static class PipelineEnvironmentOptions
{
    public const string ClassifierDecisionEnvVar = "SEWERSTUDIO_CLASSIFIER_DECISION";
    public const string ClassifierOnlyStructuralOffEnvVar = "SEWERSTUDIO_CLASSIFIER_ONLY_STRUCTURAL_OFF";
    public const string ExpectedYoloModelEnvVar = "SEWERSTUDIO_EXPECTED_YOLO_MODEL";
    public const string YoloConfidenceEnvVar = "SEWERSTUDIO_YOLO_CONFIDENCE";
    public const string DinoBoxThresholdEnvVar = "SEWERSTUDIO_DINO_BOX_THRESHOLD";
    public const string DinoTextThresholdEnvVar = "SEWERSTUDIO_DINO_TEXT_THRESHOLD";
    public const string DefaultExpectedYoloModel = "yolo26m";

    public static bool ClassifierDecisionEnabled()
        => AiSettingsFactory.ParseBool(Environment.GetEnvironmentVariable(ClassifierDecisionEnvVar));

    public static bool ClassifierOnlyStructuralEnabled()
        => !AiSettingsFactory.ParseBool(Environment.GetEnvironmentVariable(ClassifierOnlyStructuralOffEnvVar));

    public static string ExpectedYoloModel()
        => Environment.GetEnvironmentVariable(ExpectedYoloModelEnvVar)?.Trim() is { Length: > 0 } expected
            ? expected
            : DefaultExpectedYoloModel;

    public static double? ReadDoubleWithCompat(string sewerStudioName)
    {
        var value = Environment.GetEnvironmentVariable(sewerStudioName)
                    ?? Environment.GetEnvironmentVariable(CompatName(sewerStudioName));

        return AiSettingsFactory.ParseDouble(value);
    }

    public static double ResolveDoubleWithCompat(string sewerStudioName, double defaultValue)
        => ReadDoubleWithCompat(sewerStudioName) ?? defaultValue;

    private static string CompatName(string sewerStudioName)
    {
        const string prefix = "SEWERSTUDIO_";
        return sewerStudioName.StartsWith(prefix, StringComparison.Ordinal)
            ? "AUSWERTUNGPRO_" + sewerStudioName[prefix.Length..]
            : sewerStudioName;
    }
}

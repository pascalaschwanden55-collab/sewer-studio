using AuswertungPro.Next.Infrastructure.Ai.Configuration;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

public interface IPipelineEnvironmentOptions
{
    bool ClassifierDecisionEnabled();

    bool ClassifierOnlyStructuralEnabled();

    string ExpectedYoloModel();

    double? ReadDoubleWithCompat(string sewerStudioName);

    double ResolveDoubleWithCompat(string sewerStudioName, double defaultValue);
}

/// <summary>
/// Liest die wenigen Umgebungsoptionen der Multi-Modell-Pipeline. Der Leser ist
/// austauschbar, damit Tests und Fabriken nicht den globalen Prozesszustand aendern muessen.
/// </summary>
public sealed class PipelineEnvironmentOptionsService : IPipelineEnvironmentOptions
{
    private readonly Func<string, string?> _readEnvironmentVariable;

    public PipelineEnvironmentOptionsService()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    public PipelineEnvironmentOptionsService(Func<string, string?> readEnvironmentVariable)
    {
        _readEnvironmentVariable = readEnvironmentVariable
            ?? throw new ArgumentNullException(nameof(readEnvironmentVariable));
    }

    public bool ClassifierDecisionEnabled()
        => AiSettingsFactory.ParseBool(
            _readEnvironmentVariable(PipelineEnvironmentOptions.ClassifierDecisionEnvVar));

    public bool ClassifierOnlyStructuralEnabled()
        => !AiSettingsFactory.ParseBool(
            _readEnvironmentVariable(PipelineEnvironmentOptions.ClassifierOnlyStructuralOffEnvVar));

    public string ExpectedYoloModel()
        => _readEnvironmentVariable(PipelineEnvironmentOptions.ExpectedYoloModelEnvVar)?.Trim()
               is { Length: > 0 } expected
            ? expected
            : PipelineEnvironmentOptions.DefaultExpectedYoloModel;

    public double? ReadDoubleWithCompat(string sewerStudioName)
    {
        var value = _readEnvironmentVariable(sewerStudioName)
                    ?? _readEnvironmentVariable(CompatName(sewerStudioName));

        return AiSettingsFactory.ParseDouble(value);
    }

    public double ResolveDoubleWithCompat(string sewerStudioName, double defaultValue)
        => ReadDoubleWithCompat(sewerStudioName) ?? defaultValue;

    private static string CompatName(string sewerStudioName)
    {
        const string prefix = "SEWERSTUDIO_";
        return sewerStudioName.StartsWith(prefix, StringComparison.Ordinal)
            ? "AUSWERTUNGPRO_" + sewerStudioName[prefix.Length..]
            : sewerStudioName;
    }
}

/// <summary>
/// Kompatible statische Fassade. Das Lesen des Prozesszustands liegt im
/// injizierbaren <see cref="IPipelineEnvironmentOptions"/>.
/// </summary>
public static class PipelineEnvironmentOptions
{
    private static readonly IPipelineEnvironmentOptions Default =
        new PipelineEnvironmentOptionsService();

    public const string ClassifierDecisionEnvVar = "SEWERSTUDIO_CLASSIFIER_DECISION";
    public const string ClassifierOnlyStructuralOffEnvVar = "SEWERSTUDIO_CLASSIFIER_ONLY_STRUCTURAL_OFF";
    public const string ExpectedYoloModelEnvVar = "SEWERSTUDIO_EXPECTED_YOLO_MODEL";
    public const string YoloConfidenceEnvVar = "SEWERSTUDIO_YOLO_CONFIDENCE";
    public const string DinoBoxThresholdEnvVar = "SEWERSTUDIO_DINO_BOX_THRESHOLD";
    public const string DinoTextThresholdEnvVar = "SEWERSTUDIO_DINO_TEXT_THRESHOLD";
    public const string DefaultExpectedYoloModel = "yolo26m";

    public static IPipelineEnvironmentOptions Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IPipelineEnvironmentOptions options) =>
        throw new NotSupportedException(
            "Die globalen Pipeline-Umgebungswerte koennen nicht mehr ausgetauscht werden. " +
            "IPipelineEnvironmentOptions bitte per Konstruktor uebergeben.");

    public static bool ClassifierDecisionEnabled()
        => Current.ClassifierDecisionEnabled();

    public static bool ClassifierOnlyStructuralEnabled()
        => Current.ClassifierOnlyStructuralEnabled();

    public static string ExpectedYoloModel()
        => Current.ExpectedYoloModel();

    public static double? ReadDoubleWithCompat(string sewerStudioName)
        => Current.ReadDoubleWithCompat(sewerStudioName);

    public static double ResolveDoubleWithCompat(string sewerStudioName, double defaultValue)
        => Current.ResolveDoubleWithCompat(sewerStudioName, defaultValue);
}

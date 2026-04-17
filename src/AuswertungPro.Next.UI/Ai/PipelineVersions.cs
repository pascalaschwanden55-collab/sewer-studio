namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// V4.2 Nachbesserung B: Zentrale Versions-Konstanten.
///
/// Jede Analyse-Ausgabe (DamageVerification, DinoV2-Response, DifferenceEntry, KB-Sample)
/// bekommt diese Tags, damit spaetere Ursachenanalyse nachvollziehen kann, mit welcher
/// Pipeline/Prompt/Modell-Version ein Ergebnis produziert wurde.
///
/// Regel: Bei BREAKING Prompt- oder Modell-Aenderungen → Version hochziehen.
/// Ohne Breaking Change bleibt die Version stabil.
/// </summary>
public static class PipelineVersions
{
    /// <summary>Gesamte Pipeline-Version (Durchgaengig fuer Nachtbatch + Codier-Modus).</summary>
    public const string Pipeline = "v4.2.0";

    /// <summary>Version des Verifikationsprompts (EnhancedVisionAnalysisService.VerifyCodeAsync).</summary>
    public const string VerifyPrompt = "v4.2.0-verify";

    /// <summary>Version des Open-Set-Damage-Prompts (DamageClassesPromptFull).</summary>
    public const string DamagePromptFull = "v4.2.0-damage-full";

    /// <summary>Version des kurzen Codier-Prompts (DamageClassesPrompt).</summary>
    public const string DamagePromptShort = "v4.2.0-damage-short";

    /// <summary>Encoder-ID fuer DINOv2-Heads (muss mit Sidecar-Wrapper uebereinstimmen).</summary>
    public const string DinoV2Encoder = "facebook/dinov2-large";

    /// <summary>Schema-Version fuer Uncertainty-Score (bei Aenderung der Formel hochziehen).</summary>
    public const string UncertaintyScore = "v4.2.0";
}

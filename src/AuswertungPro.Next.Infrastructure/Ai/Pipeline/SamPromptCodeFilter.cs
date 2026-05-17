using System;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

public static class SamPromptCodeFilter
{
    /// <summary>
    /// Lokale SAM-Prompts nur fuer lokale Befunde. Strukturcodes wie Bogen,
    /// Rohranfang und Rohrende beschreiben die Haltung/Geometrie und erzeugen
    /// als Box-Prompt instabile Vollrohr-Masken.
    /// </summary>
    public static bool ShouldUseAsSamPrompt(string? labelOrCode)
    {
        var code = VsaCodeResolver.NormalizeFindingCode(labelOrCode)
                   ?? VsaCodeResolver.InferCodeFromLabel(labelOrCode);

        if (string.IsNullOrWhiteSpace(code))
            return true;

        return !code.StartsWith("BCC", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(code, "BCD", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(code, "BCE", StringComparison.OrdinalIgnoreCase);
    }
}

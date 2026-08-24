using System;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

/// <summary>
/// Die GeoUr-Abnahmetests sprechen mit dem echten oeffentlichen Kartendienst des
/// Kantons Uri. Bei mehreren Laeufen an einem Tag drosselt der Kanton mit HTTP 429
/// und die ganze Suite waere rot, ohne dass am Code etwas fehlt — zusaetzlich wird
/// der oeffentliche Dienst einer Behoerde unnoetig belastet. Deshalb laufen diese
/// Tests nur noch auf ausdruecklichen Zuruf ueber die Umgebungsvariable
/// <see cref="VariablenName"/> (Wert <c>1</c> oder <c>true</c>, gross/klein egal).
/// Fehlt sie, wird der Test bei der Entdeckung sichtbar uebersprungen.
/// </summary>
internal sealed class GeoUrLiveFactAttribute : FactAttribute
{
    /// <summary>
    /// Name der Umgebungsvariablen, die die GeoUr-Abnahmetests einschaltet.
    /// </summary>
    public const string VariablenName = "SEWER_GEOUR_LIVE_ACCEPTANCE";

    public GeoUrLiveFactAttribute()
    {
        Skip = IstEingeschaltet(Environment.GetEnvironmentVariable(VariablenName)) ? null : "GeoUr-Abnahme nur auf Zuruf: Umgebungsvariable SEWER_GEOUR_LIVE_ACCEPTANCE=1 setzen, um wirklich gegen die echten Kanton-Uri-Dienste zu laufen.";
    }

    private static bool IstEingeschaltet(string? wert)
        => string.Equals(wert, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(wert, "true", StringComparison.OrdinalIgnoreCase);
}

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training;

/// <summary>
/// Gueltiger, fachlich leerer Eval-Schutzordner fuer Tests ohne echte Pruefdaten
/// (Deepscan 02.10.2026, A1/R2, Entscheid E1): Seit der Sperre braucht jedes Laden und
/// Speichern von Trainingssamples einen lesbaren Pruefdaten-Ordner. Die reservierte
/// Haltung kommt in keinem Testsample vor; der Schutz ist damit aktiv, filtert aber nichts.
/// Nie den echten Ordner unter C:\KI_BRAIN verwenden.
/// </summary>
internal static class EvalSchutzTestOrdner
{
    public const string ReservierteHaltung = "999001-999002";

    /// <summary>Legt <paramref name="evalRoot"/> mit einer gueltigen Kandidatenliste an.</summary>
    public static string Anlegen(string evalRoot, string haltungKey = ReservierteHaltung)
    {
        Directory.CreateDirectory(evalRoot);
        File.WriteAllText(
            Path.Combine(evalRoot, "_candidates.json"),
            $$"""[{"haltung_key":"{{haltungKey}}"}]""");
        return evalRoot;
    }
}

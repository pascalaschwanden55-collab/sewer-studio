namespace AuswertungPro.Next.Application.Import;

/// <summary>Reine Anzeigeregeln; Zaehler beziehen sich immer auf den laufenden Schritt.</summary>
public static class ImportFortschrittText
{
    public static string Phase(int schritt, string name) => $"Schritt {schritt} von 7 · {name}";

    public static string Datei(ImportProgress fortschritt)
    {
        if (string.IsNullOrWhiteSpace(fortschritt.CurrentFile))
            return fortschritt.StatusText;
        var name = fortschritt.CurrentFile.Replace('\\', '/').Split('/').Last();
        var art = fortschritt.Phase == Phase(4, "Medien") ? "Haltung" : "Datei";
        return $"{art}: {name}";
    }

    public static bool IstBestimmt(ImportProgress fortschritt)
        => fortschritt.Total > 0 && fortschritt.Current >= 0 && fortschritt.Current <= fortschritt.Total;

    public static double Prozent(ImportProgress fortschritt)
        => IstBestimmt(fortschritt) ? 100.0 * fortschritt.Current / fortschritt.Total : 0;

    public static string Zaehler(ImportProgress fortschritt)
        => IstBestimmt(fortschritt) ? $"{fortschritt.Current} von {fortschritt.Total}" : "";

    public static string Restzeit(string phase, TimeSpan? restzeit)
    {
        if (restzeit is not { } dauer || dauer <= TimeSpan.Zero)
            return "";
        var name = phase.Split(" · ").Last();
        if (dauer.TotalMinutes < 1)
            return $"{name}: noch unter einer Minute";
        var minuten = Math.Ceiling(dauer.TotalMinutes);
        return $"{name}: noch ca. {minuten:0} {(minuten == 1 ? "Minute" : "Minuten")}";
    }
}

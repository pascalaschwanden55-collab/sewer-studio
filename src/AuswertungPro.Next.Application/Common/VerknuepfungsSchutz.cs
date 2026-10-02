using System.Security;

namespace AuswertungPro.Next.Application.Common;

/// <summary>Ergebnis einer Verknuepfungspruefung (Junction, Symlink, Reparse Point).</summary>
public enum VerknuepfungsBefund
{
    /// <summary>Keine Verknuepfung gefunden (fehlende Eintraege zaehlen nur mit <see cref="VerknuepfungsRegel.FehlendErlaubt"/>).</summary>
    Sicher,

    /// <summary>Ein geprueftes Glied ist eine Verknuepfung.</summary>
    Verknuepfung,

    /// <summary>Ein geprueftes Glied fehlt, und die Regel verlangt es.</summary>
    Fehlt,

    /// <summary>Die Attribute eines Glieds waren nicht lesbar, und die Regel sperrt dann.</summary>
    NichtPruefbar,

    /// <summary>Der Pfad liegt nicht unter der Wurzel; alle Vorfahren bis zum Laufwerk waren frei.</summary>
    Ausserhalb,
}

/// <summary>
/// Pruefergebnis mit dem betroffenen Pfad. <see cref="Fehler"/> traegt bei <see cref="VerknuepfungsBefund.Fehlt"/>
/// und <see cref="VerknuepfungsBefund.NichtPruefbar"/> die urspruengliche Ausnahme, damit ein Aufrufer sie
/// unveraendert weitergeben kann.
/// </summary>
public readonly record struct VerknuepfungsPruefung(VerknuepfungsBefund Befund, string? Pfad, Exception? Fehler)
{
    public bool IstSicher => Befund == VerknuepfungsBefund.Sicher;

    public static VerknuepfungsPruefung Frei => new(VerknuepfungsBefund.Sicher, null, null);
}

/// <summary>
/// Ausdrueckliche Regel einer Verknuepfungspruefung. Die bisherigen lokalen Kopien unterschieden
/// sich genau in diesen vier Punkten, ohne dass es benannt war (Deepscan 02.10.2026, A5).
/// </summary>
public sealed record VerknuepfungsRegel
{
    /// <summary>Die Wurzel selbst wird mitgeprueft.</summary>
    public bool WurzelEinschliessen { get; init; }

    /// <summary>Auch alle Ordner oberhalb der Wurzel bis zum Laufwerk werden geprueft.</summary>
    public bool OberhalbPruefen { get; init; }

    /// <summary>Nicht lesbare Attribute sperren (<see cref="VerknuepfungsBefund.NichtPruefbar"/>); sonst gilt das Glied als frei.</summary>
    public bool BeiFehlerSperren { get; init; } = true;

    /// <summary>Ein noch fehlendes Glied gilt als frei (es kann keine Verknuepfung sein); sonst <see cref="VerknuepfungsBefund.Fehlt"/>.</summary>
    public bool FehlendErlaubt { get; init; } = true;

    /// <summary>Spiegel und Verwaisten-Loeschung (<c>ReparsePointGuard</c>): Wurzel bereits geprueft, Lesefehler offen.</summary>
    public static VerknuepfungsRegel Spiegel { get; } = new() { BeiFehlerSperren = false };

    /// <summary>Projekt-Schreibgrenze (<c>ProjectMutationPathPolicy</c>): Wurzel und alle Vorfahren bis zum Laufwerk.</summary>
    public static VerknuepfungsRegel ProjektSchreibgrenze { get; } = new() { WurzelEinschliessen = true, OberhalbPruefen = true };

    /// <summary>Geschuetzte Gold-Speicher: Wurzel eingeschlossen, jedes Glied muss vorhanden und lesbar sein.</summary>
    public static VerknuepfungsRegel GoldSpeicher { get; } = new() { WurzelEinschliessen = true, FehlendErlaubt = false };

    /// <summary>Trainingsablage (<c>TrainingInventoryPaths</c>): ganzer Pfad ab Laufwerk, fehlender Rest erlaubt, Lesefehler sperren.</summary>
    public static VerknuepfungsRegel GanzerPfad { get; } = new();
}

/// <summary>
/// Gemeinsame Pruefung gegen Verknuepfungen/Junctions in Pfaden (Querschnittsregel «keine Verknuepfungen
/// betreten»). Die Regel ist ausdruecklich; Fehlermeldung und Ausnahmetyp waehlt weiter der Aufrufer.
/// </summary>
public static class VerknuepfungsSchutz
{
    /// <summary>Prueft genau einen Eintrag.</summary>
    public static VerknuepfungsPruefung PruefeEintrag(
        string pfad,
        VerknuepfungsRegel regel,
        Func<string, FileAttributes>? leseAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(regel);
        var lesen = leseAttribute ?? File.GetAttributes;
        try
        {
            return (lesen(pfad) & FileAttributes.ReparsePoint) != 0
                ? new VerknuepfungsPruefung(VerknuepfungsBefund.Verknuepfung, pfad, null)
                : VerknuepfungsPruefung.Frei;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return regel.FehlendErlaubt
                ? VerknuepfungsPruefung.Frei
                : new VerknuepfungsPruefung(VerknuepfungsBefund.Fehlt, pfad, ex);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or SecurityException
                                   or NotSupportedException
                                   or ArgumentException)
        {
            return regel.BeiFehlerSperren
                ? new VerknuepfungsPruefung(VerknuepfungsBefund.NichtPruefbar, pfad, ex)
                : VerknuepfungsPruefung.Frei;
        }
    }

    /// <summary>Prueft den ganzen Pfad bis unter das Laufwerk (die Laufwerkswurzel selbst nicht).</summary>
    public static VerknuepfungsPruefung PruefePfadAbLaufwerk(
        string pfad,
        VerknuepfungsRegel regel,
        Func<string, FileAttributes>? leseAttribute = null)
    {
        var voll = Path.GetFullPath(pfad);
        var laufwerk = Path.GetPathRoot(voll);
        return string.IsNullOrWhiteSpace(laufwerk)
            ? VerknuepfungsPruefung.Frei
            : PruefeKette(laufwerk, voll, regel, leseAttribute);
    }

    /// <summary>
    /// Prueft <paramref name="pfad"/> und seine Elternordner bis zur <paramref name="wurzel"/>, vom Pfad
    /// aufwaerts; der erste Befund gewinnt. Liegt der Pfad nicht unter der Wurzel, werden alle Vorfahren
    /// bis zum Laufwerk geprueft und danach <see cref="VerknuepfungsBefund.Ausserhalb"/> gemeldet.
    /// </summary>
    public static VerknuepfungsPruefung PruefeKette(
        string wurzel,
        string pfad,
        VerknuepfungsRegel regel,
        Func<string, FileAttributes>? leseAttribute = null)
    {
        ArgumentNullException.ThrowIfNull(regel);
        var volleWurzel = Path.TrimEndingDirectorySeparator(Path.GetFullPath(wurzel));
        var aktuell = Path.TrimEndingDirectorySeparator(Path.GetFullPath(pfad));

        while (!string.Equals(aktuell, volleWurzel, StringComparison.OrdinalIgnoreCase))
        {
            var befund = PruefeEintrag(aktuell, regel, leseAttribute);
            if (!befund.IstSicher)
                return befund;

            var eltern = Path.GetDirectoryName(aktuell);
            if (eltern is null || eltern.Length >= aktuell.Length)
                return new VerknuepfungsPruefung(VerknuepfungsBefund.Ausserhalb, pfad, null);
            aktuell = eltern;
        }

        if (regel.WurzelEinschliessen)
        {
            var befund = PruefeEintrag(volleWurzel, regel, leseAttribute);
            if (!befund.IstSicher)
                return befund;
        }

        if (!regel.OberhalbPruefen)
            return VerknuepfungsPruefung.Frei;

        for (var vorfahr = Path.GetDirectoryName(volleWurzel); vorfahr is not null; vorfahr = Path.GetDirectoryName(vorfahr))
        {
            var befund = PruefeEintrag(vorfahr, regel, leseAttribute);
            if (!befund.IstSicher)
                return befund;
        }

        return VerknuepfungsPruefung.Frei;
    }
}

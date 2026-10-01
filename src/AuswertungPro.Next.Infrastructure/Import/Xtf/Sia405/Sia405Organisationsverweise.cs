using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.Sia405;

/// <summary>
/// Prueft die Organisationsverweise (Eigentuemer, Datenherr, Datenlieferant) einer
/// SIA405-Datei nach einer Regel fuer Haltungen und Schaechte (seit 01.10.2026; die
/// Schaechte kamen in der zweiten Runde dazu):
/// <list type="bullet">
/// <item>Organisation in der Datei, aber ohne Bezeichnung -> Warnung (Rueckgabe von <see cref="Pruefe"/>).</item>
/// <item>Kennung nicht in der Datei -> nach Norm EXTERNAL und erlaubt (wie <c>DssExportPruefung</c>);
/// nur gezaehlt und am Ende als ein gesammelter Hinweis je Datei ausgegeben (<see cref="Hinweis"/>).</item>
/// </list>
/// Hier wird nur gemeldet, kein Wert geaendert.
/// </summary>
internal sealed class Sia405Organisationsverweise
{
    private readonly IReadOnlySet<string> _inDatei;

    // Rolle + Kennung -> Anzahl Haltungen und Schaechte, in Reihenfolge des ersten Auftretens.
    private readonly List<(string Rolle, string Kennung, int Haltungen, int Schaechte)> _externe = new();

    /// <param name="organisationenInDatei">Die Kennungen ALLER Organisationen der Datei, auch ohne Bezeichnung.</param>
    public Sia405Organisationsverweise(IReadOnlySet<string> organisationenInDatei)
    {
        ArgumentNullException.ThrowIfNull(organisationenInDatei);
        _inDatei = organisationenInDatei;
    }

    /// <summary>
    /// Prueft einen Verweis, der sich nicht aufloesen liess. Gibt den Warnungstext zurueck,
    /// wenn die Organisation in der Datei steht, aber keine Bezeichnung traegt; sonst
    /// <c>null</c> (kein Verweis, aufgeloest oder extern und gezaehlt).
    /// </summary>
    /// <param name="kopf">Der Anfang der Meldung, z.B. <c>Haltung "X" (TID t): </c>.</param>
    public string? Pruefe(string kopf, string rolle, string? kennung, bool aufgeloest, bool schacht)
    {
        if (string.IsNullOrWhiteSpace(kennung) || aufgeloest)
            return null;

        if (_inDatei.Contains(kennung))
            return kopf + $"{rolle}-Verweis {kennung} zeigt auf eine Organisation ohne Bezeichnung – {rolle} nicht übernommen.";

        var stelle = _externe.FindIndex(e => e.Rolle == rolle && string.Equals(e.Kennung, kennung, StringComparison.OrdinalIgnoreCase));
        if (stelle < 0)
            _externe.Add((rolle, kennung, schacht ? 0 : 1, schacht ? 1 : 0));
        else
            _externe[stelle] = schacht
                ? _externe[stelle] with { Schaechte = _externe[stelle].Schaechte + 1 }
                : _externe[stelle] with { Haltungen = _externe[stelle].Haltungen + 1 };
        return null;
    }

    /// <summary>Der gesammelte Hinweis zu den externen Verweisen, oder <c>null</c>, wenn es keine gibt.</summary>
    public ImportMessage? Hinweis(string kontext)
    {
        if (_externe.Count == 0)
            return null;

        return new ImportMessage
        {
            Level = "Info",
            Context = kontext,
            Message = "Organisationsverweise ausserhalb der Datei (nach Norm zulässig, Name nicht übernommen): "
                      + string.Join(", ", _externe.Select(e => $"{e.Rolle} {e.Kennung} ({Anzahl(e.Haltungen, e.Schaechte)})"))
                      + "."
        };
    }

    private static string Anzahl(int haltungen, int schaechte)
    {
        var teile = new List<string>(2);
        if (haltungen > 0)
            teile.Add($"{haltungen} {(haltungen == 1 ? "Haltung" : "Haltungen")}");
        if (schaechte > 0)
            teile.Add($"{schaechte} {(schaechte == 1 ? "Schacht" : "Schächte")}");
        return string.Join(", ", teile);
    }
}

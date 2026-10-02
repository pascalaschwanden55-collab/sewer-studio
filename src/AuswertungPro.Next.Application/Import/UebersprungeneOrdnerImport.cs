using System.Collections.Generic;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Traegt uebersprungene Ordner der Importquelle in ein Importergebnis ein.
///
/// Jeder uebersprungene Ordner zaehlt als ein Fehler und steht als Zeile am Anfang der
/// Meldungen. So erscheint er in der Fehlerbilanz des Imports, und der Abschluss sagt
/// «nicht vollständig» statt «0 Fehler» (CLAUDE.md: ein Teillauf bleibt bis ins Ergebnis
/// als unvollständig sichtbar). Am Import selbst aendert sich nichts.
/// </summary>
public static class UebersprungeneOrdnerImport
{
    /// <summary>Prueft den Ordnerbaum der Quelle und ergaenzt das Ergebnis.</summary>
    public static Result<ImportStats> Ergaenze(Result<ImportStats> ergebnis, string? quellordner)
        => Ergaenze(ergebnis, UebersprungeneOrdner.PruefeBaum(quellordner));

    /// <summary>
    /// Ergaenzt das Ergebnis um die Berichtszeilen. Eine Zeile, die ein Teilimport schon
    /// gemeldet hat (auch mit vorangestellter Quelle wie «WinCan: …»), zaehlt nicht doppelt.
    /// Ein gescheiterter Import behaelt Fehlercode und Text; die Zeilen werden angehaengt,
    /// weil der fehlende Ordner oft der Grund des Scheiterns ist.
    /// </summary>
    public static Result<ImportStats> Ergaenze(Result<ImportStats> ergebnis, IReadOnlyList<string> meldungen)
    {
        ArgumentNullException.ThrowIfNull(ergebnis);
        if (meldungen is not { Count: > 0 })
            return ergebnis;

        if (!ergebnis.Ok || ergebnis.Value is null)
        {
            var teile = new List<string>();
            if (!string.IsNullOrWhiteSpace(ergebnis.ErrorMessage))
                teile.Add(ergebnis.ErrorMessage);
            teile.AddRange(meldungen.Where(meldung => ergebnis.ErrorMessage?.Contains(meldung, StringComparison.Ordinal) != true));
            return Result<ImportStats>.Fail(ergebnis.ErrorCode ?? "IMPORT_FAILED", string.Join(" | ", teile));
        }

        var stats = ergebnis.Value;
        var neu = meldungen.Where(meldung => !SchonGemeldet(stats.Messages, meldung)).ToList();
        if (neu.Count == 0)
            return ergebnis;

        return Result<ImportStats>.Success(stats with
        {
            Errors = stats.Errors + neu.Count,
            Messages = neu.Concat(stats.Messages).ToList()
        });
    }

    /// <summary>
    /// Nimmt die von einem Teilimport schon eingetragenen Zeilen samt ihrem Fehlerzaehler wieder
    /// heraus. Ein Sammelimport (KINS) ruft das fuer jeden Teilimport auf und traegt die Zeilen
    /// danach genau einmal mit <see cref="Ergaenze(Result{ImportStats}, IReadOnlyList{string})"/> ein.
    /// Sonst zaehlte derselbe Ordner bei WinCan und IBAK je einmal (Review PR #68).
    /// </summary>
    public static Result<ImportStats> Entferne(Result<ImportStats> ergebnis, IReadOnlyList<string> meldungen)
    {
        ArgumentNullException.ThrowIfNull(ergebnis);
        if (meldungen is not { Count: > 0 } || !ergebnis.Ok || ergebnis.Value is null)
            return ergebnis;

        var stats = ergebnis.Value;
        var entfernt = meldungen.Distinct(StringComparer.Ordinal).Count(meldung => stats.Messages.Contains(meldung));
        if (entfernt == 0)
            return ergebnis;

        return Result<ImportStats>.Success(stats with
        {
            Errors = Math.Max(0, stats.Errors - entfernt),
            Messages = stats.Messages.Where(text => !meldungen.Contains(text)).ToList()
        });
    }

    private static bool SchonGemeldet(IReadOnlyList<string> vorhandene, string meldung)
        => vorhandene.Any(text => string.Equals(text, meldung, StringComparison.Ordinal)
                                  || text.EndsWith(": " + meldung, StringComparison.Ordinal));
}

using System.Text;
using AuswertungPro.Next.Application.UseCases.Verteilung;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Schreibt Verteilberichte nach <c>&lt;Projekt&gt;\__IMPORT_REPORTS\verteilung_&lt;art&gt;_&lt;Zeit&gt;.txt</c>
/// — dort, wo auch die Importberichte liegen, damit «Berichte» auf der Importseite alles zeigt.
/// Best effort: ein Schreibfehler verhindert nie die Verteilung selbst.
/// </summary>
public sealed class VerteilberichtAblage : IVerteilberichtAblage
{
    private readonly Func<DateTime> _jetzt;

    public VerteilberichtAblage(Func<DateTime>? jetzt = null)
    {
        _jetzt = jetzt ?? (() => DateTime.Now);
    }

    public string? Schreibe(string projektOrdner, string art, string text)
    {
        if (string.IsNullOrWhiteSpace(projektOrdner) || !Directory.Exists(projektOrdner))
            return null;

        try
        {
            var guard = new ProjectWritePathGuard(projektOrdner);
            var ordner = guard.EnsureSafeDirectoryTarget(
                Path.Combine(projektOrdner, ProjectStructure.ImportReports));
            Directory.CreateDirectory(ordner);
            guard.EnsureSafeDirectoryTarget(ordner);

            var zeit = _jetzt();
            var name = AuswertungPro.Next.Application.Common.ProjectPathResolver.SanitizePathSegment(
                string.IsNullOrWhiteSpace(art) ? "Verteilung" : art.Trim());
            var stamm = $"verteilung_{name}_{zeit:yyyyMMdd_HHmmss}";
            var pfad = Path.Combine(ordner, stamm + ".txt");
            for (var n = 2; File.Exists(pfad); n++)
                pfad = Path.Combine(ordner, $"{stamm}_{n}.txt");
            pfad = guard.EnsureSafeFileTarget(pfad);

            var inhalt = new StringBuilder()
                .AppendLine($"Verteilung {art} {zeit:yyyy-MM-dd HH:mm:ss}")
                .AppendLine()
                .Append(text)
                .ToString();
            File.WriteAllText(pfad, inhalt, Encoding.UTF8);
            return pfad;
        }
        catch (Exception ex)
        {
            AuswertungPro.Next.Application.Common.BestEffort.ReportWarning(
                $"[Verteilbericht] nicht geschrieben ({art}): {ex.Message}");
            return null;
        }
    }
}

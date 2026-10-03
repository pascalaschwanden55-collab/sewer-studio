using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Prueft die Dateien eines Training-Center-Fallordners, bevor Scan oder Verteilung sie verwenden
/// (PR #85, aus <see cref="TrainingCenterImportService"/> ausgelagert).
///
/// Regeln: Videos und Protokolle im Fallordner sind selbst keine Verknuepfung (Regel Streng; die
/// Ordner darueber bis zur Scan-Wurzel betritt die sichere Ordnersuche nur ohne Verknuepfung).
/// Videoverweise <c>&lt;name&gt;.&lt;videoendung&gt;.link</c> werden nur lesend aufgeloest; ihr Ziel liegt
/// ausserhalb des Baums und wird deshalb als ganzer Pfad bis zum Laufwerk geprueft (Regel GanzerPfad).
/// Jede Ablehnung steht in der Hinweisliste; nichts wird still ausgelassen.
/// </summary>
internal sealed class TrainingCenterFallDateien
{
    internal enum Art
    {
        Video,
        Protokoll,
    }

    private readonly IReadOnlyCollection<string> _videoEndungen;
    private readonly Func<string, FileAttributes?>? _leseAttribute;

    public TrainingCenterFallDateien(
        IReadOnlyCollection<string> videoEndungen,
        Func<string, FileAttributes?>? leseAttribute)
    {
        _videoEndungen = videoEndungen;
        _leseAttribute = leseAttribute;
    }

    public bool IstVideo(string pfad) => _videoEndungen.Contains(Path.GetExtension(pfad).ToLowerInvariant());

    /// <summary>Eintrag im Fallordner ist keine Verknuepfung und pruefbar; sonst Hinweis und <c>false</c>.</summary>
    public bool IstUnverknuepft(string datei, Art art, ICollection<string>? hinweise)
    {
        var befund = VerknuepfungsSchutz.PruefeEintrag(datei, VerknuepfungsRegel.Streng, _leseAttribute);
        if (befund.IstSicher)
            return true;

        Melde(
            hinweise,
            art == Art.Video
                ? $"Video «{datei}» ist eine Verknüpfung (alter Lauf) oder nicht sicher prüfbar und wird nicht verwendet – "
                  + "bitte die Verteilung erneut ausführen."
                : $"Protokoll «{datei}» ist eine Verknüpfung oder nicht sicher prüfbar und wird nicht verwendet.",
            befund.Fehler);
        return false;
    }

    /// <summary>
    /// Videoziel ausserhalb des Baums (Verweisziel, Video der Verteilung): ganzer Pfad bis zum Laufwerk,
    /// fehlender Rest erlaubt, nicht pruefbare Glieder sperren.
    /// </summary>
    public VerknuepfungsPruefung PruefeVideoziel(string ziel)
        => VerknuepfungsSchutz.PruefePfadAbLaufwerk(ziel, VerknuepfungsRegel.GanzerPfad, _leseAttribute);

    /// <summary>
    /// Loest die Videoverweise der Haltungsverteilung nur lesend zum Originalvideo auf. Der Inhalt ist eine
    /// Zeile mit absolutem Pfad; uebernommen wird er nur mit Videoendung, sicherem Pfad und vorhandener Datei.
    /// </summary>
    public List<string> LoeseVideoverweiseAuf(IEnumerable<string> files, ICollection<string>? hinweise)
    {
        var videos = new List<string>();
        foreach (var verweis in files.Where(IstVideoverweis))
        {
            // Ist der Verweis selbst eine Verknuepfung, fuehrte das Lesen aus dem Baum heraus.
            var befund = VerknuepfungsSchutz.PruefeEintrag(verweis, VerknuepfungsRegel.Streng, _leseAttribute);
            if (!befund.IstSicher)
            {
                Melde(
                    hinweise,
                    $"Videoverweis «{verweis}» ist eine Verknüpfung oder nicht sicher prüfbar und wird nicht gelesen; "
                    + "Fall ohne Video geladen.",
                    befund.Fehler);
                continue;
            }

            string[] zeilen;
            try
            {
                zeilen = File.ReadAllLines(verweis)
                    .Where(zeile => !string.IsNullOrWhiteSpace(zeile))
                    .Select(zeile => zeile.Trim())
                    .ToArray();
            }
            catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or System.Security.SecurityException)
            {
                // Lesefehler werden gemeldet, nicht verschluckt; der Fall bleibt ohne Video.
                Melde(hinweise, $"Videoverweis «{verweis}» nicht lesbar: {UserError.Describe(ex)}", ex);
                continue;
            }

            var ziel = zeilen.Length == 1 ? zeilen[0] : null;
            if (ziel is null || !Path.IsPathFullyQualified(ziel) || !IstVideo(ziel))
            {
                Melde(hinweise, $"Videoverweis «{verweis}» zeigt auf kein vorhandenes Video; Fall ohne Video geladen.", null);
                continue;
            }

            // Vor File.Exists: die ganze Kette des Ziels, sonst folgte schon die Existenzpruefung einer Verknuepfung.
            var zielBefund = PruefeVideoziel(ziel);
            if (!zielBefund.IstSicher)
            {
                Melde(
                    hinweise,
                    $"Videoverweis «{verweis}» zeigt auf «{ziel}»; im Pfad liegt eine Verknüpfung oder ein nicht sicher "
                    + "prüfbarer Eintrag («{zielBefund.Pfad}»). Fall ohne Video geladen.",
                    zielBefund.Fehler);
                continue;
            }

            if (!File.Exists(ziel))
            {
                Melde(hinweise, $"Videoverweis «{verweis}» zeigt auf kein vorhandenes Video; Fall ohne Video geladen.", null);
                continue;
            }

            videos.Add(ziel);
        }

        return videos;
    }

    public bool IstVideoverweis(string pfad)
        => pfad.EndsWith(".link", StringComparison.OrdinalIgnoreCase)
           && IstVideo(Path.GetFileNameWithoutExtension(pfad));

    private static void Melde(ICollection<string>? hinweise, string meldung, Exception? ex)
    {
        hinweise?.Add(meldung);
        System.Diagnostics.Trace.WriteLine(
            $"[TrainingCenterImport] {meldung}" + (ex is null ? "" : $" ({ex.GetType().Name}: {ex.Message})"));
    }
}

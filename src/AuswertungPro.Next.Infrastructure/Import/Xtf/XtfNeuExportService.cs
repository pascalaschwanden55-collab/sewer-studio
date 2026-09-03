using System.Text;
using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Fuehrt den Erstexport zusammen: Verlaeufe holen, Plan bauen, Bericht schreiben und
/// die neue XTF veroeffentlichen.
///
/// Jeder Lauf bekommt einen eigenen Dateinamen mit Zeitstempel; nichts Bestehendes wird
/// ersetzt. Die Objektkennungen bleiben ueber Laeufe hinweg dieselben — nur so wird aus
/// einem zweiten Export eine Korrektur und nicht eine Verdopplung im Zielsystem.
/// </summary>
public sealed class XtfNeuExportService : IXtfNeuExportService
{
    private readonly IXtfVerlaufQuelle? _verlaeufe;

    public XtfNeuExportService(IXtfVerlaufQuelle? verlaeufe = null)
        => _verlaeufe = verlaeufe;

    public XtfNeuExportResult Erzeuge(XtfNeuExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Projekt is null)
            return new XtfNeuExportResult(false, "", "Es ist kein Projekt geladen.", null);

        if (string.IsNullOrWhiteSpace(request.ZielOrdner) && !request.NurPruefen)
            return new XtfNeuExportResult(false, "", "Es wurde kein Zielordner angegeben.", null);

        var verlaeufe = LiesVerlaeufe(out var quellHinweis);

        var plan = XtfNeuPlanBuilder.Build(
            request.Projekt.Data,
            request.Projekt.SchaechteData,
            request.Projekt.Id.ToString("N"),
            verlaeufe);

        var bericht = BaueBericht(plan, request.Projekt, quellHinweis);

        if (plan.Leer)
        {
            return new XtfNeuExportResult(
                false, bericht,
                "Es gibt nichts zu exportieren — kein Objekt erfuellt die Pflichtangaben.", null);
        }

        if (request.NurPruefen)
            return new XtfNeuExportResult(true, bericht, null, null);

        var stempel = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var name = Dateiname(request.Projekt.Name);
        var ziel = Path.Combine(request.ZielOrdner, $"{name}_{stempel}.xtf");

        var ergebnis = XtfNeuWriter.Schreibe(plan, ziel);
        return ergebnis.Ok
            ? new XtfNeuExportResult(true, bericht + $"\n\nGeschrieben: {ergebnis.Datei}", null, ergebnis.Datei)
            : new XtfNeuExportResult(false, bericht, ergebnis.Fehler, null);
    }

    private IReadOnlyDictionary<string, XtfNeuGeometrie>? LiesVerlaeufe(out string hinweis)
    {
        hinweis = "Keine QGIS-Quelle eingerichtet — die Objekte gehen ohne Verlauf hinaus.";
        if (_verlaeufe is null)
            return null;

        try
        {
            var gelesen = _verlaeufe.Lies();
            hinweis = gelesen.Count == 0
                ? $"Aus \"{_verlaeufe.Quellpfad}\" konnte kein Verlauf gelesen werden."
                : $"{gelesen.Count} Verlaeufe aus \"{_verlaeufe.Quellpfad}\".";
            return gelesen;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException
                                      or UnauthorizedAccessException)
        {
            // Eine unlesbare Bestandsquelle darf den Export nicht verhindern — die
            // Sachdaten stehen im Projekt und sind davon unabhaengig.
            hinweis = $"Die QGIS-Quelle war nicht lesbar ({ex.Message}) — Objekte ohne Verlauf.";
            return null;
        }
    }

    private static string BaueBericht(XtfNeuPlan plan, Domain.Models.Project projekt, string quellHinweis)
    {
        var text = new StringBuilder();
        text.AppendLine($"Projekt: {projekt.Name}");
        text.AppendLine();
        text.AppendLine($"Im Projekt: {projekt.Data.Count} Haltungen, {projekt.SchaechteData.Count} Schaechte.");
        text.AppendLine($"In die Datei: {plan.Haltungen} Haltungen, {plan.Schaechte} Schaechte " +
                        $"({plan.Objekte.Count} Objekte insgesamt).");
        text.AppendLine(quellHinweis);

        SchreibeHinweise(text, plan);

        text.AppendLine();
        text.AppendLine("Die Objektkennungen bleiben bei jedem Export dieselben. Ein zweiter Lauf");
        text.AppendLine("aktualisiert deshalb dieselben Objekte, statt neue anzulegen.");
        text.AppendLine("Datenherr und Datenlieferant tragen den Eigentuemer — in SIA405 sind beide");
        text.AppendLine("Pflicht, und fuer eine Ersterfassung ist das die naheliegende Angabe.");

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Die Hinweise, nach Art zusammengefasst.
    ///
    /// Ungefiltert waeren es im Alltag Dutzende gleichlautende Zeilen — bei einem
    /// Projekt ohne erfasste Eigentuemer eine je Schacht. Der Dialog waere unlesbar und
    /// die eine Zeile, auf die es ankommt, ginge darin unter.
    /// </summary>
    private static void SchreibeHinweise(StringBuilder text, XtfNeuPlan plan)
    {
        if (plan.Hinweise.Count == 0)
            return;

        var ohneEigentuemer = plan.Hinweise.Count(h => h.Contains("ohne Eigentuemer", StringComparison.Ordinal));
        var ohneVerlauf = plan.Hinweise.Count(h => h.Contains("kein Verlauf", StringComparison.Ordinal));
        var ohneSchacht = plan.Hinweise
            .Where(h => h.Contains("ist im Projekt nicht erfasst", StringComparison.Ordinal))
            .Count();
        var uebrige = plan.Hinweise
            .Where(h => !h.Contains("ohne Eigentuemer", StringComparison.Ordinal)
                     && !h.Contains("kein Verlauf", StringComparison.Ordinal)
                     && !h.Contains("ist im Projekt nicht erfasst", StringComparison.Ordinal))
            .ToList();

        text.AppendLine();
        text.AppendLine("Hinweise:");

        if (ohneEigentuemer > 0)
        {
            text.AppendLine(
                $"  {ohneEigentuemer} Objekte haben keinen Eigentuemer und bleiben deshalb draussen.");
            text.AppendLine(
                "  In SIA405 ist der Verweis auf eine Organisation Pflicht. Der Knopf");
            text.AppendLine(
                "  \"Leere Felder aus QGIS ergaenzen\" auf der Haltungs- und der Schachtseite");
            text.AppendLine(
                "  fuellt ihn dort, wo der Kataster ihn kennt.");
        }

        if (ohneVerlauf > 0)
            text.AppendLine($"  {ohneVerlauf} Haltungen ohne Verlauf in der QGIS-Kopie.");

        if (ohneSchacht > 0)
        {
            text.AppendLine(
                $"  {ohneSchacht} Haltungsenden verweisen auf Schaechte, die das Projekt nicht");
            text.AppendLine(
                "  fuehrt. Die Haltung geht trotzdem hinaus, ihr Endpunkt bleibt nur ohne");
            text.AppendLine("  Verbindung zum Schacht.");
        }

        // Alles Uebrige einzeln, aber gedeckelt: Eine lange Liste liest ohnehin niemand.
        foreach (var hinweis in uebrige.Take(20))
            text.AppendLine($"  {hinweis}");

        if (uebrige.Count > 20)
            text.AppendLine($"  … und {uebrige.Count - 20} weitere.");
    }

    /// <summary>Ein Dateiname aus dem Projektnamen, ohne fuer Windows unzulaessige Zeichen.</summary>
    private static string Dateiname(string? projekt)
    {
        var roh = (projekt ?? "").Trim();
        if (roh.Length == 0)
            return "SewerStudio-Export";

        var sauber = new StringBuilder(roh.Length);
        foreach (var zeichen in roh)
            sauber.Append(Path.GetInvalidFileNameChars().Contains(zeichen) ? '_' : zeichen);

        return sauber.ToString();
    }
}

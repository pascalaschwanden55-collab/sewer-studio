using System.Text;
using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Fuehrt den ganzen Weg zur revidierten XTF zusammen: Originaldateien im Projekt finden,
/// je Datei einen Plan bauen und ihn schreiben.
///
/// Kundenoriginale werden ausschliesslich gelesen. Die Revisionen landen in einem neuen
/// Ordner mit Zeitstempel — jeder Lauf bekommt seinen eigenen, es wird nie etwas ersetzt.
/// </summary>
public sealed class XtfRevisionExportService : IXtfRevisionExportService
{
    /// <summary>Ablagen, in denen der Import die XTF-Quellen des Projekts hinterlegt.</summary>
    private static readonly string[] QuellOrdner =
    {
        Path.Combine("Imports", "XTF"),
        Path.Combine("Importdateien", "XTF")
    };

    public XtfRevisionExportResult Erzeuge(XtfRevisionExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Projekt is null)
            return Fehler("Es ist kein Projekt geladen.");

        if (string.IsNullOrWhiteSpace(request.ZielOrdner))
            return Fehler("Es wurde kein Zielordner angegeben.");

        var quellen = FindeQuellen(request.ProjektPfad);
        if (quellen.Count == 0)
        {
            return Fehler(
                "Im Projekt wurde keine XTF-Quelldatei gefunden. Gesucht wird unter " +
                "'Imports\\XTF' und 'Importdateien\\XTF'.");
        }

        var stempel = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var ausgabe = Path.Combine(request.ZielOrdner, $"XTF-Revision_{stempel}");

        var bericht = new StringBuilder();
        var geschrieben = new List<string>();
        var fehler = new List<string>();

        foreach (var quelle in quellen)
        {
            var name = Path.GetFileName(quelle);
            IReadOnlyList<XtfKanalschadenElement> elemente;
            try
            {
                elemente = XtfKanalschadenElementReader.Read(quelle);
            }
            catch (Exception ex)
            {
                fehler.Add($"{name}: nicht lesbar ({ex.Message})");
                continue;
            }

            IReadOnlyList<XtfStammdatenElement> stammdaten;
            try
            {
                stammdaten = XtfStammdatenElementReader.Read(quelle);
            }
            catch (Exception ex)
            {
                fehler.Add($"{name}: Stammdaten nicht lesbar ({ex.Message})");
                continue;
            }

            if (elemente.Count == 0 && stammdaten.Count == 0)
            {
                bericht.AppendLine($"{name}: weder Kanalschaeden noch Stammdaten — uebersprungen.");
                continue;
            }

            var basis = XtfRevisionPlanBuilder.Build(request.Projekt.Data, elemente, name);
            // Die Modellfassung der Datei entscheidet ueber die gueltige Schreibweise
            // mancher Werte (2015 "Regenabwasser" gegen 2020 "Niederschlagsabwasser").
            var stamm = XtfStammdatenPlanBuilder.Build(
                request.Projekt.Data,
                stammdaten,
                XtfStammdatenElementReader.ReadModelName(quelle));
            var plan = stamm.Positionen.Count == 0
                ? basis
                : basis with { Positionen = basis.Positionen.Concat(stamm.Positionen).ToList() };
            bericht.AppendLine(
                $"{name}: {plan.AnzahlGeaendert} geaendert, {plan.AnzahlNeu} neu, " +
                $"{plan.AnzahlEntfernt} entfernt, {plan.AnzahlUnveraendert} unveraendert.");

            foreach (var warnung in plan.Warnungen)
                bericht.AppendLine($"    offen: {warnung}");

            // Hinweise halten den Export nicht auf, muessen aber sichtbar bleiben.
            foreach (var hinweis in stamm.Hinweise)
                bericht.AppendLine($"    Hinweis: {hinweis}");

            if (request.NurPruefen)
                continue;

            if (plan.BrauchtEntscheidung)
            {
                fehler.Add($"{name}: offene Faelle — es wurde nichts geschrieben.");
                continue;
            }

            if (plan.OhneAenderung)
            {
                bericht.AppendLine($"    keine Aenderung — keine Revision noetig.");
                continue;
            }

            var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, Path.Combine(ausgabe, name));
            if (!ergebnis.Ok)
            {
                fehler.Add($"{name}: {ergebnis.Fehler}");
                continue;
            }

            geschrieben.Add(ergebnis.Zielpfad!);
            bericht.AppendLine($"    geschrieben: {ergebnis.Zielpfad}");
        }

        if (fehler.Count > 0)
        {
            bericht.AppendLine();
            foreach (var f in fehler)
                bericht.AppendLine($"FEHLER: {f}");
        }

        return new XtfRevisionExportResult(
            fehler.Count == 0,
            bericht.ToString().TrimEnd(),
            fehler.Count == 0 ? null : string.Join("\n", fehler),
            geschrieben);
    }

    /// <summary>
    /// Sucht die XTF-Quellen unterhalb des Projektordners. Liegt die Projektdatei in
    /// 'Projektdateien', gilt der Ordner darueber als Projektwurzel.
    /// </summary>
    internal static List<string> FindeQuellen(string? projektPfad)
    {
        var treffer = new List<string>();
        if (string.IsNullOrWhiteSpace(projektPfad))
            return treffer;

        var wurzel = Path.GetDirectoryName(Path.GetFullPath(projektPfad));
        if (string.IsNullOrWhiteSpace(wurzel))
            return treffer;

        if (string.Equals(Path.GetFileName(wurzel), "Projektdateien", StringComparison.OrdinalIgnoreCase))
            wurzel = Path.GetDirectoryName(wurzel) ?? wurzel;

        foreach (var relativ in QuellOrdner)
        {
            var ordner = Path.Combine(wurzel, relativ);
            if (!Directory.Exists(ordner))
                continue;

            foreach (var datei in Directory.GetFiles(ordner, "*.xtf", SearchOption.TopDirectoryOnly))
            {
                // Gleicher Dateiname in beiden Ablagen: nur einmal verarbeiten.
                if (!treffer.Any(t => string.Equals(Path.GetFileName(t), Path.GetFileName(datei), StringComparison.OrdinalIgnoreCase)))
                    treffer.Add(datei);
            }
        }

        return treffer;
    }

    private static XtfRevisionExportResult Fehler(string text)
        => new(false, text, text, Array.Empty<string>());
}

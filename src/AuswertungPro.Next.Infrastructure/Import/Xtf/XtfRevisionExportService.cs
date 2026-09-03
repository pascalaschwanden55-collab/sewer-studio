using System.Security.Cryptography;
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

        List<string> quellen;
        if (request.Quelldateien is { Count: > 0 })
        {
            var pruefung = PruefeExpliziteQuellen(request.Quelldateien);
            if (pruefung.Fehler is not null)
                return Fehler(pruefung.Fehler);

            quellen = pruefung.Quellen;
        }
        else
        {
            try
            {
                quellen = FindeQuellen(request.ProjektPfad);
            }
            catch (Exception ex)
            {
                return Fehler($"Die XTF-Quellen im Projekt konnten nicht gelesen werden: {ex.Message}");
            }
        }

        if (quellen.Count == 0)
        {
            return Fehler(
                "Im Projekt wurde keine XTF-Quelldatei gefunden. Gesucht wird unter " +
                "'Imports\\XTF' und 'Importdateien\\XTF'.",
                quelleFehlt: true);
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

            // Haltungen und Schaechte teilen sich EIN Organisationsbuch je Datei. Zwei
            // getrennte Buecher wuerden dieselbe Organisation doppelt anlegen oder
            // dieselbe Kennung zweimal vergeben.
            var buch = new XtfOrganisationsbuch(stammdaten);

            // Die Modellfassung der Datei entscheidet ueber die gueltige Schreibweise
            // mancher Werte (2015 "Regenabwasser" gegen 2020 "Niederschlagsabwasser").
            var stamm = XtfStammdatenPlanBuilder.Build(
                request.Projekt.Data,
                stammdaten,
                XtfStammdatenElementReader.ReadModelName(quelle),
                buch);
            var schacht = XtfSchachtPlanBuilder.Build(request.Projekt.SchaechteData, stammdaten, buch);

            var zusatz = stamm.Positionen.Concat(schacht.Positionen).ToList();
            var plan = zusatz.Count == 0 && buch.Neue.Count == 0
                ? basis
                : basis with
                {
                    Positionen = basis.Positionen.Concat(zusatz).ToList(),
                    NeueOrganisationen = buch.Neue
                };
            bericht.AppendLine(
                $"{name}: {plan.AnzahlGeaendert} geaendert, {plan.AnzahlNeu} neu, " +
                $"{plan.AnzahlEntfernt} entfernt, {plan.AnzahlUnveraendert} unveraendert.");

            foreach (var warnung in plan.Warnungen)
                bericht.AppendLine($"    offen: {warnung}");

            // Hinweise halten den Export nicht auf, muessen aber sichtbar bleiben.
            foreach (var hinweis in stamm.Hinweise.Concat(schacht.Hinweise))
                bericht.AppendLine($"    Hinweis: {hinweis}");

            if (plan.BrauchtEntscheidung)
            {
                fehler.Add(request.NurPruefen
                    ? $"{name}: offene Faelle — die Pruefung ist nicht bestanden."
                    : $"{name}: offene Faelle — es wurde nichts geschrieben.");
                continue;
            }

            if (request.NurPruefen)
                continue;

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
                var gleichnamig = treffer.FirstOrDefault(t =>
                    string.Equals(
                        Path.GetFileName(t),
                        Path.GetFileName(datei),
                        StringComparison.OrdinalIgnoreCase));
                if (gleichnamig is null)
                {
                    treffer.Add(datei);
                    continue;
                }

                // Alte und neue Projektablage duerfen dieselbe Importkopie enthalten.
                // Nur ein belegter Inhaltsvergleich erlaubt das Entdoppeln. Bei zwei
                // verschiedenen Quellen waere unklar, welche revidiert werden soll.
                if (!HabenGleichenInhalt(gleichnamig, datei))
                {
                    throw new InvalidDataException(
                        $"Zwei XTF-Projektquellen haben den gleichen Namen " +
                        $"'{Path.GetFileName(datei)}', aber unterschiedlichen Inhalt: " +
                        $"'{gleichnamig}' und '{datei}'.");
                }
            }
        }

        return treffer;
    }

    private static bool HabenGleichenInhalt(string ersterPfad, string zweiterPfad)
    {
        if (string.Equals(
                Path.GetFullPath(ersterPfad),
                Path.GetFullPath(zweiterPfad),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ersterInfo = new FileInfo(ersterPfad);
        var zweiterInfo = new FileInfo(zweiterPfad);
        if (ersterInfo.Length != zweiterInfo.Length)
            return false;

        using var ersterStream = File.OpenRead(ersterPfad);
        using var zweiterStream = File.OpenRead(zweiterPfad);
        var ersterHash = SHA256.HashData(ersterStream);
        var zweiterHash = SHA256.HashData(zweiterStream);
        return CryptographicOperations.FixedTimeEquals(ersterHash, zweiterHash);
    }

    /// <summary>
    /// Prueft bewusst gewaehlte Quellen vollstaendig vor dem ersten Export. Gleiche
    /// Pfade werden nur einmal gelesen. Zwei verschiedene Dateien mit demselben Namen
    /// werden abgelehnt, weil sie sonst dasselbe Ausgabeziel haetten.
    /// </summary>
    private static (List<string> Quellen, string? Fehler) PruefeExpliziteQuellen(
        IReadOnlyList<string> quellPfade)
    {
        var quellen = new List<string>();
        var bekanntePfade = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var namen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rohPfad in quellPfade)
        {
            if (string.IsNullOrWhiteSpace(rohPfad))
                return ([], "Eine gewaehlte XTF-Quelldatei hat keinen Pfad.");

            string pfad;
            try
            {
                pfad = Path.GetFullPath(rohPfad);
            }
            catch (Exception ex)
            {
                return ([], $"Der Pfad der XTF-Quelldatei ist ungueltig: {ex.Message}");
            }

            if (!string.Equals(Path.GetExtension(pfad), ".xtf", StringComparison.OrdinalIgnoreCase))
                return ([], $"Die Quelldatei '{pfad}' ist keine .xtf-Datei.");

            if (!File.Exists(pfad))
                return ([], $"Die XTF-Quelldatei wurde nicht gefunden: {pfad}");

            if (!bekanntePfade.Add(pfad))
                continue;

            var name = Path.GetFileName(pfad);
            if (namen.TryGetValue(name, out var vorhandenerPfad))
            {
                return ([],
                    $"Zwei gewaehlte XTF-Quellen heissen '{name}'. " +
                    $"Bitte waehle nur eine davon: '{vorhandenerPfad}' oder '{pfad}'.");
            }

            namen[name] = pfad;
            quellen.Add(pfad);
        }

        return (quellen, null);
    }

    private static XtfRevisionExportResult Fehler(string text, bool quelleFehlt = false)
        => new(false, text, text, Array.Empty<string>(), quelleFehlt);
}

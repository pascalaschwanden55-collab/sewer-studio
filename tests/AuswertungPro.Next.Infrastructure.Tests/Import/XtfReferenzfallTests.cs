using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Referenzfaelle fuer den XTF-Rueckweg (Wartbarkeitspaket AP08, 30.09.2026).
///
/// Jede synthetische Datei unter <c>tests/Fixtures/XtfReferenz</c> wird eingelesen und das
/// VOLLSTAENDIGE Ergebnis mit einem Schnappschuss verglichen: alle Projektfelder mit Wert und
/// Herkunft, Katasterkennungen, Befunde, Schachtprotokolle, Konflikte, Zaehler und Meldungen.
/// Der Schnappschuss wurde VOR dem Umbau des Parsers am unveraenderten Stand aufgenommen.
/// Ein Unterschied heisst: Der Umbau hat Verhalten veraendert.
///
/// Absichtlich neutralisiert sind nur Werte, die sich von Lauf zu Lauf aendern (Guids,
/// Zeitstempel, Programmversion, temporaerer Ordner). Sie gehoeren nicht zum Importergebnis.
/// </summary>
public sealed class XtfReferenzfallTests : IDisposable
{
    private readonly string _lauf = Path.Combine(Path.GetTempPath(), "xtf_referenz_" + Guid.NewGuid().ToString("N"));

    public XtfReferenzfallTests() => Directory.CreateDirectory(_lauf);

    public void Dispose()
    {
        try { Directory.Delete(_lauf, recursive: true); }
        catch (IOException) { /* Aufraeumen darf den Test nicht faellen. */ }
    }

    [Fact]
    public void Sia405_Referenz_in_ein_leeres_Projekt()
        => PruefeSchnappschuss("sia405-referenz", new Project(), "sia405-referenz.xtf");

    [Fact]
    public void Sia405_Referenz_zweimal_hintereinander()
        => PruefeSchnappschuss("sia405-referenz-zweimal", new Project(), "sia405-referenz.xtf", "sia405-referenz.xtf");

    [Fact]
    public void Sia405_Referenz_schuetzt_Handwerte_im_vorbelegten_Projekt()
        => PruefeSchnappschuss("sia405-referenz-vorbelegt", VorbelegtesProjekt(), "sia405-referenz.xtf");

    [Fact]
    public void Sia405_Eigentuemer_und_Verwaltungsrollen_je_Bezugsart()
        => PruefeSchnappschuss("sia405-bezuege", new Project(), "sia405-bezuege.xtf");

    [Fact]
    public void Sia405_gewinnt_gegen_VsaKek_in_derselben_Datei()
        => PruefeSchnappschuss("sia405-mit-vsakek", new Project(), "sia405-mit-vsakek.xtf");

    [Fact]
    public void VsaKek_Referenz_mit_Gegenbefahrung_Videozaehler_und_Schachtbegehung()
    {
        // Die Medien liegen dort, wo die Datei sie erwartet; sonst waere der aufgeloeste
        // Pfad ein blosser Rueckfallkandidat.
        foreach (var datei in new[] { "Fotos/foto_schaden2.jpg", "Fotos/foto_schaden3.jpg",
                     "Fotos/foto_schaden2_zweites.jpg", "Film/200-201.mpg", "Film/200-201_gegen.mp4" })
        {
            var ziel = Path.Combine(_lauf, datei);
            Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
            File.WriteAllBytes(ziel, [0x00]);
        }

        PruefeSchnappschuss("vsakek-referenz", new Project(), "vsakek-referenz.xtf");
    }

    /// <summary>
    /// Ein Projekt mit Handwerten und Werten anderer Herkunft, damit Feldschutz, Vorrang und
    /// Konfliktmeldungen der Uebernahme im Schnappschuss stehen.
    /// </summary>
    private static Project VorbelegtesProjekt()
    {
        var projekt = new Project();

        var haltung = new HaltungRecord();
        haltung.SetFieldValue(FieldKeys.HoldingName, "100-101", FieldSource.Manual, userEdited: true);
        haltung.SetFieldValue(FieldKeys.PipeMaterial, "PVC", FieldSource.Manual, userEdited: true);
        haltung.SetFieldValue(FieldKeys.Owner, "", FieldSource.Manual, userEdited: true);
        haltung.SetFieldValue(FieldKeys.NominalDiameterMm, "500", FieldSource.Pdf, userEdited: false);
        haltung.SetFieldValue(FieldKeys.InspectionYear, "06.10.2025", FieldSource.Xtf, userEdited: false);
        haltung.SetFieldValue(FieldKeys.ConditionClass, "3", FieldSource.Kataster, userEdited: false);
        haltung.SetFieldValue(FieldKeys.CadastreObjectId, "altesKataster01", FieldSource.Kataster, userEdited: false);
        projekt.Data.Add(haltung);

        var zweite = new HaltungRecord();
        zweite.SetFieldValue(FieldKeys.HoldingName, "101 - 102", FieldSource.Manual, userEdited: false);
        zweite.SetFieldValue(FieldKeys.Remarks, "Handnotiz", FieldSource.Manual, userEdited: true);
        projekt.Data.Add(zweite);

        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "100", FieldSource.Manual, userEdited: false);
        schacht.SetFieldValue("Funktion", "Einlaufschacht", FieldSource.Manual, userEdited: true);
        schacht.SetFieldValue(FieldKeys.ShaftDimension1Mm, "800", FieldSource.Spro, userEdited: false);
        projekt.SchaechteData.Add(schacht);

        return projekt;
    }

    private void PruefeSchnappschuss(string name, Project projekt, params string[] dateien)
    {
        var pfade = dateien.Select(datei =>
        {
            var ziel = Path.Combine(_lauf, datei);
            if (!File.Exists(ziel))
                File.Copy(TestRepoPaths.RepoFile("tests", "Fixtures", "XtfReferenz", datei), ziel);
            return ziel;
        }).ToArray();

        // Klartexte nur aus den eingebauten Titeln: sonst haengt der Schnappschuss davon ab, ob
        // auf dem Rechner ein WinCan-Katalog installiert ist (lokal ja, in der CI nein).
        using var _ = XtfPrimaryDamageFormatter.NurEingebauteTitelVerwenden();
        var stats = new LegacyXtfImportService().ImportXtfFiles(pfade, projekt);
        var ist = Schnappschuss(projekt, stats);

        var erwartetPfad = Path.Combine(TestRepoPaths.RepoRoot(), "tests", "Fixtures", "XtfReferenz", name + ".schnappschuss.json");
        if (!File.Exists(erwartetPfad))
        {
            File.WriteAllText(erwartetPfad, ist, new UTF8Encoding(false));
            Assert.Fail($"Schnappschuss neu angelegt, bitte pruefen und einchecken: {erwartetPfad}");
        }

        var erwartet = File.ReadAllText(erwartetPfad).Replace("\r\n", "\n", StringComparison.Ordinal);
        if (string.Equals(erwartet, ist, StringComparison.Ordinal))
            return;

        var istPfad = Path.Combine(Path.GetTempPath(), $"xtf-referenz-{name}.ist.json");
        File.WriteAllText(istPfad, ist, new UTF8Encoding(false));
        Assert.Fail($"Referenzfall {name} weicht ab ({ErsteAbweichung(erwartet, ist)}). Ist-Stand: {istPfad}");
    }

    private string Schnappschuss(Project projekt, ImportStats stats)
    {
        var wurzel = new JsonObject
        {
            ["projekt"] = JsonSerializer.SerializeToNode(projekt, JsonProjectRepository.SerializerOptions),
            ["statistik"] = JsonSerializer.SerializeToNode(stats, JsonProjectRepository.SerializerOptions)
        };

        var bereinigt = Bereinige(wurzel)!;
        return bereinigt.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static readonly HashSet<string> LaufabhaengigeNamen = new(StringComparer.Ordinal)
    {
        "Id", "CreatedAtUtc", "ModifiedAtUtc", "LastUpdatedUtc", "timestampUtc", "AppVersion"
    };

    /// <summary>
    /// Sortiert die Schluessel (die Reihenfolge eines Woerterbuchs ist kein Ergebnis) und
    /// ersetzt laufabhaengige Werte. Die Reihenfolge in Listen bleibt erhalten.
    /// </summary>
    private JsonNode? Bereinige(JsonNode? knoten)
    {
        switch (knoten)
        {
            case JsonObject objekt:
            {
                var neu = new JsonObject();
                foreach (var (schluessel, wert) in objekt.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (LaufabhaengigeNamen.Contains(schluessel) && wert is not null)
                        neu[schluessel] = "<laufabhaengig>";
                    else if (schluessel == "Source" && objekt.ContainsKey("UserEdited") && wert is JsonValue quelle
                             && quelle.TryGetValue<int>(out var zahl))
                        neu[schluessel] = ((FieldSource)zahl).ToString();
                    else
                        neu[schluessel] = Bereinige(wert);
                }
                return neu;
            }
            case JsonArray liste:
                return new JsonArray(liste.Select(Bereinige).ToArray());
            case JsonValue wert when wert.TryGetValue<string>(out var text):
                if (Guid.TryParseExact(text, "D", out _))
                    return "<guid>";
                if (text.Length >= 19 && text[4] == '-' && text[10] == 'T'
                    && DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out _))
                    return "<zeitpunkt>";
                return text.Replace(_lauf, "<lauf>", StringComparison.OrdinalIgnoreCase).Replace('\\', '/');
            default:
                return knoten?.DeepClone();
        }
    }

    private static string ErsteAbweichung(string erwartet, string ist)
    {
        var a = erwartet.Split('\n');
        var b = ist.Split('\n');
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var links = i < a.Length ? a[i] : "<Ende>";
            var rechts = i < b.Length ? b[i] : "<Ende>";
            if (!string.Equals(links, rechts, StringComparison.Ordinal))
                return $"Zeile {i + 1}: erwartet «{links.Trim()}», ist «{rechts.Trim()}»";
        }

        return "gleicher Text";
    }
}

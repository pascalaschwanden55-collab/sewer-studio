using System.IO.Compression;
using System.Text;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.SchachtPro;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Tests fuer den SchachtPro-Import (.spro = ZIP mit JSON, Android-App SchachtPro).
/// Das Test-Archiv wird zur Laufzeit als echtes ZIP erzeugt (kein Binaer-Fixture);
/// JSON-Feldnamen entsprechen exakt den Gson-DTOs der App (ProjectArchive.kt).
/// </summary>
public sealed partial class SchachtProImportServiceTests
{
    // ---------------------------------------------------------------
    // 1. Happy Path: 1 Projekt, 2 Protokolle (rund/oval), Anschluesse,
    //    Zustaende, Fotos, GPS.
    // ---------------------------------------------------------------

    [Fact]
    public void HappyPath_Importiert_Protokolle_Felder_DCodes_Und_Fotos()
    {
        using var temp = new TempDir();
        var archiv = ErzeugeArchivMitZweiProtokollen(temp);
        var project = new Project();
        var service = new SchachtProImportService();

        using var session = BeginStaging(temp);
        var result = service.ImportSchachtProArchive(archiv, project, Ctx(session));
        session.Publish();
        session.Accept();

        Assert.True(result.Ok, result.ErrorMessage);
        var stats = result.Value!;
        Assert.Equal(2, stats.Found);
        Assert.Equal(2, stats.Created);
        Assert.Equal(0, stats.Updated);
        Assert.Equal(0, stats.Errors);
        Assert.Equal(0, stats.Uncertain);

        Assert.Equal(2, project.SchaechteData.Count);

        // --- Schacht S-100 (rund) ---
        var s100 = project.SchaechteData.Single(r => r.GetFieldValue("Schachtnummer") == "S-100");
        Assert.Equal("S-100", s100.GetFieldValue("NR."));
        Assert.Equal("S-100", s100.GetFieldValue("Nr."));
        Assert.Equal("Kontrollschacht", s100.GetFieldValue("Funktion"));
        Assert.Equal("rund", s100.GetFieldValue("Schachtform"));
        Assert.Equal("1000", s100.GetFieldValue("Dimension"));
        Assert.Equal("1000", s100.GetFieldValue("Durchmesser"));
        Assert.Equal("2.50", s100.GetFieldValue("Schachttiefe"));
        Assert.Equal("Beton", s100.GetFieldValue("Material"));
        Assert.Equal("12.07.2026", s100.GetFieldValue("Ausführung Datum/Jahr"));
        // ASCII-Legacy-Alias (Konvention des Schacht-PDF-Imports)
        Assert.Equal("12.07.2026", s100.GetFieldValue("Ausfuehrung Datum/Jahr"));
        Assert.Equal("schoen_trocken", s100.GetFieldValue("Wetter"));
        Assert.Equal("Steigeisen", s100.GetFieldValue("Steighilfe"));
        // LV95, KEIN WGS84
        Assert.Equal("2683947.125", s100.GetFieldValue("Koordinate_East"));
        Assert.Equal("1192844.5", s100.GetFieldValue("Koordinate_North"));
        Assert.Contains("Nr. 1: Typ=Anschluss", s100.GetFieldValue("Anschlüsse"));
        Assert.Contains("DN=150", s100.GetFieldValue("Anschlüsse"));

        var entries100 = s100.Protocol!.Current.Entries;
        Assert.Equal(4, entries100.Count);
        // Bauteil-Ordnung: Schachtdeckel, Konus, Anschluss, Leiter/Steigeisen
        Assert.Equal("Schachtdeckel", entries100[0].Code);
        Assert.Equal("gerissen — DAB-B, K2", entries100[0].Beschreibung);
        Assert.Equal("Konus", entries100[1].Code);
        Assert.Equal("korrodiert — DAI-A, K2", entries100[1].Beschreibung);
        Assert.Equal("Anschluss", entries100[2].Code);
        Assert.Equal("Nr. 1: gerissen — DAB-B, K2", entries100[2].Beschreibung);
        Assert.Equal("Leiter/Steigeisen", entries100[3].Code);
        Assert.Equal("verrostet — DAI-B, K2", entries100[3].Beschreibung);
        Assert.All(entries100, e => Assert.Equal(Domain.Protocol.ProtocolEntrySource.Imported, e.Source));
        Assert.Equal("Arbeitskopie", s100.Protocol.Current.Comment);
        Assert.Contains("SchachtPro-Archiv", s100.Protocol.Original.Comment);
        Assert.Equal(
            "Schachtdeckel: gerissen; Konus: korrodiert; Anschluss: Nr. 1: gerissen; Leiter/Steigeisen: verrostet",
            s100.GetFieldValue("Primäre Schäden"));
        // ASCII-Legacy-Alias (Konvention des Schacht-PDF-Imports)
        Assert.Equal(
            s100.GetFieldValue("Primäre Schäden"),
            s100.GetFieldValue("Primaere Schaeden"));

        // Foto kopiert + relativ verlinkt
        var fotoRel100 = s100.GetFieldValue("Fotos");
        Assert.Equal("Fotos/Schächte/S-100/0_0.jpg", fotoRel100);
        Assert.True(File.Exists(Path.Combine(temp.ProjectRoot, "Fotos", "Schächte", "S-100", "0_0.jpg")));

        // --- Schacht S-200 (oval: Dimension aus Laenge x Breite) ---
        var s200 = project.SchaechteData.Single(r => r.GetFieldValue("Schachtnummer") == "S-200");
        Assert.Equal("oval", s200.GetFieldValue("Schachtform"));
        Assert.Equal("800 x 600", s200.GetFieldValue("Dimension"));
        Assert.Equal("800 x 600", s200.GetFieldValue("Durchmesser"));
        Assert.Equal("800", s200.GetFieldValue("Schachtlänge"));
        Assert.Equal("600", s200.GetFieldValue("Schachtbreite"));

        var entries200 = s200.Protocol!.Current.Entries;
        Assert.Equal(2, entries200.Count);
        Assert.Equal("Bankett", entries200[0].Code);
        Assert.Equal("Ablagerungen — DAK-A, K3", entries200[0].Beschreibung);
        Assert.Equal("Tauchbogen", entries200[1].Code);
        Assert.Equal("fehlt — DAP, K2", entries200[1].Beschreibung);

        var fotos200 = s200.GetFieldValue("Fotos").Split(';');
        Assert.Equal(2, fotos200.Length);
        Assert.All(fotos200, p =>
        {
            Assert.StartsWith("Fotos/Schächte/S-200/", p);
            Assert.True(File.Exists(Path.Combine(temp.ProjectRoot, p.Replace('/', Path.DirectorySeparatorChar))));
        });

        // Auftraggeber des SchachtPro-Projekts landet in den Projekt-Metadaten.
        Assert.Equal("Gemeinde Uri", project.Metadata["Auftraggeber"]);
    }

    // ---------------------------------------------------------------
    // 2. Norm-Mapping (Stufe B)
    // ---------------------------------------------------------------

    [Theory]
    // Universelle Labels (App-Strings aus ZustandPage.kt)
    [InlineData("Schachtdeckel", "überdeckt", "überdeckt — DXX, Z")]
    [InlineData("Schachtdeckel", "gerissen", "gerissen — DAB-B, K2")]
    [InlineData("Schachtdeckel", "Haarrisse", "Haarrisse — DAB-A, K3")]
    [InlineData("Schachtdeckel", "ausgebrochen", "ausgebrochen — DAC-B, K1")]
    [InlineData("Deckelrahmen", "lose", "lose — DAH, K2")]
    [InlineData("Konus", "korrodiert", "korrodiert — DAI-A, K2")]
    [InlineData("Konus", "Fugen mangelhaft verputzt", "Fugen mangelhaft verputzt — DAD-A, K2")]
    [InlineData("Deckelrahmen", "mangelhaft unterbetoniert", "mangelhaft unterbetoniert — DAD-B, K2")]
    [InlineData("Bankett", "mangelhaft ausgebildet", "mangelhaft ausgebildet — DAP, K3")]
    [InlineData("Schachtdeckel", "kann nicht geöffnet werden", "kann nicht geöffnet werden — DXX, Z")]
    [InlineData("Bankett", "Ablagerungen", "Ablagerungen — DAK-A, K3")]
    [InlineData("Schachtrohr", "Wurzeln", "Wurzeln — DAL-A, K2")]
    [InlineData("Schachtrohr", "Infiltration", "Infiltration — DAM-A, K2")]
    [InlineData("Schachtrohr", "Infiltration Wasser fliesst/spritzt", "Infiltration Wasser fliesst/spritzt — DAM-C, K1")]
    [InlineData("Schachthals", "Verkalkungen", "Verkalkungen — DAK-B, K3")]
    // Steighilfe (Leiter/Steigeisen)
    [InlineData("Leiter/Steigeisen", "fehlt", "fehlt — DAN-A, K1")]
    [InlineData("Leiter/Steigeisen", "zu kurz", "zu kurz — DAN-C, K2")]
    [InlineData("Leiter/Steigeisen", "verrostet", "verrostet — DAI-B, K2")]
    [InlineData("Leiter/Steigeisen", "defekt", "defekt — DAN-B, K1")]
    [InlineData("Leiter/Steigeisen", "Befestigung mangelhaft", "Befestigung mangelhaft — DAN-D, K2")]
    [InlineData("Leiter/Steigeisen", "Sprosse(n) gebrochen", "Sprosse(n) gebrochen — DAN-B, K1")]
    // Tauchbogen
    [InlineData("Tauchbogen", "fehlt", "fehlt — DAP, K2")]
    [InlineData("Tauchbogen", "defekt", "defekt — DAC, K2")]
    [InlineData("Tauchbogen", "kann nicht entfernt werden", "kann nicht entfernt werden — DAO, K3")]
    // Anschluss-Labels (universelle Tabelle, App-Schreibweise "infiltration" klein)
    [InlineData("Anschluss", "gerissen", "gerissen — DAB-B, K2")]
    [InlineData("Anschluss", "ausgebrochen", "ausgebrochen — DAC-B, K1")]
    [InlineData("Anschluss", "Wurzeln", "Wurzeln — DAL-A, K2")]
    [InlineData("Anschluss", "infiltration", "infiltration — DAM-A, K2")]
    public void Mapping_Label_Wird_Zu_Erwartetem_DCode(string sektion, string label, string erwartet)
    {
        var resolved = SchachtProZustandMapper.Resolve(sektion, label);

        var mapping = Assert.IsType<SchachtProZustandMapper.DamageMapping>(resolved);
        Assert.Equal(erwartet, mapping.FormatBeschreibung(label));
    }

    [Theory]
    // Bekannte Labels OHNE Codierung (Inventar-Info / Mängelfrei)
    [InlineData("Schacht", "Mängelfrei")]
    [InlineData("Schachtdeckel", "verschraubt")]
    [InlineData("Tauchbogen", "vorhanden")]
    [InlineData("Tauchbogen", "nicht notwendig")]
    [InlineData("Leiter/Steigeisen", "leiter")]
    [InlineData("Leiter/Steigeisen", "steigeisen")]
    public void Mapping_InventarLabels_Erzeugen_Keine_Codierung(string sektion, string label)
        => Assert.IsType<SchachtProZustandMapper.NoCoding>(SchachtProZustandMapper.Resolve(sektion, label));

    [Fact]
    public void Mapping_Unbekanntes_Label_Wird_Nicht_Geraten()
        => Assert.Null(SchachtProZustandMapper.Resolve("Konus", "gibtsNicht"));

    [Fact]
    public void Unbekanntes_Label_Erzeugt_Uncertain_Und_Klartext_Eintrag()
    {
        using var temp = new TempDir();
        var archiv = ErzeugeArchiv(temp,
            manifest: Manifest("P1", "Projekt A"),
            projekte: new()
            {
                ["P1"] = Snapshot("P1", "Projekt A",
                    Protokoll("""{"schachtNr":"S-1","schachtZustand":{"Mängelfrei":true},"anschluesse":[{"nr":2,"typ":"Anschluss","zustand":{"mangelhaft eingebunden":true}}]}"""))
            });
        var project = new Project();

        var result = new SchachtProImportService().ImportSchachtProArchive(archiv, project);

        Assert.True(result.Ok, result.ErrorMessage);
        var stats = result.Value!;
        Assert.Equal(1, stats.Found);
        Assert.Equal(1, stats.Uncertain);
        Assert.Equal(0, stats.Errors);
        Assert.Contains(stats.Messages, m => m.Contains("mangelhaft eingebunden"));

        var record = project.SchaechteData.Single();
        var entry = Assert.Single(record.Protocol!.Current.Entries);
        Assert.Equal("Anschluss", entry.Code);
        // Klartext ohne Norm-Anhaengsel
        Assert.Equal("Nr. 2: mangelhaft eingebunden", entry.Beschreibung);
    }

    [Fact]
    public void Maengelfrei_Allein_Erzeugt_Keinen_Schadenseintrag()
    {
        using var temp = new TempDir();
        var archiv = ErzeugeArchiv(temp,
            manifest: Manifest("P1", "Projekt A"),
            projekte: new()
            {
                ["P1"] = Snapshot("P1", "Projekt A",
                    Protokoll("""{"schachtNr":"S-1","schachtZustand":{"Mängelfrei":true},"deckelZustand":{"verschraubt":true},"tauchbogen":{"vorhanden":true}}"""))
            });
        var project = new Project();

        var result = new SchachtProImportService().ImportSchachtProArchive(archiv, project);

        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal(0, result.Value!.Uncertain);
        var record = project.SchaechteData.Single();
        Assert.Null(record.Protocol);
        Assert.Equal("Maengelfrei", record.GetFieldValue("Primäre Schäden"));
    }

    // ---------------------------------------------------------------
    // 3. Versions-Guard
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(2, 21)]
    [InlineData(1, 22)]
    public void Zu_Neue_Archivversion_Wird_Als_Unsupported_Abgewiesen(int formatVersion, int dbSchemaVersion)
    {
        using var temp = new TempDir();
        var archiv = ErzeugeArchiv(temp,
            manifest: $$"""{"formatVersion":{{formatVersion}},"dbSchemaVersion":{{dbSchemaVersion}},"appVersionName":"4.5.0","appVersionCode":45,"exportedAtMillis":1752174000000,"projectCount":0,"projects":[]}""",
            projekte: new());

        var result = new SchachtProImportService().ImportSchachtProArchive(archiv, new Project());

        Assert.False(result.Ok);
        Assert.Equal("UNSUPPORTED_VERSION", result.ErrorCode);
        Assert.Contains("neuer als unterstuetzt", result.ErrorMessage);
    }

    // ---------------------------------------------------------------
    // 4. Fehlerisolierung pro Protokoll / Projekt
    // ---------------------------------------------------------------

    [Fact]
    public void Defektes_Protokoll_Bricht_Import_Nicht_Ab()
    {
        using var temp = new TempDir();
        // Zweites Protokoll: schachtNr als Zahl statt String -> Deserialisierungsfehler
        var archiv = ErzeugeArchiv(temp,
            manifest: Manifest("P1", "Projekt A"),
            projekte: new()
            {
                ["P1"] = Snapshot("P1", "Projekt A",
                    Protokoll("""{"schachtNr":"S-1","schachtform":"rund"}""") + ","
                    + """{"schachtNr":123,"schachtform":"oval"}""")
            });
        var project = new Project();

        var result = new SchachtProImportService().ImportSchachtProArchive(archiv, project);

        Assert.True(result.Ok, result.ErrorMessage);
        var stats = result.Value!;
        Assert.Equal(1, stats.Found);
        Assert.Equal(1, stats.Created);
        Assert.Equal(1, stats.Errors);
        Assert.Contains(stats.Messages, m => m.Contains("beschaedigt"));
        Assert.Single(project.SchaechteData);
        Assert.Equal("S-1", project.SchaechteData[0].GetFieldValue("Schachtnummer"));
    }

    [Fact]
    public void Defektes_ProjektJson_Bricht_Andere_Projekte_Nicht_Ab()
    {
        using var temp = new TempDir();
        var archiv = ErzeugeArchiv(temp,
            manifest: Manifest(("P1", "Kaputt"), ("P2", "Heil")),
            projekte: new()
            {
                ["P1"] = "{ das ist kein JSON",
                ["P2"] = Snapshot("P2", "Heil", Protokoll("""{"schachtNr":"S-9","schachtform":"rund"}"""))
            });
        var project = new Project();

        var result = new SchachtProImportService().ImportSchachtProArchive(archiv, project);

        Assert.True(result.Ok, result.ErrorMessage);
        var stats = result.Value!;
        Assert.Equal(1, stats.Errors);
        Assert.Equal(1, stats.Found);
        Assert.Equal("S-9", project.SchaechteData.Single().GetFieldValue("Schachtnummer"));
    }

    // ---------------------------------------------------------------
    // 5. Zip-Slip
    // ---------------------------------------------------------------

    [Fact]
    public void ZipSlip_Eintrag_Wird_Abgewiesen()
    {
        using var temp = new TempDir();
        var archivPfad = Path.Combine(temp.Root, "boese.spro");
        using (var fs = File.Create(archivPfad))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            SchreibeTextEintrag(zip, "manifest.json", Manifest("P1", "Projekt A"));
            SchreibeTextEintrag(zip, "../evil.txt", "boese");
        }

        var result = new SchachtProImportService().ImportSchachtProArchive(archivPfad, new Project());

        Assert.False(result.Ok);
        Assert.Equal("UNSAFE_ENTRY", result.ErrorCode);
        Assert.Contains("evil.txt", result.ErrorMessage);
        Assert.False(File.Exists(Path.Combine(temp.Root, "evil.txt")));
    }

    // ---------------------------------------------------------------
    // 6. Idempotenz: zweiter Import aktualisiert statt Duplikate
    // ---------------------------------------------------------------

    [Fact]
    public void Zweiter_Import_Aktualisiert_Statt_Duplikate_Zu_Erzeugen()
    {
        using var temp = new TempDir();
        var archiv = ErzeugeArchivMitZweiProtokollen(temp);
        var project = new Project();
        var service = new SchachtProImportService();

        using (var session1 = BeginStaging(temp))
        {
            var first = service.ImportSchachtProArchive(archiv, project, Ctx(session1));
            session1.Publish();
            session1.Accept();
            Assert.True(first.Ok, first.ErrorMessage);
            Assert.Equal(2, first.Value!.Created);
        }

        using (var session2 = BeginStaging(temp))
        {
            var second = service.ImportSchachtProArchive(archiv, project, Ctx(session2));
            session2.Publish();
            session2.Accept();
            Assert.True(second.Ok, second.ErrorMessage);
            var stats = second.Value!;
            Assert.Equal(2, stats.Found);
            Assert.Equal(0, stats.Created);
            Assert.Equal(2, stats.Updated);
        }

        Assert.Equal(2, project.SchaechteData.Count);
        var s100 = project.SchaechteData.Single(r => r.GetFieldValue("Schachtnummer") == "S-100");
        Assert.Equal("Fotos/Schächte/S-100/0_0.jpg", s100.GetFieldValue("Fotos"));
        Assert.True(File.Exists(Path.Combine(temp.ProjectRoot, "Fotos", "Schächte", "S-100", "0_0.jpg")));
        Assert.Equal(4, s100.Protocol!.Current.Entries.Count);
    }

    // ---------------------------------------------------------------
    // Bonus: LITE-Modus (nur SchachtNr, Datum, Bemerkung, GPS, Fotos)
    // ---------------------------------------------------------------

    [Fact]
    public void Lite_Projekt_Importiert_Nur_Basisdaten()
    {
        using var temp = new TempDir();
        var archiv = ErzeugeArchiv(temp,
            manifest: Manifest("P1", "Lite Projekt"),
            projekte: new()
            {
                ["P1"] = Snapshot("P1", "Lite Projekt",
                    Protokoll("""{"schachtNr":"L-1","datum":"01.07.2026","bemerkungen":"nur Foto","schachtform":"rund","deckelZustand":{"gerissen":true},"lv95East":2700000.0,"lv95North":1200000.0}"""),
                    mode: "LITE")
            });
        var project = new Project();

        var result = new SchachtProImportService().ImportSchachtProArchive(archiv, project);

        Assert.True(result.Ok, result.ErrorMessage);
        var record = project.SchaechteData.Single();
        Assert.Equal("L-1", record.GetFieldValue("Schachtnummer"));
        Assert.Equal("01.07.2026", record.GetFieldValue("Ausführung Datum/Jahr"));
        Assert.Equal("nur Foto", record.GetFieldValue("Bemerkungen"));
        Assert.Equal("2700000", record.GetFieldValue("Koordinate_East"));
        // Keine PRO-Felder und keine Zustands-Eintraege im LITE-Modus
        Assert.Equal("", record.GetFieldValue("Schachtform"));
        Assert.Null(record.Protocol);
    }

    [Fact]
    public void Protokoll_Ohne_Schachtnummer_Wird_Als_Unklar_Uebersprungen()
    {
        using var temp = new TempDir();
        var archiv = ErzeugeArchiv(temp,
            manifest: Manifest("P1", "Projekt A"),
            projekte: new()
            {
                ["P1"] = Snapshot("P1", "Projekt A", Protokoll("""{"schachtform":"rund"}"""))
            });
        var project = new Project();

        var result = new SchachtProImportService().ImportSchachtProArchive(archiv, project);

        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal(1, result.Value!.Uncertain);
        Assert.Empty(project.SchaechteData);
    }

    // ---------------------------------------------------------------
    // 7. Re-Import: manuelle Arbeit am Protokoll bleibt bestehen
    // ---------------------------------------------------------------

    [Fact]
    public void ReImport_Erhalt_Manuellen_Eintrag_Und_Manuellen_Loeschung()
    {
        using var temp = new TempDir();
        var archiv = ErzeugeArchivMitZweiProtokollen(temp);
        var project = new Project();
        var service = new SchachtProImportService();

        Assert.True(service.ImportSchachtProArchive(archiv, project).Ok);
        var s100 = project.SchaechteData.Single(r => r.GetFieldValue("Schachtnummer") == "S-100");
        Assert.Equal(4, s100.Protocol!.Current.Entries.Count);

        // Benutzer: fuegt manuellen Eintrag hinzu und loescht den Anschluss-Importeintrag.
        var manuellerEintrag = new Domain.Protocol.ProtocolEntry
        {
            Code = "Konus",
            Beschreibung = "Manueller Nachtrag vor Ort",
            Source = Domain.Protocol.ProtocolEntrySource.Manual
        };
        s100.Protocol.Current.Entries.Add(manuellerEintrag);
        s100.Protocol.Current.Entries.RemoveAll(e => e.Code == "Anschluss");

        // Identischer Re-Import: die Arbeitskopie entspricht bereits dem Merge-Ergebnis
        // (3 Import + 1 manuell, Anschluss entfernt) -> Dokument bleibt unangetastet.
        Assert.True(service.ImportSchachtProArchive(archiv, project).Ok);
        var current = s100.Protocol.Current.Entries;
        Assert.Equal(4, current.Count);
        Assert.DoesNotContain(current, e => e.Code == "Anschluss");
        Assert.Contains(current, e => e.Beschreibung == "Manueller Nachtrag vor Ort"
                                    && e.Source == Domain.Protocol.ProtocolEntrySource.Manual);
        Assert.Empty(s100.Protocol.History);

        // Re-Import mit GEAENDERTEM Archiv (neuer Schaden "Haarrisse" am Deckel):
        // Arbeitskopie wird aktualisiert, die bisherige wandert in die History.
        var protokollMitHaarrisse = """
            {
              "schachtNr":"S-100",
              "datum":"13.07.2026",
              "schachtform":"rund",
              "deckelZustand":{"gerissen":true,"Haarrisse":true},
              "konusZustand":{"korrodiert":true},
              "leiterSteigeisen":{"steigeisen":true,"verrostet":true},
              "anschluesse":[{"nr":1,"typ":"Anschluss","dn":"150","zustand":{"gerissen":true}}]
            }
            """;
        var archivNeu = ErzeugeArchiv(temp,
            manifest: Manifest("XP1", "Uri 2026"),
            projekte: new()
            {
                ["XP1"] = Snapshot("XP1", "Uri 2026", Protokoll(protokollMitHaarrisse))
            });

        Assert.True(service.ImportSchachtProArchive(archivNeu, project).Ok);

        current = s100.Protocol.Current.Entries;
        // 4 Import-Eintraege (gerissen+Haarrisse am Deckel, Konus, Leiter/Steigeisen;
        // Anschluss weiterhin unterdrueckt) + 1 manueller Eintrag.
        Assert.Equal(5, current.Count);
        Assert.DoesNotContain(current, e => e.Code == "Anschluss");
        Assert.Contains(current, e => e.Beschreibung == "Haarrisse — DAB-A, K3");
        Assert.Contains(current, e => e.Beschreibung == "Manueller Nachtrag vor Ort");

        // Original = vollstaendiger neuer Import-Snapshot (5 Eintraege inkl. Anschluss)
        Assert.Equal(5, s100.Protocol.Original.Entries.Count);
        Assert.Contains(s100.Protocol.Original.Entries, e => e.Code == "Anschluss");

        // Bisherige Arbeitskopie (mit manuellem Eintrag, bereits ohne den vom
        // Benutzer geloeschten Anschluss-Eintrag) liegt in der History.
        // Der Anschluss-Schaden selbst ist im neuen Original weiterhin dokumentiert.
        var history = Assert.Single(s100.Protocol.History);
        Assert.Contains("Re-Import", history.Comment);
        Assert.Contains(history.Entries, e => e.Beschreibung == "Manueller Nachtrag vor Ort");
        Assert.DoesNotContain(history.Entries, e => e.Code == "Anschluss");

        // Weiterer identischer Import: keine erneute History-Revision
        Assert.True(service.ImportSchachtProArchive(archivNeu, project).Ok);
        Assert.Single(s100.Protocol.History);
        Assert.Equal(5, s100.Protocol.Current.Entries.Count);
    }

    [Fact]
    public void Identischer_ReImport_Laesst_Protokoll_Unveraendert()
    {
        using var temp = new TempDir();
        var archiv = ErzeugeArchivMitZweiProtokollen(temp);
        var project = new Project();
        var service = new SchachtProImportService();

        Assert.True(service.ImportSchachtProArchive(archiv, project).Ok);
        var s100 = project.SchaechteData.Single(r => r.GetFieldValue("Schachtnummer") == "S-100");
        var protokollNachErstemImport = s100.Protocol;
        var entryIds = s100.Protocol!.Current.Entries.Select(e => e.EntryId).ToList();

        Assert.True(service.ImportSchachtProArchive(archiv, project).Ok);

        Assert.Same(protokollNachErstemImport, s100.Protocol);
        Assert.Empty(s100.Protocol.History);
        Assert.Equal(entryIds, s100.Protocol.Current.Entries.Select(e => e.EntryId).ToList());
    }

    [Fact]
    public void ReImport_Erhalt_Benutzereditierten_Importeintrag()
    {
        using var temp = new TempDir();
        var archiv = ErzeugeArchivMitZweiProtokollen(temp);
        var project = new Project();
        var service = new SchachtProImportService();

        Assert.True(service.ImportSchachtProArchive(archiv, project).Ok);
        var s100 = project.SchaechteData.Single(r => r.GetFieldValue("Schachtnummer") == "S-100");

        // Benutzer korrigiert den Text eines importierten Eintrags.
        var eintrag = s100.Protocol!.Current.Entries.Single(e => e.Code == "Schachtdeckel");
        eintrag.Beschreibung = "gerissen, gross — DAB-B, K2 (vor Ort korrigiert)";

        Assert.True(service.ImportSchachtProArchive(archiv, project).Ok);

        // Die korrigierte Fassung bleibt bestehen (zusaetzlich zur frischen Import-Fassung:
        // inhaltlich geaenderte Eintraege werden als Benutzerarbeit uebernommen).
        Assert.Contains(s100.Protocol.Current.Entries,
            e => e.Beschreibung == "gerissen, gross — DAB-B, K2 (vor Ort korrigiert)");
        Assert.Single(s100.Protocol.History);
    }

    [Fact]
    public void Erneuter_Import_ueberschreibt_eine_Handkorrektur_nicht()
    {
        // Pascals Regel: was von Hand geaendert wurde, bleibt - auch wenn dieselbe
        // Datei versehentlich ein zweites Mal importiert wird.
        using var temp = new TempDir();
        var archiv = ErzeugeArchivMitZweiProtokollen(temp);
        var project = new Project();
        var service = new SchachtProImportService();

        using (var erste = BeginStaging(temp))
        {
            service.ImportSchachtProArchive(archiv, project, Ctx(erste));
            erste.Publish();
            erste.Accept();
        }

        var schacht = project.SchaechteData.Single(r => r.GetFieldValue("Schachtnummer") == "S-100");
        Assert.Equal("Kontrollschacht", schacht.GetFieldValue("Funktion"));

        // So schreibt die Schachtseite eine Handeingabe.
        schacht.SetFieldValue("Funktion", "Schlammsammler", FieldSource.Manual, userEdited: true);

        using (var zweite = BeginStaging(temp))
        {
            service.ImportSchachtProArchive(archiv, project, Ctx(zweite));
            zweite.Publish();
            zweite.Accept();
        }

        var danach = project.SchaechteData.Single(r => r.GetFieldValue("Schachtnummer") == "S-100");
        Assert.Equal("Schlammsammler", danach.GetFieldValue("Funktion"));
        Assert.True(danach.IsUserEdited("Funktion"));
    }

    [Fact]
    public void Der_Bericht_nennt_die_wegen_Handkorrektur_uebersprungenen_Felder()
    {
        // Ohne diese Zeile wundert man sich, warum eine Korrektur aus SchachtPro
        // nicht ankommt - der Wert wird still nicht uebernommen.
        using var temp = new TempDir();
        var archiv = ErzeugeArchivMitZweiProtokollen(temp);
        var project = new Project();
        var service = new SchachtProImportService();

        using (var erste = BeginStaging(temp))
        {
            service.ImportSchachtProArchive(archiv, project, Ctx(erste));
            erste.Publish();
            erste.Accept();
        }

        project.SchaechteData
            .Single(r => r.GetFieldValue("Schachtnummer") == "S-100")
            .SetFieldValue("Funktion", "Schlammsammler", FieldSource.Manual, userEdited: true);

        using var zweite = BeginStaging(temp);
        var result = service.ImportSchachtProArchive(archiv, project, Ctx(zweite));
        zweite.Publish();
        zweite.Accept();

        Assert.True(result.Ok, result.ErrorMessage);
        var meldung = result.Value!.Messages.FirstOrDefault(m => m.Contains("S-100", StringComparison.Ordinal)
                                                                && m.Contains("Funktion", StringComparison.Ordinal));
        Assert.False(string.IsNullOrEmpty(meldung), "Keine Meldung zu den nicht uebernommenen Feldern gefunden.");
        Assert.Contains("von Hand", meldung!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Importierte_Werte_tragen_die_Herkunft_SchachtPro_und_keine_Handmarkierung()
    {
        // Frueher wurde jeder Archivwert als "Manual" gekennzeichnet, obwohl nichts
        // von Hand kam. Damit war spaeter nicht mehr unterscheidbar, was der Mensch
        // gesetzt hat und was das Archiv.
        using var temp = new TempDir();
        var archiv = ErzeugeArchivMitZweiProtokollen(temp);
        var project = new Project();

        using var session = BeginStaging(temp);
        new SchachtProImportService().ImportSchachtProArchive(archiv, project, Ctx(session));
        session.Publish();
        session.Accept();

        var schacht = project.SchaechteData.Single(r => r.GetFieldValue("Schachtnummer") == "S-100");
        Assert.Equal(FieldSource.Spro, schacht.FieldMeta["Funktion"].Source);
        Assert.Equal(FieldSource.Spro, schacht.FieldMeta["Schachtnummer"].Source);
        Assert.False(schacht.IsUserEdited("Funktion"));
    }
}

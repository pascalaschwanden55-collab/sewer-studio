using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfImportTests
{
    [Fact]
    public void Import_ArchiviertAusserhalbDesProgrammordners_UndMigriertAltbestand()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xtf-archive-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(root, "source");
        var archiveDir = Path.Combine(root, "localappdata", "xtf_imports");
        var legacyDir = Path.Combine(root, "bin", "Rohdaten", "xtf_imports");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(legacyDir);
        var xtf = Path.Combine(sourceDir, "test.xml");
        File.WriteAllText(xtf, "<root />");
        File.WriteAllText(Path.Combine(legacyDir, "alt.xtf"), "alt");

        try
        {
            var service = new LegacyXtfImportService(archiveDir, legacyDir);
            service.ImportXtfFiles(new[] { xtf }, new Project());

            Assert.True(File.Exists(Path.Combine(archiveDir, "test.xml")));
            Assert.True(File.Exists(Path.Combine(archiveDir, "alt.xtf")));
            Assert.False(File.Exists(Path.Combine(legacyDir, "alt.xtf")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Vorschau_SchreibtUndMigriertKeinRohdatenarchiv()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xtf-preview-archive-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(root, "source");
        var archiveDir = Path.Combine(root, "archive");
        var legacyDir = Path.Combine(root, "legacy");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(legacyDir);
        var source = Path.Combine(sourceDir, "preview.xtf");
        var legacyFile = Path.Combine(legacyDir, "alt.xtf");
        File.WriteAllText(source, "<root />");
        File.WriteAllText(legacyFile, "alt");

        try
        {
            var context = new ImportRunContext(
                CancellationToken.None,
                progress: null,
                log: new ImportRunLog(),
                dryRun: true);
            var service = new LegacyXtfImportService(archiveDir, legacyDir);

            service.ImportXtfFiles([source], new Project(), context);

            Assert.False(Directory.Exists(archiveDir));
            Assert.True(File.Exists(source));
            Assert.True(File.Exists(legacyFile));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Import_ArchivFehler_VerhindertParsingNicht()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xtf-archive-error-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "test.xtf");
        var archiveAsFile = Path.Combine(root, "kein-ordner");
        File.WriteAllText(source, "<root />");
        File.WriteAllText(archiveAsFile, "blockiert");

        try
        {
            var service = new LegacyXtfImportService(archiveAsFile);
            var stats = service.ImportXtfFiles(new[] { source }, new Project());

            Assert.Equal(0, stats.Errors);
            Assert.Contains(stats.Messages, m => m.Level == "Warn" && m.Context == "XTF-ARCHIV");
            Assert.Contains(stats.Messages, m => m.Message.Contains("Unbekanntes Schema", StringComparison.Ordinal));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void VsaKekImport_FindetFotoUndVideo_WennXtfInDokumenteUnterordner()
    {
        // Reales IKAS-Layout: XTF in <Export>\Dokumente\, Foto/Film aber im Export-Root eine Ebene hoeher.
        // Regression: frueher wurde nur <Dokumente>\Foto / <Dokumente>\Film gesucht -> alles "nicht gefunden".
        var dir = Path.Combine(Path.GetTempPath(), $"vsakek-sub-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "Dokumente"));
        Directory.CreateDirectory(Path.Combine(dir, "Foto"));
        Directory.CreateDirectory(Path.Combine(dir, "Film"));
        var xtf = Path.Combine(dir, "Dokumente", "test.xtf");
        File.WriteAllText(Path.Combine(dir, "Foto", "H_06-001_002.jpg"), "bild");
        File.WriteAllText(Path.Combine(dir, "Film", "H_06-001.mpg"), "video");
        File.WriteAllText(xtf, """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>06-001</Bezeichnung>
        <Zeitpunkt>2026-06-26</Zeitpunkt>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="S_BAA">
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BAA</KanalSchadencode>
        <Distanz>1.90</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Datei TID="D1">
        <Art>Foto</Art>
        <Klasse>Kanalschaden</Klasse>
        <Objekt>S_BAA</Objekt>
        <Bezeichnung>H_06-001_002.jpg</Bezeichnung>
        <Relativpfad>Foto</Relativpfad>
      </VSA_KEK_2020_LV95.KEK.Datei>
      <VSA_KEK_2020_LV95.KEK.Datei TID="D2">
        <Art>Film</Art>
        <Klasse>Untersuchung</Klasse>
        <Objekt>U1</Objekt>
        <Bezeichnung>H_06-001.mpg</Bezeichnung>
        <Relativpfad>Film</Relativpfad>
      </VSA_KEK_2020_LV95.KEK.Datei>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""");

        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();
            var stats = svc.ImportXtfFiles(new[] { xtf }, project);
            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message}"));

            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "06-001", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);

            // Video-Link zeigt auf die Datei im Export-Root\Film (Eltern-Ebene) und existiert.
            var link = rec!.GetFieldValue("Link");
            Assert.Contains("H_06-001.mpg", link);
            Assert.True(File.Exists(link), $"Link sollte existieren (Eltern-Ebene): {link}\n{debug}");

            // Foto am BAA-Finding zeigt auf Export-Root\Foto und existiert.
            var baa = rec.VsaFindings.FirstOrDefault(f => string.Equals(f.KanalSchadencode, "BAA", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(baa);
            Assert.False(string.IsNullOrWhiteSpace(baa!.FotoPath), debug);
            Assert.True(File.Exists(baa.FotoPath!), $"FotoPath sollte existieren (Eltern-Ebene): {baa.FotoPath}\n{debug}");
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    // ── Herkunft fuer die spaetere revidierte XTF (Etappe 1) ────────────────
    // Die Bindung an Datei, Modell und Element wird beim Import festgehalten. Nur damit
    // laesst sich spaeter genau das urspruengliche Element wiederfinden, statt es ueber
    // Code und Meter erraten zu muessen.

    private const string KekHerkunftXtf = """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" VERSION="03.05.2021" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="ch100000004EB182">
        <Bezeichnung>59220-10.1036545</Bezeichnung>
        <vonPunktBezeichnung>10.1036545</vonPunktBezeichnung>
        <bisPunktBezeichnung>59220</bisPunktBezeichnung>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="ch100000004EB1AB">
        <UntersuchungRef REF="ch100000004EB182" />
        <KanalSchadencode>BCD</KanalSchadencode>
        <Distanz>0.00</Distanz>
        <Videozaehlerstand>00:00:15:00</Videozaehlerstand>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""";

    [Fact]
    public void VsaKekImport_haelt_Datei_Modell_und_Untersuchung_als_Anker_fest()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vsakek-herkunft-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var xtf = Path.Combine(dir, "Buerglen_1225.xtf");
        File.WriteAllText(xtf, KekHerkunftXtf);

        try
        {
            var project = new Project();
            new LegacyXtfImportService().ImportXtfFiles(new[] { xtf }, project);

            var rec = Assert.Single(project.Data);
            Assert.NotNull(rec.XtfHerkunft);
            Assert.Equal("Buerglen_1225.xtf", rec.XtfHerkunft!.Datei);
            Assert.Equal("VSA_KEK_2020_LV95", rec.XtfHerkunft.Modell);
            Assert.Equal("ch100000004EB182", rec.XtfHerkunft.UntersuchungTid);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void VsaKekImport_haelt_die_Element_Kennungen_am_Befund_fest()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vsakek-tid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var xtf = Path.Combine(dir, "test.xtf");
        File.WriteAllText(xtf, KekHerkunftXtf);

        try
        {
            var project = new Project();
            new LegacyXtfImportService().ImportXtfFiles(new[] { xtf }, project);

            var finding = Assert.Single(Assert.Single(project.Data).VsaFindings);
            Assert.Equal("BCD", finding.KanalSchadencode);
            Assert.Equal("ch100000004EB1AB", finding.KanalschadenTid);
            Assert.Equal("ch100000004EB182", finding.UntersuchungTid);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    // Beim Zusammenfuehren mit einem bereits vorhandenen Datensatz darf der Anker
    // nicht verloren gehen — sonst waere er nach dem zweiten Import weg.
    [Fact]
    public void VsaKekImport_behaelt_den_Anker_beim_zweiten_Import()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vsakek-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var xtf = Path.Combine(dir, "zweitlauf.xtf");
        File.WriteAllText(xtf, KekHerkunftXtf);

        try
        {
            var project = new Project();
            var service = new LegacyXtfImportService();
            service.ImportXtfFiles(new[] { xtf }, project);
            service.ImportXtfFiles(new[] { xtf }, project);

            var rec = Assert.Single(project.Data);
            Assert.NotNull(rec.XtfHerkunft);
            Assert.Equal("zweitlauf.xtf", rec.XtfHerkunft!.Datei);
            Assert.Equal("ch100000004EB182", rec.XtfHerkunft.UntersuchungTid);
            Assert.Equal(
                "ch100000004EB1AB",
                Assert.Single(rec.VsaFindings).KanalschadenTid);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void Ein_neuer_Datensatz_ohne_XTF_Herkunft_erfindet_keinen_Anker()
    {
        Assert.Null(new HaltungRecord().XtfHerkunft);
        Assert.Null(new VsaFinding().KanalschadenTid);
        Assert.Null(new VsaFinding().UntersuchungTid);
    }

    [Fact]
    public void VsaKekImport_SetztSchachtObenUnten_AusVonBisPunkt()
    {
        // VSA_KEK liefert die Schachtnamen ueber von-/bisPunktBezeichnung der Untersuchung.
        // Feldabdeckung: diese wurden geparst, aber nicht als Schacht_oben/unten gesetzt.
        var dir = Path.Combine(Path.GetTempPath(), $"vsakek-schacht-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var xtf = Path.Combine(dir, "test.xtf");
        File.WriteAllText(xtf, """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>865-864</Bezeichnung>
        <Zeitpunkt>2026-06-26</Zeitpunkt>
        <vonPunktBezeichnung>865</vonPunktBezeichnung>
        <bisPunktBezeichnung>864</bisPunktBezeichnung>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="S_BCD">
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BCD</KanalSchadencode>
        <Distanz>0.00</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""");
        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();
            svc.ImportXtfFiles(new[] { xtf }, project);

            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "865-864", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            Assert.Equal("865", rec!.GetFieldValue("Schacht_oben"));
            Assert.Equal("864", rec.GetFieldValue("Schacht_unten"));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void VsaKekImport_SetztLaengeDatumUndBemerkungskontext_AusUntersuchung()
    {
        // Charakterisierung der VSA_KEK-Hauptquelle:
        // Stammdaten aus Untersuchung werden direkt uebernommen; Zustandsklasse wird nicht aus Einzelschadenklasse geraten.
        var dir = Path.Combine(Path.GetTempPath(), $"vsakek-fields-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var xtf = Path.Combine(dir, "test.xtf");
        File.WriteAllText(xtf, """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>12-34</Bezeichnung>
        <Zeitpunkt>20260626</Zeitpunkt>
        <Inspizierte_Laenge>18.75</Inspizierte_Laenge>
        <Erfassungsart>TV</Erfassungsart>
        <Grund>Abnahme</Grund>
        <Witterung>trocken</Witterung>
        <Ausfuehrender>Inspektor A</Ausfuehrender>
        <Fahrzeug>FZ-1</Fahrzeug>
        <Geraet>Kamera-7</Geraet>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="S_BBA">
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BBA</KanalSchadencode>
        <Distanz>4.50</Distanz>
        <Einzelschadenklasse>4</Einzelschadenklasse>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""");

        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();

            var stats = svc.ImportXtfFiles(new[] { xtf }, project);
            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));

            Assert.True(stats.Errors == 0, debug);
            var rec = project.Data.SingleOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "12-34", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            Assert.Equal("18.75", rec!.GetFieldValue("Haltungslaenge_m"));
            Assert.Equal("26.06.2026", rec.GetFieldValue("Datum_Jahr"));
            Assert.Equal("", rec.GetFieldValue("Pruefungsresultat"));
            Assert.Equal("", rec.GetFieldValue("Zustandsklasse"));

            var bemerkungen = rec.GetFieldValue("Bemerkungen");
            Assert.Contains("Erfassung: TV", bemerkungen);
            Assert.Contains("Grund: Abnahme", bemerkungen);
            Assert.Contains("Witterung: trocken", bemerkungen);
            Assert.Contains("Ausfuehrender: Inspektor A", bemerkungen);
            Assert.Contains("Fahrzeug: FZ-1", bemerkungen);
            Assert.Contains("Geraet: Kamera-7", bemerkungen);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Sia405Import_ParsesHoldingMaterialAndDn_FromSyntheticBasket()
    {
        // Minimales SYNTHETISCHES SIA405-XTF (kein echter Kundendatensatz):
        // ein SIA405_Abwasser-Basket mit einer Haltung inkl. Material und lichter Hoehe (DN).
        // Deckt den realen Parse-Pfad LegacyXtfImportService.ParseSia405 ab, ohne Uri/Altdorf-Daten ins Repo zu legen.
        var tempPath = Path.Combine(Path.GetTempPath(), $"sia405-{Guid.NewGuid():N}.xtf");
        File.WriteAllText(tempPath, """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="SewerStudioTest" VERSION="2.3">
    <MODELS>
      <MODEL NAME="SIA405_Abwasser_2015_LV95" />
    </MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_Abwasser.SIA405_Abwasser BID="B1">
      <Haltung TID="H1">
        <Bezeichnung>80638-80631</Bezeichnung>
        <LaengeEffektiv>22.5</LaengeEffektiv>
        <Lichte_Hoehe>300</Lichte_Hoehe>
        <Material>Steinzeug</Material>
      </Haltung>
    </SIA405_Abwasser.SIA405_Abwasser>
  </DATASECTION>
</TRANSFER>
""");

        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();

            var stats = svc.ImportXtfFiles(new[] { tempPath }, project);

            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));
            Assert.True(stats.Errors == 0, debug);
            Assert.True(project.Data.Count > 0, $"No records imported.\n{debug}");
            Assert.Equal(stats.Found, project.Data.Count);

            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "80638-80631", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            Assert.Equal("Steinzeug", rec!.GetFieldValue("Rohrmaterial"));
            Assert.Equal("300", rec.GetFieldValue("DN_mm"));
            Assert.Equal("22.5", rec.GetFieldValue("Haltungslaenge_m"));
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public void Sia405Import_UeberschreibtUserEditedFieldsNicht()
    {
        // Charakterisierung der Import-Prioritaet:
        // UserEdit > SIA405. Die Anreicherung darf vorhandene manuelle Werte nicht ersetzen.
        var tempPath = Path.Combine(Path.GetTempPath(), $"sia405-useredit-{Guid.NewGuid():N}.xtf");
        File.WriteAllText(tempPath, """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="SewerStudioTest" VERSION="2.3">
    <MODELS><MODEL NAME="SIA405_Abwasser_2015_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_Abwasser.SIA405_Abwasser BID="B1">
      <Kanal TID="K1">
        <Bezeichnung>K-1</Bezeichnung>
        <Standortname>Dorfstrasse</Standortname>
      </Kanal>
      <Haltung TID="H1">
        <Bezeichnung>80638-80631</Bezeichnung>
        <LaengeEffektiv>22.5</LaengeEffektiv>
        <Lichte_Hoehe>300</Lichte_Hoehe>
        <Material>Steinzeug</Material>
        <AbwasserbauwerkRef REF="K1" />
      </Haltung>
    </SIA405_Abwasser.SIA405_Abwasser>
  </DATASECTION>
</TRANSFER>
""");

        try
        {
            var project = new Project();
            var existing = new HaltungRecord();
            existing.SetFieldValue("Haltungsname", "80638-80631", FieldSource.Manual, userEdited: true);
            existing.SetFieldValue("Rohrmaterial", "Kunststoff", FieldSource.Manual, userEdited: true);
            existing.SetFieldValue("DN_mm", "250", FieldSource.Manual, userEdited: true);
            existing.SetFieldValue("Haltungslaenge_m", "99.9", FieldSource.Manual, userEdited: true);
            project.AddRecord(existing);

            var svc = new LegacyXtfImportService();
            var stats = svc.ImportXtfFiles(new[] { tempPath }, project);
            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));

            Assert.True(stats.Errors == 0, debug);
            Assert.Single(project.Data);
            Assert.Equal("Kunststoff", existing.GetFieldValue("Rohrmaterial"));
            Assert.Equal("250", existing.GetFieldValue("DN_mm"));
            Assert.Equal("99.9", existing.GetFieldValue("Haltungslaenge_m"));
            Assert.Equal("Dorfstrasse", existing.GetFieldValue("Strasse"));
            Assert.True(stats.Conflicts >= 3, $"Erwartete Konflikte fuer UserEdit-Felder.\n{debug}");
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public void Sia405Import_SetztFunktionHierarchisch_AusKanal()
    {
        // SIA405-Kanal liefert Funktionhierarchisch; Haltung verweist via AbwasserbauwerkRef.
        // Erwartet: gueltiger Katalog-Combo-Wert "PAA.<Suffix>".
        var tempPath = Path.Combine(Path.GetTempPath(), $"sia405-fh-{Guid.NewGuid():N}.xtf");
        File.WriteAllText(tempPath, """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="SewerStudioTest" VERSION="2.3">
    <MODELS><MODEL NAME="SIA405_Abwasser_2015_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_Abwasser.SIA405_Abwasser BID="B1">
      <Kanal TID="K1">
        <Bezeichnung>K-1</Bezeichnung>
        <Funktionhierarchisch>Sammelkanal</Funktionhierarchisch>
      </Kanal>
      <Haltung TID="H1">
        <Bezeichnung>80638-80631</Bezeichnung>
        <LaengeEffektiv>22.5</LaengeEffektiv>
        <AbwasserbauwerkRef REF="K1" />
      </Haltung>
    </SIA405_Abwasser.SIA405_Abwasser>
  </DATASECTION>
</TRANSFER>
""");
        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();
            svc.ImportXtfFiles(new[] { tempPath }, project);

            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "80638-80631", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            Assert.Equal("PAA.Sammelkanal", rec!.GetFieldValue("FunktionHierarchisch"));
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public void VsaKekImport_LinksPhotoToCorrectObservation_ViaKanalschadenTid()
    {
        // VSA_KEK-XTF: KEK.Datei.Objekt referenziert die Kanalschaden-TID (diese XTFs haben KEIN OBJ_ID-Element).
        // Regression: frueher wurde Datei.Objekt nur gegen OBJ_ID gematcht -> findingsByObjId leer ->
        // 0 Fotos verknuepft (Fallback packte alles auf die erste Beobachtung). Jetzt Match auch via TID.
        var dir = Path.Combine(Path.GetTempPath(), $"vsakek-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "Foto"));
        var xtf = Path.Combine(dir, "test.xtf");
        File.WriteAllText(Path.Combine(dir, "Foto", "H_22152-3.01_119.jpg"), "bild");
        File.WriteAllText(xtf, """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>22152-3.01</Bezeichnung>
        <Zeitpunkt>2026-06-26</Zeitpunkt>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="S_BCD">
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BCD</KanalSchadencode>
        <Distanz>0.00</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="S_BAA">
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BAA</KanalSchadencode>
        <Distanz>1.90</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Datei TID="D1">
        <Art>Foto</Art>
        <Klasse>Kanalschaden</Klasse>
        <Objekt>S_BAA</Objekt>
        <Bezeichnung>H_22152-3.01_119.jpg</Bezeichnung>
        <Relativpfad>Foto</Relativpfad>
      </VSA_KEK_2020_LV95.KEK.Datei>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""");

        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();
            var stats = svc.ImportXtfFiles(new[] { xtf }, project);
            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));

            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "22152-3.01", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);

            var baa = rec!.VsaFindings.FirstOrDefault(f => string.Equals(f.KanalSchadencode, "BAA", StringComparison.OrdinalIgnoreCase));
            var bcd = rec.VsaFindings.FirstOrDefault(f => string.Equals(f.KanalSchadencode, "BCD", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(baa);
            Assert.False(string.IsNullOrWhiteSpace(baa!.FotoPath), $"Foto muss an BAA (1.90m) haengen.\n{debug}");
            Assert.Contains("H_22152-3.01_119.jpg", baa.FotoPath!);
            // Darf NICHT an der ersten Beobachtung (BCD) haengen — genau der frueher gemeldete Fehler.
            Assert.True(string.IsNullOrWhiteSpace(bcd?.FotoPath), "BCD darf kein Foto haben (kein Lumping auf die erste Beobachtung).");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void VsaKekImport_SetztVideoLink_AusUntersuchungsDatei()
    {
        // VSA_KEK-XTF: KEK.Datei mit Klasse=Untersuchung, Objekt=Untersuchungs-TID, Bezeichnung=H_06-001.mpg, Relativpfad=Film.
        // Erwartet: nach Import ist rec.GetFieldValue("Link") der aufgeloeste Videopfad (enthaelt H_06-001.mpg).
        var dir = Path.Combine(Path.GetTempPath(), $"vsakek-video-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "Film"));
        var xtf = Path.Combine(dir, "test.xtf");
        File.WriteAllText(Path.Combine(dir, "Film", "H_06-001.mpg"), "dummy-video");
        File.WriteAllText(xtf, """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>06-001</Bezeichnung>
        <Zeitpunkt>2026-06-26</Zeitpunkt>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Datei TID="DV1">
        <Art>Film</Art>
        <Klasse>Untersuchung</Klasse>
        <Objekt>U1</Objekt>
        <Bezeichnung>H_06-001.mpg</Bezeichnung>
        <Relativpfad>Film</Relativpfad>
      </VSA_KEK_2020_LV95.KEK.Datei>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""");

        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();
            var stats = svc.ImportXtfFiles(new[] { xtf }, project);
            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));

            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "06-001", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);

            var link = rec!.GetFieldValue("Link");
            Assert.False(string.IsNullOrWhiteSpace(link),
                $"Link-Feld muss den Videopfad enthalten.\n{debug}");
            Assert.Contains("H_06-001.mpg", link!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void M150Import_MergesIntoExistingHolding_WhenNameFormattingDiffers()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"xtf-import-{Guid.NewGuid():N}.m150");
        File.WriteAllText(tempPath, """
<root>
  <row>
    <Haltung>80638 - 80631</Haltung>
    <Inspektionsdatum>2025-01-03</Inspektionsdatum>
    <Laenge>22.5</Laenge>
  </row>
</root>
""");

        try
        {
            var project = new Project();
            var existing = new HaltungRecord();
            existing.SetFieldValue("Haltungsname", "80638-80631", FieldSource.Manual, userEdited: true);
            project.AddRecord(existing);

            var svc = new LegacyXtfImportService();
            var stats = svc.ImportXtfFiles(new[] { tempPath }, project);

            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));
            Assert.True(stats.Errors == 0, debug);
            Assert.Single(project.Data);
            Assert.Equal("03.01.2025", existing.GetFieldValue("Datum_Jahr"));
            Assert.Equal("22.5", existing.GetFieldValue("Haltungslaenge_m"));
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public void M150Import_BuildsHoldingFromHg011Hg012_WhenCombinedHoldingIsMissing()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"xtf-import-{Guid.NewGuid():N}.m150");
        File.WriteAllText(tempPath, """
<root>
  <HG>
    <HG011>80638</HG011>
    <HG012>80631</HG012>
    <HG008>22.5</HG008>
    <HI>
      <HI104>2025-01-03</HI104>
    </HI>
  </HG>
</root>
""");

        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();

            var stats = svc.ImportXtfFiles(new[] { tempPath }, project);
            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));

            Assert.True(stats.Errors == 0, debug);
            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "80638-80631", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            Assert.Equal("03.01.2025", rec!.GetFieldValue("Datum_Jahr"));
            Assert.Equal("22.5", rec.GetFieldValue("Haltungslaenge_m"));
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public void M150Import_ParsesIsybauHgHiStructure_IntoHoldingDateLengthAndVideoLink()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"xtf-import-{Guid.NewGuid():N}.xml");
        File.WriteAllText(tempPath, """
<?xml version="1.0" encoding="iso-8859-1"?>
<DATA>
  <HG>
    <HG003>23021</HG003>
    <HG004>22369</HG004>
    <HG304>BETON</HG304>
    <HG306>300</HG306>
    <HG310>35.120</HG310>
    <HI>
      <HI104>2014-04-22</HI104>
      <HI116>1_1_1_22042014_112151.mp2</HI116>
      <HZ>
        <HZ001>9.8</HZ001>
        <HZ002>BBA</HZ002>
        <HZ010>Komplexes Wurzelwerk</HZ010>
      </HZ>
    </HI>
  </HG>
</DATA>
""");

        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();

            var stats = svc.ImportXtfFiles(new[] { tempPath }, project);
            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));

            Assert.True(stats.Errors == 0, debug);
            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "23021-22369", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            Assert.Equal("22.04.2014", rec!.GetFieldValue("Datum_Jahr"));
            Assert.Equal("35.120", rec.GetFieldValue("Haltungslaenge_m"));
            Assert.Equal("300", rec.GetFieldValue("DN_mm"));
            Assert.Equal("BETON", rec.GetFieldValue("Rohrmaterial"));
            Assert.Equal("1_1_1_22042014_112151.mp2", rec.GetFieldValue("Link"));
            Assert.Contains("BBA", rec.GetFieldValue("Primaere_Schaeden"));
            Assert.True(rec.VsaFindings.Count > 0, "Expected VsaFindings from HZ nodes");
            Assert.Contains(rec.VsaFindings, f => string.Equals(f.KanalSchadencode, "BBA", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(stats.Messages, m => m.Context == "M150" && m.Message.Contains("HG erkannt=1", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public void M150Import_PrefersHi116VideoFile_OverHi006Code()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"xtf-import-{Guid.NewGuid():N}.xml");
        File.WriteAllText(tempPath, """
<?xml version="1.0" encoding="iso-8859-1"?>
<DATA>
  <HG>
    <HG003>23021</HG003>
    <HG004>22369</HG004>
    <HI>
      <HI006>L100</HI006>
      <HI104>2014-04-22</HI104>
      <HI116>1_1_1_22042014_112151.mp2</HI116>
    </HI>
  </HG>
</DATA>
""");

        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();

            var stats = svc.ImportXtfFiles(new[] { tempPath }, project);
            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));

            Assert.True(stats.Errors == 0, debug);
            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "23021-22369", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            Assert.Equal("1_1_1_22042014_112151.mp2", rec!.GetFieldValue("Link"));
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }
}

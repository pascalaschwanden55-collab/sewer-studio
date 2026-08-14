using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Ruecknahme der Dateien eines verworfenen Ein-Knopf-Imports
/// (Gesamtaudit 2026-08-14, P1-5).
/// </summary>
public sealed class ImportedFileLedgerServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewer-import-ledger-" + Guid.NewGuid().ToString("N"));

    public ImportedFileLedgerServiceTests()
    {
        Schreibe("projekt.json", "{}");
        Schreibe("Importdateien\\XTF\\alt.xtf", "vorher vorhanden");
    }

    [Fact]
    public void Neu_angelegte_Dateien_werden_zurueckgenommen()
    {
        var ledger = new ImportedFileLedgerService();
        var vorher = ledger.Capture(_root);

        Schreibe("Importdateien\\XTF\\neu.xtf", "vom Import angelegt");
        Schreibe("Haltungen_Verteilt\\H1\\20260814_H1.pdf", "verteiltes Protokoll");

        var ergebnis = ledger.RollbackNewFiles(vorher);

        Assert.True(ergebnis.RolledBack);
        Assert.Equal(2, ergebnis.DeletedFiles);
        Assert.False(File.Exists(Voll("Importdateien\\XTF\\neu.xtf")));
        Assert.False(File.Exists(Voll("Haltungen_Verteilt\\H1\\20260814_H1.pdf")));
    }

    [Fact]
    public void Vorher_vorhandene_Dateien_bleiben_unangetastet()
    {
        var ledger = new ImportedFileLedgerService();
        var vorher = ledger.Capture(_root);
        Schreibe("Importdateien\\XTF\\neu.xtf", "neu");

        ledger.RollbackNewFiles(vorher);

        Assert.True(File.Exists(Voll("projekt.json")));
        Assert.Equal("vorher vorhanden", File.ReadAllText(Voll("Importdateien\\XTF\\alt.xtf")));
    }

    [Fact]
    public void Eine_geaenderte_Bestandsdatei_wird_nicht_geloescht()
    {
        var ledger = new ImportedFileLedgerService();
        var vorher = ledger.Capture(_root);

        // Der Import hat eine vorhandene Datei ergaenzt (andere Groesse, gleicher Pfad).
        File.WriteAllText(Voll("Importdateien\\XTF\\alt.xtf"), "vorher vorhanden, jetzt ergaenzt");

        var ergebnis = ledger.RollbackNewFiles(vorher);

        Assert.True(ergebnis.RolledBack);
        Assert.Equal(0, ergebnis.DeletedFiles);
        Assert.True(File.Exists(Voll("Importdateien\\XTF\\alt.xtf")));
    }

    [Fact]
    public void Fehlt_eine_frueher_vorhandene_Datei_wird_gar_nichts_geloescht()
    {
        // Fail-closed: dann war mehr als ein reines Hinzufuegen im Spiel und eine
        // Teil-Ruecknahme koennte Daten kosten.
        var ledger = new ImportedFileLedgerService();
        var vorher = ledger.Capture(_root);

        Schreibe("Importdateien\\XTF\\neu.xtf", "neu");
        File.Delete(Voll("Importdateien\\XTF\\alt.xtf"));

        var ergebnis = ledger.RollbackNewFiles(vorher);

        Assert.False(ergebnis.RolledBack);
        Assert.Equal(0, ergebnis.DeletedFiles);
        Assert.True(File.Exists(Voll("Importdateien\\XTF\\neu.xtf")));
        Assert.Contains(ergebnis.Messages, m => m.Contains("nichts geloescht", StringComparison.Ordinal));
    }

    [Fact]
    public void Der_Importbericht_bleibt_absichtlich_liegen()
    {
        // Die Diagnosespur soll gerade bei einem verworfenen Import erhalten bleiben.
        var ledger = new ImportedFileLedgerService();
        var vorher = ledger.Capture(_root);

        Schreibe("__IMPORT_REPORTS\\bericht.txt", "Diagnose");
        Schreibe("Importdateien\\XTF\\neu.xtf", "neu");

        var ergebnis = ledger.RollbackNewFiles(vorher);

        Assert.True(ergebnis.RolledBack);
        Assert.Equal(1, ergebnis.DeletedFiles);
        Assert.True(File.Exists(Voll("__IMPORT_REPORTS\\bericht.txt")));
    }

    [Fact]
    public void Neue_leere_Ordner_verschwinden_wieder()
    {
        var ledger = new ImportedFileLedgerService();
        var vorher = ledger.Capture(_root);
        Schreibe("Fotos\\Haltungen\\H1\\bild.jpg", "foto");

        ledger.RollbackNewFiles(vorher);

        Assert.False(Directory.Exists(Voll("Fotos\\Haltungen\\H1")));
        Assert.False(Directory.Exists(Voll("Fotos")));
    }

    [Fact]
    public void Ein_vorher_vorhandener_Ordner_bleibt_stehen()
    {
        Directory.CreateDirectory(Voll("Fotos"));
        var ledger = new ImportedFileLedgerService();
        var vorher = ledger.Capture(_root);
        Schreibe("Fotos\\neu.jpg", "foto");

        ledger.RollbackNewFiles(vorher);

        Assert.True(Directory.Exists(Voll("Fotos")));
        Assert.False(File.Exists(Voll("Fotos\\neu.jpg")));
    }

    [Fact]
    public void Ohne_Aenderung_gibt_es_nichts_zurueckzunehmen()
    {
        var ledger = new ImportedFileLedgerService();
        var vorher = ledger.Capture(_root);

        var ergebnis = ledger.RollbackNewFiles(vorher);

        Assert.True(ergebnis.RolledBack);
        Assert.Equal(0, ergebnis.DeletedFiles);
        Assert.Equal(0, ergebnis.KeptFiles);
    }

    [Fact]
    public void Ein_fehlender_Projektordner_meldet_statt_zu_werfen()
    {
        var ledger = new ImportedFileLedgerService();
        var vorher = ledger.Capture(Path.Combine(_root, "gibtesnicht"));

        var ergebnis = ledger.RollbackNewFiles(vorher);

        Assert.False(ergebnis.RolledBack);
        Assert.Contains(ergebnis.Messages, m => m.Contains("nicht gefunden", StringComparison.Ordinal));
    }

    private string Voll(string relativ) => Path.Combine(_root, relativ);

    private void Schreibe(string relativ, string inhalt)
    {
        var voll = Voll(relativ);
        Directory.CreateDirectory(Path.GetDirectoryName(voll)!);
        File.WriteAllText(voll, inhalt);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Aufraeumen darf den Test nicht zum Scheitern bringen.
        }
    }
}

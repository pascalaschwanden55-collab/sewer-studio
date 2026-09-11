using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class BackupRunJournalTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "backup-journal-test-" + Guid.NewGuid());

    [Fact]
    public void Abbruch_stellt_ersetzte_und_geloeschte_Dateien_wieder_her_und_entfernt_neue()
    {
        Directory.CreateDirectory(_root);
        var first = Path.Combine(_root, "eins.txt");
        var deleted = Path.Combine(_root, "zwei.txt");
        var added = Path.Combine(_root, "neu.txt");
        File.WriteAllText(first, "alter Stand");
        File.WriteAllText(deleted, "bleibt erhalten");
        using (var journal = new BackupRunJournal(_root))
        {
            journal.Preserve(first);
            File.WriteAllText(first, "neuer Stand");
            journal.Preserve(deleted);
            File.Delete(deleted);
            journal.Preserve(added);
            File.WriteAllText(added, "neu");
        }
        Assert.Equal("alter Stand", File.ReadAllText(first));
        Assert.Equal("bleibt erhalten", File.ReadAllText(deleted));
        Assert.False(File.Exists(added));
        Assert.False(BackupRunJournal.IsPending(_root));
    }

    [Fact]
    public void Abschluss_behaelt_neuen_Stand_und_archiviert_die_Vorversion()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "datei.txt");
        File.WriteAllText(file, "alt");
        using (var journal = new BackupRunJournal(_root))
        {
            journal.Preserve(file);
            File.WriteAllText(file, "neu");
            journal.Commit();
        }
        Assert.Equal("neu", File.ReadAllText(file));
        var copies = Directory.GetFiles(Path.Combine(_root, "_Versionen"), "datei.txt", SearchOption.AllDirectories);
        Assert.Single(copies);
        Assert.Equal("alt", File.ReadAllText(copies[0]));
    }

    [Fact]
    public void Zweiter_Lauf_ist_waehrend_eines_offenen_Laufs_gesperrt()
    {
        Directory.CreateDirectory(_root);
        using var first = new BackupRunJournal(_root);
        Assert.Throws<IOException>(() => new BackupRunJournal(_root));
    }

    [Fact]
    public void Neustart_laesst_sich_allein_aus_dem_dauerhaften_Protokoll_zuruecksetzen()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "datei.txt");
        File.WriteAllText(file, "vor Absturz");
        var lost = new BackupRunJournal(_root);
        lost.Preserve(file);
        File.WriteAllText(file, "unvollständiger Lauf");
        // Prozessende nachbilden: nur das Betriebssystem-Handle freigeben;
        // insbesondere KEIN Dispose/Rollback des alten Dienstes aufrufen.
        var handle = (FileStream)typeof(BackupRunJournal).GetField("_lock",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(lost)!;
        handle.Dispose();

        using (var restarted = new BackupRunJournal(_root))
        {
            Assert.Equal("vor Absturz", File.ReadAllText(file));
            restarted.Rollback();
        }
        Assert.False(BackupRunJournal.IsPending(_root));
    }

    [Fact]
    public void Beschaedigte_Vorherkopie_sperrt_Recovery_ohne_Zieldatei_zu_ueberschreiben()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "datei.txt");
        File.WriteAllText(file, "alt");
        var lost = new BackupRunJournal(_root);
        lost.Preserve(file);
        File.WriteAllText(file, "aktueller Inhalt");
        File.WriteAllText(Path.Combine(_root, "_Versionen", ".unterbrochener-lauf", "datei.txt"), "kaputt");
        ((FileStream)typeof(BackupRunJournal).GetField("_lock",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(lost)!).Dispose();
        Assert.Throws<InvalidDataException>(() => new BackupRunJournal(_root));
        Assert.Equal("aktueller Inhalt", File.ReadAllText(file));
        Assert.True(BackupRunJournal.IsPending(_root));
    }

    [Fact]
    public void Neue_Vorversion_bleibt_auch_bei_zurueckgestellter_Uhr_die_neueste()
    {
        Directory.CreateDirectory(_root);
        var future = AuswertungPro.Next.Application.Backup.BackupVersionRetention.BuildStandName(DateTime.Now.AddDays(1));
        Directory.CreateDirectory(Path.Combine(_root, "_Versionen", future));
        var file = Path.Combine(_root, "datei.txt");
        File.WriteAllText(file, "letzter Stand");
        using (var journal = new BackupRunJournal(_root))
        {
            journal.Preserve(file);
            File.WriteAllText(file, "neu");
            journal.Commit();
        }
        var saved = Assert.Single(Directory.GetFiles(Path.Combine(_root, "_Versionen"), "datei.txt", SearchOption.AllDirectories));
        var stand = Path.GetFileName(Path.GetDirectoryName(saved));
        Assert.True(string.CompareOrdinal(stand, future) > 0);
        Assert.Equal("letzter Stand", File.ReadAllText(saved));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}

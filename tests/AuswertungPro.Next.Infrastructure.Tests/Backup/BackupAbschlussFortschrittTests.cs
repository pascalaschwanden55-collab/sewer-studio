using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

/// <summary>
/// Anlass Buerglen 23.09.2026: Ein Lauf ueber 274'334 Dateien stand nach dem Kopieren
/// rund eine Stunde bei 100 % auf "Extras / umgebung.txt" — die Datei war da laengst
/// vollstaendig geschrieben. Zwischen der letzten Kopiermeldung und dem ersten
/// Lebenszeichen der Pruefphase laufen SECHS vollstaendige Durchlaeufe ueber den
/// Zielbaum (Zielordner dreimal pruefen, verwaiste Dateien suchen, leere Ordner
/// entfernen, Dateien zaehlen). Keiner davon meldete etwas, fuenf davon beachteten
/// den Abbruch nicht. Fuer den Bearbeiter war "arbeitet noch" nicht von "haengt" zu
/// unterscheiden, und ein Abbruch liess das Journal offen stehen.
/// </summary>
public sealed class BackupAbschlussFortschrittTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sewerstudio-abschluss-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Zwischen_Kopieren_Und_Pruefen_meldet_der_Lauf_woran_er_arbeitet()
    {
        var meldungen = new List<FullBackupProgress>();
        var service = new FullBackupService(CreateSourceTree);

        var ergebnis = await service.RunAsync(
            Path.Combine(_root, "ziel"), new SofortFortschritt(meldungen.Add));

        Assert.True(ergebnis.Success, ergebnis.Error);

        var letzteKopie = meldungen.FindLastIndex(m => m.Component == "Extras");
        var erstePruefung = meldungen.FindIndex(m => m.Component == "Prüfe Sicherung");
        Assert.True(letzteKopie >= 0, "Keine Extras-Meldung gefunden.");
        Assert.True(erstePruefung > letzteKopie, "Keine Pruefmeldung nach den Extras.");

        var dazwischen = meldungen
            .Skip(letzteKopie + 1)
            .Take(erstePruefung - letzteKopie - 1)
            .ToList();

        Assert.True(
            dazwischen.Count > 0,
            "Zwischen der letzten Kopiermeldung und der Pruefphase kam keine einzige "
            + "Rueckmeldung — genau das laesst einen laufenden Abschluss wie ein "
            + "Haengen aussehen.");
    }

    [Fact]
    public void Der_Zielordner_Waechter_bricht_auf_Wunsch_ab()
    {
        var ordner = Path.Combine(_root, "ziel-waechter");
        Directory.CreateDirectory(Path.Combine(ordner, "tief", "tiefer"));
        File.WriteAllText(Path.Combine(ordner, "tief", "tiefer", "a.txt"), "x");

        using var abbruch = new CancellationTokenSource();
        abbruch.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => BackupTargetPathGuard.EnsureTreeIsSafe(ordner, abbruch.Token));
    }

    /// <summary>
    /// Der Lauf vom 22./23.09.2026 arbeitete 15 Stunden und hinterliess im
    /// Anwendungsprotokoll KEIN einziges Wort — im ganzen Sicherungsweg gibt es
    /// keinen Logger. Deshalb liess sich hinterher nicht feststellen, wo er stand.
    /// Das Protokoll liegt bewusst NEBEN dem Sicherungsordner: darin wuerde es die
    /// Manifest-Pruefsumme brechen (es waechst nach dem Hashen weiter) und beim
    /// Aufraeumen als verwaiste Datei entfernt.
    /// </summary>
    [Fact]
    public async Task Ein_Lauf_hinterlaesst_ein_Protokoll_neben_der_Sicherung()
    {
        var ziel = Path.Combine(_root, "ziel-protokoll");
        var service = new FullBackupService(CreateSourceTree);

        var ergebnis = await service.RunAsync(ziel);
        Assert.True(ergebnis.Success, ergebnis.Error);

        var protokoll = Path.Combine(ziel, FullBackupService.ProtokollDateiName);
        Assert.True(
            File.Exists(protokoll),
            "Kein Laufprotokoll — ein stockender Lauf bliebe wieder unauffindbar.");

        var text = await File.ReadAllTextAsync(protokoll);
        Assert.Contains("Zielordner prüfen", text);
        Assert.Contains("Alte Dateien aufräumen", text);
        Assert.Contains("Abgeschlossen", text);

        // Innerhalb der Sicherung darf es nicht liegen, sonst bricht es die Pruefsumme.
        Assert.False(File.Exists(Path.Combine(
            ziel, BackupPlanBuilder.TargetFolderName, FullBackupService.ProtokollDateiName)));
    }

    [Fact]
    public async Task Das_protokoll_nennt_jede_warnung_nicht_nur_die_ersten_200()
    {
        // Audit A17 (23.09.2026): Rueckgabe und Manifest sind auf 200 Pfade gekuerzt; der Dialog verspricht
        // aber die vollstaendige Liste. Sie steht jetzt im Laufprotokoll neben der Sicherung.
        var ziel = Path.Combine(_root, "ziel-warnungen");
        var fehlend = Enumerable.Range(1, 205)
            .Select(i => new BackupSingleFile(Path.Combine(_root, "weg", $"fehlt_{i:000}.pdf"), $"Verknuepft/fehlt_{i:000}.pdf", WarnIfMissing: true))
            .ToList();
        var service = new FullBackupService(() => CreateSourceTree() with { ReferencedFiles = fehlend });

        var ergebnis = await service.RunAsync(ziel);

        Assert.True(ergebnis.Success, ergebnis.Error);
        Assert.Equal(205, ergebnis.SkippedFileTotal);
        var text = await File.ReadAllTextAsync(Path.Combine(ziel, FullBackupService.ProtokollDateiName));
        foreach (var f in fehlend)
            Assert.Contains(Path.GetFileName(f.SourcePath), text);
    }

    private FullBackupSources CreateSourceTree()
    {
        var repo = Path.Combine(_root, "repo");
        var brain = Path.Combine(_root, "brain");
        var local = Path.Combine(_root, "local");
        var projects = Path.Combine(_root, "projects");

        Write(Path.Combine(repo, "src", "app.cs"), "code");
        Write(Path.Combine(brain, "gold.json"), "gold");
        Write(Path.Combine(local, "settings.json"), "{}");
        Write(Path.Combine(projects, "Projektdateien", "projekt.json"), "{}");

        return new FullBackupSources(
            RepoRoot: repo,
            KnowledgeRoot: brain,
            LocalSewerStudioDir: local,
            RoamingSewerStudioDir: Path.Combine(_root, "roaming-sewer"),
            RoamingAuswertungProDir: Path.Combine(_root, "roaming-auswertung"),
            DesktopDir: Path.Combine(_root, "desktop"),
            AppVersion: "5.0-test",
            EnvironmentVariables: new Dictionary<string, string>(),
            ProjectRoots: [projects],
            IncludeProjectVideos: false);
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private sealed class SofortFortschritt(Action<FullBackupProgress> melde)
        : IProgress<FullBackupProgress>
    {
        public void Report(FullBackupProgress value) => melde(value);
    }
}

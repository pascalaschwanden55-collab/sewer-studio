using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace TrainingDataInventory;

internal sealed class TrainingInventoryConsole
{
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public TrainingInventoryConsole(TextWriter output, TextWriter error)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public void WriteStarted(bool computeAssetHashes)
    {
        _output.WriteLine("AP 0.1: Trainingsbestand wird nur lesend geprueft ...");
        if (!computeAssetHashes)
            _output.WriteLine("Hinweis: Asset-Hashes sind aus. Trainingsfreigaben bleiben gesperrt.");
    }

    public void WriteCompleted(
        TrainingDataInventoryReport report,
        TrainingInventoryReportFiles files,
        bool successful)
    {
        ArgumentNullException.ThrowIfNull(report);
        var summary = report.Summary;
        var writer = successful ? _output : _error;
        writer.WriteLine(successful
            ? "Pruefung abgeschlossen. Keine Quelldatei wurde veraendert."
            : "Pruefung mit Fehlern abgeschlossen. Einzelheiten stehen im Bericht.");
        writer.WriteLine($"Teacher-Eintraege:                 {summary.Data.TeacherRecords}");
        writer.WriteLine($"Bild + positive Box:               {summary.Data.ExistingFrameAndPositiveArea}");
        writer.WriteLine($"Bild + positive Box + Herkunft:    {summary.Holdings.ExistingFramePositiveAreaExplicit}");
        writer.WriteLine($"Freie Train/Val-Kandidaten:        {summary.Triage.TrainValCandidates}");
        writer.WriteLine($"Quarantaene wegen Herkunft:        {summary.Triage.QuarantineOrigin}");
        writer.WriteLine($"Quarantaene wegen Box-Geometrie:   {summary.Triage.QuarantineGeometry}");
        writer.WriteLine($"Unklare Herkunft insgesamt:        {summary.Holdings.NonExplicit}");
        writer.WriteLine($"Archiv:                             {summary.Triage.Archive}");
        writer.WriteLine($"Sichere Dateinamen-Treffer (Pruefung noetig): {summary.Paths.FullFrameSuggestions}");
        writer.WriteLine($"Eval-/Abnahmedaten gesperrt:        {summary.Evaluation.ReservedRecords}");
        writer.WriteLine($"Eval-Schutz nicht geprueft:         {summary.Evaluation.UncheckedRecords}");
        writer.WriteLine($"Bericht: {files.ReportPath}");
        writer.WriteLine($"Pruefsumme: {files.Sha256Path}");
    }

    public void WriteHelp()
        => _output.WriteLine("""
            TrainingDataInventory (AP 0.1)

            Erstellt einen rein lesenden Bericht ueber Teacher- und Trainingsquellen.
            Es werden keine Annotationen, Bilder oder gespeicherten Pfade veraendert.

            Verwendung:
              dotnet run --project tools/TrainingDataInventory -c Release --no-build --

            Optionen:
              --root <ordner>            Standard: C:\KI_BRAIN
              --eval-root <ordner>       Standard: <root>\eval_set
              --out <datei.json>         Standard: <root>\training\reports\...
              --search-root <ordner>     Erlaubter Suchordner; mehrfach moeglich
              --protected-root <ordner>  Eval-/Abnahmeordner; mehrfach moeglich
              --current-only             Nur die aktuellen JSON-Dateien einlesen
              --no-hashes                Asset-Hashes aus; Freigaben bleiben gesperrt
              --help                     Diese Hilfe

            Ein eindeutiger Dateiname ist nur ein Vorschlag zur manuellen Pruefung.
            Das Werkzeug fuehrt keine automatische Pfadreparatur aus.
            --out darf nur auf eine Datei unter <root>\training\reports zeigen.
            Mit Strg+C wird der Lauf kontrolliert abgebrochen.
            """);
}

using System.Text;
using AuswertungPro.Next.Infrastructure.Import;

internal static class ModernizeReportWriter
{
    public static void Write(string projectFolder, ModernizeReport report)
    {
        var path = Path.Combine(projectFolder, ProjectStructure.ImportReports, $"modernize_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var sb = new StringBuilder();
        AppendReportText(sb, projectFolder, report);
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        report.Messages.Add($"Report: {path}");
    }

    public static void Print(string projectFolder, string projectFile, string? sourceFolder, ModernizeReport report)
        => Print(Console.Out, projectFolder, projectFile, sourceFolder, report);

    public static void Print(TextWriter writer, string projectFolder, string projectFile, string? sourceFolder, ModernizeReport report)
    {
        writer.WriteLine($"Projekt: {projectFolder}");
        writer.WriteLine($"Projektdatei: {projectFile}");
        if (!string.IsNullOrWhiteSpace(sourceFolder))
            writer.WriteLine($"Quelle: {sourceFolder}");
        writer.WriteLine();
        WriteReportBody(writer, report);
    }

    private static void AppendReportText(StringBuilder sb, string projectFolder, ModernizeReport report)
    {
        using var writer = new StringWriter(sb);
        writer.WriteLine($"Projekt: {projectFolder}");
        WriteReportBody(writer, report);
    }

    private static void WriteReportBody(TextWriter writer, ModernizeReport report)
    {
        writer.WriteLine($"Ordner neu: {report.FoldersCreated}");
        writer.WriteLine($"Importdateien kopiert: {report.ImportCopied}, wiederverwendet: {report.ReusedFiles}, uebersprungen: {report.ImportSkipped}");
        writer.WriteLine($"Haltungsdateien kopiert: {report.HaltungFilesCopied}");
        writer.WriteLine($"Schachtdateien kopiert: {report.SchachtFilesCopied}");
        writer.WriteLine($"Plaene kopiert: {report.PlanFilesCopied}");
        writer.WriteLine($"Fotos kopiert: {report.PhotoFilesCopied}");
        writer.WriteLine($"Haltungen_Verteilt bereinigt: {report.FlattenedFiles}");
        writer.WriteLine($"Alte Unterordner entfernt: {report.FoldersRemoved}");
        writer.WriteLine($"Haltungsfotos zentralisiert: {report.CentralPhotos}");
        writer.WriteLine($"Protokollfotos repariert: {report.ProtocolPhotosRepaired}");
        writer.WriteLine($"Pfade aktualisiert: {report.RelinkedPaths}");
        writer.WriteLine($"Metadaten aktualisiert: {report.MetadataUpdated}");
        writer.WriteLine($"Externe Links bereinigt: {report.ExternalLinksRemoved}");
        writer.WriteLine($"Externe Snapshotlinks bereinigt: {report.SnapshotLinksRemoved}");
        writer.WriteLine($"Nicht aufgeloeste Pfade: {report.UnresolvedPaths}");
        writer.WriteLine($"Kopierfehler: {report.CopyErrors}");
        writer.WriteLine();

        foreach (var message in report.Messages.Take(200))
            writer.WriteLine(message);
        if (report.Messages.Count > 200)
            writer.WriteLine($"... {report.Messages.Count - 200} weitere Meldungen im Lauf.");
    }
}

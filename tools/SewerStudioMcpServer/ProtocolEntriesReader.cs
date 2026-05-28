using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

namespace AuswertungPro.Tools.SewerStudioMcpServer;

public static class ProtocolEntriesReader
{
    public static ProtocolEntriesResult Read(string haltungenRoot, string caseId)
    {
        var folder = HaltungenReader.FindCaseFolder(haltungenRoot, caseId);
        if (folder is null)
        {
            return new ProtocolEntriesResult(
                CaseId: caseId,
                FolderPath: null,
                PdfPath: null,
                Error: "Haltungs-Ordner nicht gefunden",
                Entries: []);
        }

        var pdfPath = HaltungenReader.FindProtocolPdf(folder);
        if (pdfPath is null)
        {
            return new ProtocolEntriesResult(
                CaseId: caseId,
                FolderPath: folder,
                PdfPath: null,
                Error: "Kein nutzbares PDF gefunden",
                Entries: []);
        }

        var extractor = new PdfProtocolExtractor();
        var parsed = extractor.ExtractAsync(pdfPath).GetAwaiter().GetResult();
        var entries = parsed
            .Select(GroundTruthProtocolEntryMapper.ToProtocolEntry)
            .Select((entry, index) => new ProtocolEntryDto(
                Index: index,
                Code: entry.Code,
                Beschreibung: entry.Beschreibung,
                MeterStart: entry.MeterStart,
                MeterEnd: entry.MeterEnd,
                IsStreckenschaden: entry.IsStreckenschaden,
                Mpeg: entry.Mpeg,
                ZeitSeconds: entry.Zeit?.TotalSeconds,
                Severity: entry.CodeMeta?.Severity))
            .ToArray();

        return new ProtocolEntriesResult(
            CaseId: caseId,
            FolderPath: folder,
            PdfPath: pdfPath,
            Error: null,
            Entries: entries);
    }
}

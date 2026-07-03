using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageContainingFolderTargetResolverTests
{
    [Fact]
    public void Resolve_prefers_existing_link_path()
    {
        var record = Record("H-1");
        record.SetFieldValue(FieldKeys.Link, "raw-link", FieldSource.Manual, userEdited: false);

        var target = DataPageContainingFolderTargetResolver.Resolve(
            record,
            resolveExistingPath: raw => raw == "raw-link" ? "C:\\projekt\\H-1\\film.mp4" : null,
            ensureProtocolPath: _ => throw new InvalidOperationException("PDF path should not be requested."),
            getProjectFolder: () => throw new InvalidOperationException("Project folder should not be requested."),
            resolveOriginalPdfPaths: (_, _) => throw new InvalidOperationException("Fallback should not run."));

        Assert.Equal("C:\\projekt\\H-1\\film.mp4", target);
    }

    [Fact]
    public void Resolve_uses_protocol_path_when_link_is_missing()
    {
        var record = Record("H-1");

        var target = DataPageContainingFolderTargetResolver.Resolve(
            record,
            resolveExistingPath: _ => null,
            ensureProtocolPath: _ => "C:\\projekt\\H-1\\protokoll.pdf",
            getProjectFolder: () => throw new InvalidOperationException("Project folder should not be requested."),
            resolveOriginalPdfPaths: (_, _) => throw new InvalidOperationException("Fallback should not run."));

        Assert.Equal("C:\\projekt\\H-1\\protokoll.pdf", target);
    }

    [Fact]
    public void Resolve_falls_back_to_original_pdf_paths()
    {
        var record = Record("H-1");

        var target = DataPageContainingFolderTargetResolver.Resolve(
            record,
            resolveExistingPath: _ => null,
            ensureProtocolPath: _ => null,
            getProjectFolder: () => "C:\\projekt",
            resolveOriginalPdfPaths: (actualRecord, projectFolder) =>
            {
                Assert.Same(record, actualRecord);
                Assert.Equal("C:\\projekt", projectFolder);
                return ["C:\\projekt\\Importdateien\\PDF\\gesamt.pdf"];
            });

        Assert.Equal("C:\\projekt\\Importdateien\\PDF\\gesamt.pdf", target);
    }

    [Fact]
    public void Resolve_returns_null_when_record_or_paths_are_missing()
    {
        Assert.Null(DataPageContainingFolderTargetResolver.Resolve(
            null,
            resolveExistingPath: _ => throw new InvalidOperationException("No lookup without record."),
            ensureProtocolPath: _ => throw new InvalidOperationException("No lookup without record."),
            getProjectFolder: () => throw new InvalidOperationException("No lookup without record."),
            resolveOriginalPdfPaths: (_, _) => throw new InvalidOperationException("No lookup without record.")));

        Assert.Null(DataPageContainingFolderTargetResolver.Resolve(
            Record("H-1"),
            resolveExistingPath: _ => null,
            ensureProtocolPath: _ => "",
            getProjectFolder: () => "",
            resolveOriginalPdfPaths: (_, _) => []));
    }

    private static HaltungRecord Record(string holding)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, holding, FieldSource.Manual, userEdited: false);
        return record;
    }
}

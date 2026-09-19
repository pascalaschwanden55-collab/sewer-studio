using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Tests;

public sealed partial class ImportManualWorkflowControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QrImport_NutztVorschauUndSpeichernUndSetztNachDefekterDateiFort(bool preview)
    {
        var dialogs = new DialogFake { OpenFilesResult = [@"C:\Import\defekt.png", @"C:\Import\gut.png"] };
        var stored = new StoredImportFileFake();
        var state = new WorkflowState();
        var live = new Project();
        var qr = new QrFake();
        var controller = CreateController(dialogs, new PdfImportFake(), new XtfImportFake(),
            new FolderImportFake(), stored, qr: qr);
        await controller.ImportSchachtProQrAsync(CreateContext(state, live, showPreviewFirst: preview));
        Assert.Equal(2, qr.Calls);
        Assert.Empty(live.SchaechteData);
        Assert.Contains("*.png", dialogs.LastOpenFilesFilter);
        if (preview)
        {
            Assert.True(state.PreviewWasShown);
            Assert.Null(state.ReplacedProject);
            Assert.Equal(0, state.SaveCount);
        }
        else
        {
            Assert.Single(state.ReplacedProject!.SchaechteData);
            Assert.Equal(1, state.SaveCount);
            Assert.Contains("Schächte: 1 gefunden", state.Summary);
            Assert.Contains("defekt.png", state.Details);
        }
    }

    [Fact]
    public async Task NurDefekteQrBilder_SpeichernKeineProjektAenderung()
    {
        var state = new WorkflowState();
        var stored = new StoredImportFileFake();
        var controller = CreateController(new DialogFake { OpenFilesResult = [@"C:\Import\defekt.png"] },
            new PdfImportFake(), new XtfImportFake(), new FolderImportFake(), stored, qr: new QrFake());
        await controller.ImportSchachtProQrAsync(CreateContext(state, new Project()));
        Assert.Null(state.ReplacedProject);
        Assert.Equal(0, state.SaveCount);
        Assert.Empty(stored.Calls);
        Assert.Contains("fehlgeschlagen", state.Summary);
    }

    private sealed class QrFake : ISchachtProQrImportService
    {
        public int Calls { get; private set; }
        public Result<ImportStats> ImportImage(string path, Project project, ImportRunContext? context = null)
        {
            Calls++;
            if (path.EndsWith("defekt.png")) return Result<ImportStats>.Fail("BAD_QR", "Pruefsumme falsch");
            var shaft = new SchachtRecord();
            shaft.SetFieldValue("Schachtnummer", "QR-001", FieldSource.Spro, false);
            project.SchaechteData.Add(shaft);
            return Result<ImportStats>.Success(new(1, 1, 0, 0, 0, []));
        }
    }
}

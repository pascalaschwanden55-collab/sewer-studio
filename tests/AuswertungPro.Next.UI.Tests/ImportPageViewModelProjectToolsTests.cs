using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Vsa;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Vsa;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ImportPageViewModelProjectToolsTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(_ => { });

    [Fact]
    public async Task Projekt_portabel_ohne_gespeichertes_Projekt_zeigt_Hinweis()
    {
        var dialogs = new DialogFake();
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Dialogs = dialogs
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var viewModel = new ImportPageViewModel(shell, services);

        await viewModel.MakeProjectPortableCommand.ExecuteAsync(null);

        Assert.Contains("zuerst speichern", dialogs.LastInfoMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Projekt portabel machen", dialogs.LastInfoTitle);
    }

    [Fact]
    public async Task Fotos_zuordnen_ohne_gespeichertes_Projekt_zeigt_Hinweis()
    {
        var dialogs = new DialogFake();
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Dialogs = dialogs
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var viewModel = new ImportPageViewModel(shell, services);

        await viewModel.AssignPhotosFromFolderCommand.ExecuteAsync(null);

        Assert.Contains("zuerst speichern", dialogs.LastInfoMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Fotos zuordnen", dialogs.LastInfoTitle);
    }

    [Fact]
    public async Task Protokolle_neu_generieren_ohne_gespeichertes_Projekt_zeigt_Hinweis()
    {
        var dialogs = new DialogFake();
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Dialogs = dialogs
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var viewModel = new ImportPageViewModel(shell, services);

        await viewModel.ProtokollNeuGenerierenCommand.ExecuteAsync(null);

        Assert.Contains("zuerst speichern", dialogs.LastInfoMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Protokoll neu generieren", dialogs.LastInfoTitle);
    }

    [Fact]
    public async Task Protokolle_verteilen_ohne_gespeichertes_Projekt_zeigt_Hinweis()
    {
        var dialogs = new DialogFake();
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Dialogs = dialogs
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var viewModel = new ImportPageViewModel(shell, services);

        await viewModel.ImportSchachtPdfsFolderCommand.ExecuteAsync(null);

        Assert.Contains("Kein Projekt", dialogs.LastInfoMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Protokolle verteilen", dialogs.LastInfoTitle);
    }

    [Fact]
    public async Task Kanalprojekt_importieren_ohne_gespeichertes_Projekt_zeigt_Hinweis()
    {
        var dialogs = new DialogFake();
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Dialogs = dialogs
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var viewModel = new ImportPageViewModel(shell, services);

        await viewModel.ImportKanalProjektCommand.ExecuteAsync(null);

        Assert.Contains("zuerst", dialogs.LastInfoMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Import Kanalfernseh-Projekt", dialogs.LastInfoTitle);
    }

    [Fact]
    public void Importbericht_oeffnen_ohne_gespeichertes_Projekt_zeigt_Hinweis()
    {
        var dialogs = new DialogFake();
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Dialogs = dialogs
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var viewModel = new ImportPageViewModel(shell, services);

        viewModel.OpenReportFolderCommand.Execute(null);

        Assert.Contains("nicht vorhanden", dialogs.LastInfoMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Import-Berichte", dialogs.LastInfoTitle);
    }

    [Fact]
    public void Importbericht_exportieren_ohne_gespeichertes_Projekt_zeigt_Hinweis()
    {
        var dialogs = new DialogFake();
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory)
        {
            Dialogs = dialogs
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var viewModel = new ImportPageViewModel(shell, services);

        viewModel.ExportImportSummaryCommand.Execute(null);

        Assert.Contains("zuerst", dialogs.LastInfoMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Import-Report", dialogs.LastInfoTitle);
    }

    [Fact]
    public void Katalogstatus_wird_beim_Start_verstaendlich_gesetzt()
    {
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            _loggerFactory.CreateLogger("test"),
            _loggerFactory);
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));

        var viewModel = new ImportPageViewModel(shell, services);

        Assert.StartsWith("VSA-", viewModel.CatalogStatus, StringComparison.Ordinal);
        Assert.Equal(
            viewModel.CatalogStatus.StartsWith("VSA-2019-Katalog", StringComparison.Ordinal),
            viewModel.IsCatalogOk);
    }

    [Fact]
    public async Task Portabilitaets_Controller_uebernimmt_Ergebnis_und_speichert_Projekt()
    {
        var dialogs = new DialogFake();
        var service = new PortabilityFake();
        var controller = new ImportProjectPortabilityController(dialogs, service);
        var project = new Project();
        project.Data.Add(new HaltungRecord());
        var progress = string.Empty;
        var summary = string.Empty;
        var details = string.Empty;
        var saveCalls = 0;

        await controller.ExecuteAsync(new ImportProjectPortabilityActions(
            GetProjectFolder: () => @"C:\Projekt",
            GetProject: () => project,
            SaveProject: () =>
            {
                saveCalls++;
                return true;
            },
            SetProgress: value => progress = value,
            AppendSummary: value => summary += value,
            AppendDetails: value => details += value));

        Assert.Equal(1, service.Calls);
        Assert.Equal(1, saveCalls);
        Assert.Equal(string.Empty, progress);
        Assert.Contains("3 Pfade relativ", summary);
        Assert.Contains("Foto A", details);
        Assert.Contains("1:1", dialogs.LastInfoMessage);
    }

    [Fact]
    public async Task Fotozuordnungs_Controller_uebernimmt_Ergebnis_und_speichert_Projekt()
    {
        var dialogs = new DialogFake { SelectedFolder = @"C:\Fotos" };
        var service = new PhotoAssignmentFake();
        var controller = new ImportProjectPhotoAssignmentController(dialogs, service);
        var project = new Project();
        project.Data.Add(new HaltungRecord());
        var progress = string.Empty;
        var summary = string.Empty;
        var details = string.Empty;
        var saveCalls = 0;

        await controller.ExecuteAsync(new ImportProjectPhotoAssignmentActions(
            GetProjectFolder: () => @"C:\Projekt",
            GetProject: () => project,
            SaveProject: () =>
            {
                saveCalls++;
                return true;
            },
            SetProgress: value => progress = value,
            AppendSummary: value => summary += value,
            AppendDetails: value => details += value));

        Assert.Equal(1, service.Calls);
        Assert.Equal(1, saveCalls);
        Assert.Equal(string.Empty, progress);
        Assert.Contains("2 Haltungen", summary);
        Assert.Contains("Foto B", details);
        Assert.Equal("Fotos zuordnen", dialogs.LastInfoTitle);
    }

    [Fact]
    public async Task Protokoll_Controller_uebernimmt_Ergebnis_Status_und_speichert_Projekt()
    {
        var dialogs = new DialogFake();
        var service = new ProtocolRegenerationFake();
        var catalog = new CodeCatalogFake();
        var controller = new ImportProtocolRegenerationController(dialogs, service, catalog);
        var project = new Project();
        project.Data.Add(new HaltungRecord());
        var progress = string.Empty;
        var summary = string.Empty;
        var details = string.Empty;
        var status = string.Empty;
        var saveCalls = 0;

        await controller.ExecuteAsync(new ImportProtocolRegenerationActions(
            GetProjectFolder: () => @"C:\Projekt",
            GetProject: () => project,
            SaveProject: () =>
            {
                saveCalls++;
                return true;
            },
            SetProgress: value => progress = value,
            AppendSummary: value => summary += value,
            AppendDetails: value => details += value,
            SetStatus: value => status = value));

        Assert.Equal(1, service.Calls);
        Assert.Same(catalog, service.ReceivedCatalog);
        Assert.Equal(1, saveCalls);
        Assert.Equal(string.Empty, progress);
        Assert.Contains("2 Protokolle", summary);
        Assert.Contains("Haltung A", details);
        Assert.Equal("Eigene Protokolle neu generiert", status);
        Assert.Equal("Protokoll neu generieren", dialogs.LastInfoTitle);
    }

    [Fact]
    public async Task Protokollverteilungs_Controller_speichert_und_meldet_Dateifehler_sicher()
    {
        var dialogs = new DialogFake { SelectedFolder = @"C:\Quelle" };
        var distributor = new ProtocolDistributionFake();
        var logger = new CapturingLogger();
        var controller = new ImportProtocolDistributionController(dialogs, distributor, logger);
        var project = new Project();
        var collectionLock = new object();
        var saveCalls = 0;

        await controller.ExecuteAsync(new ImportProtocolDistributionActions(
            GetProjectFolder: () => @"C:\Projekt",
            GetProject: () => project,
            CollectionLock: collectionLock,
            SaveProject: () => saveCalls++));

        Assert.Equal(1, distributor.Calls);
        Assert.Equal(@"C:\Projekt", distributor.ProjectFolder);
        Assert.Equal(@"C:\Quelle", distributor.SourceFolder);
        Assert.Same(collectionLock, distributor.CollectionLock);
        Assert.True(project.Dirty);
        Assert.Equal(1, saveCalls);
        Assert.Contains("2 Haltungs-Protokolle", dialogs.LastInfoMessage);
        Assert.Contains("nicht.pdf", dialogs.LastInfoMessage);
        Assert.Contains("Tageslog", dialogs.LastInfoMessage);
        Assert.DoesNotContain("Zugriff verweigert", dialogs.LastInfoMessage);
        Assert.Contains(logger.Messages, message => message.Contains("Zugriff verweigert", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ein_Knopf_Import_Controller_speichert_Report_und_Ergebnis()
    {
        var dialogs = new DialogFake { SelectedFolder = @"C:\Quelle" };
        var importer = new OneClickImporterFake(OneClickProjectImportFormat.Kins);
        var reports = new OneClickReportWriterFake();
        var controller = new ImportOneClickProjectController(dialogs, () => importer, reports);
        var project = new Project();
        var collectionLock = new object();
        var progress = string.Empty;
        var summary = string.Empty;
        var details = string.Empty;
        var saveCalls = 0;

        await controller.ExecuteAsync(new ImportOneClickProjectActions(
            GetProjectFolder: () => @"C:\Projekt",
            GetProject: () => project,
            CollectionLock: collectionLock,
            SaveProject: () =>
            {
                saveCalls++;
                return true;
            },
            SetProgress: value => progress = value,
            AppendSummary: value => summary += value,
            AppendDetails: value => details += value));

        Assert.Equal(1, importer.Calls);
        Assert.Same(collectionLock, importer.Context?.CollectionLock);
        Assert.Equal(1, saveCalls);
        Assert.Equal(1, reports.Calls);
        Assert.Equal(string.Empty, progress);
        Assert.Contains("4 Haltungen", summary);
        Assert.Contains("Importmeldung", details);
        Assert.Equal("Import Kanalfernseh-Projekt", dialogs.LastInfoTitle);
    }

    [Fact]
    public async Task Ein_Knopf_Import_Controller_speichert_bei_unbekanntem_Format_nicht()
    {
        var dialogs = new DialogFake { SelectedFolder = @"C:\Quelle" };
        var importer = new OneClickImporterFake(OneClickProjectImportFormat.Unknown);
        var reports = new OneClickReportWriterFake();
        var controller = new ImportOneClickProjectController(dialogs, () => importer, reports);
        var saveCalls = 0;

        await controller.ExecuteAsync(new ImportOneClickProjectActions(
            GetProjectFolder: () => @"C:\Projekt",
            GetProject: () => new Project(),
            CollectionLock: new object(),
            SaveProject: () =>
            {
                saveCalls++;
                return true;
            },
            SetProgress: _ => { },
            AppendSummary: _ => { },
            AppendDetails: _ => { }));

        Assert.Equal(0, saveCalls);
        Assert.Equal(0, reports.Calls);
        Assert.Contains("nicht eindeutig", dialogs.LastInfoMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Importmeldung", dialogs.LastInfoMessage);
    }

    [Fact]
    public void Berichtsnavigation_oeffnet_den_zuletzt_erzeugten_Report_sicher()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "SewerStudio_ReportNavigation_" + Guid.NewGuid().ToString("N"));
        var reportDirectory = Path.Combine(root, "__IMPORT_REPORTS");
        var reportPath = Path.Combine(reportDirectory, "report.txt");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllText(reportPath, "Report");
        try
        {
            var dialogs = new DialogFake();
            string? openedPath = null;
            var controller = new ImportReportNavigationController(
                dialogs,
                () => Path.Combine(root, "projekt.json"),
                path =>
                {
                    openedPath = path;
                    return true;
                });
            controller.SetLastReportPath(reportPath);

            controller.OpenLastReport();

            Assert.Equal(reportPath, openedPath);
            Assert.Equal(reportDirectory, controller.GetReportDirectory());
            Assert.Equal(string.Empty, dialogs.LastInfoMessage);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Importbericht_Controller_uebernimmt_Pfad_und_Status()
    {
        var dialogs = new DialogFake();
        var exporter = new ImportSummaryExporterFake();
        var logger = new CapturingLogger();
        var controller = new ImportSummaryExportController(dialogs, exporter, logger);
        var lastResult = string.Empty;
        var status = string.Empty;

        controller.Execute(new ImportSummaryExportActions(
            GetProjectPath: () => @"C:\Projekt\projekt.json",
            GetProject: () => new Project(),
            SetLastResult: value => lastResult = value,
            SetStatus: value => status = value));

        Assert.Equal(1, exporter.Calls);
        Assert.Contains(exporter.ResultPath, lastResult);
        Assert.Equal("Import-Report erstellt", status);
        Assert.Equal(string.Empty, dialogs.LastErrorMessage);
    }

    [Fact]
    public void Importbericht_Controller_zeigt_keine_technischen_Fehler()
    {
        var dialogs = new DialogFake();
        var exporter = new ImportSummaryExporterFake
        {
            Error = new UnauthorizedAccessException("Geheimer Dateipfad")
        };
        var logger = new CapturingLogger();
        var controller = new ImportSummaryExportController(dialogs, exporter, logger);

        controller.Execute(new ImportSummaryExportActions(
            GetProjectPath: () => @"C:\Projekt\projekt.json",
            GetProject: () => new Project(),
            SetLastResult: _ => { },
            SetStatus: _ => { }));

        Assert.Contains("Tageslog", dialogs.LastErrorMessage);
        Assert.DoesNotContain("Geheimer", dialogs.LastErrorMessage);
        Assert.Contains(logger.Messages, message => message.Contains("Geheimer Dateipfad", StringComparison.Ordinal));
    }

    [Fact]
    public void Katalog_Controller_erkennt_gemeinsam_aufgeloesten_SEC_NOD_Katalog()
    {
        var controller = new ImportCatalogController(
            getConfiguredSecPath: () => null,
            getConfiguredNodPath: () => null,
            getResolvedPath: () => @"C:\SEC.xml | C:\NOD.xml",
            new CodeCatalogFake(),
            new CapturingLogger());

        var status = controller.GetStatus();

        Assert.True(status.IsOk);
        Assert.Contains("SEC+NOD", status.Text);
    }

    [Fact]
    public void Katalog_Controller_meldet_fehlenden_NOD_Katalog()
    {
        var controller = new ImportCatalogController(
            getConfiguredSecPath: () => null,
            getConfiguredNodPath: () => @"C:\fehlt_NOD.xml",
            getResolvedPath: () => null,
            new CodeCatalogFake(),
            new CapturingLogger());

        var status = controller.GetStatus();

        Assert.False(status.IsOk);
        Assert.Contains("NOD", status.Text);
        Assert.Contains("nicht gefunden", status.Text);
    }

    [Fact]
    public async Task Vsa_Nachlauf_Controller_meldet_erfolgreiche_Bewertung()
    {
        var service = new VsaEvaluationFake(success: true);
        var controller = new ImportVsaEvaluationController(service, new CapturingLogger());
        var project = new Project();
        project.Data.Add(new HaltungRecord());
        var progress = string.Empty;
        var summary = string.Empty;

        await controller.ExecuteAsync(
            project,
            "PDF",
            new ImportVsaEvaluationActions(
                SetProgress: value => progress = value,
                AppendSummary: value => summary += value));

        Assert.Equal(1, service.Calls);
        Assert.Contains("PDF", progress);
        Assert.Contains("1 Haltungen bewertet", summary);
    }

    [Fact]
    public async Task Vsa_Nachlauf_Controller_verbirgt_technischen_Fehler()
    {
        var service = new VsaEvaluationFake(success: false);
        var logger = new CapturingLogger();
        var controller = new ImportVsaEvaluationController(service, logger);
        var summary = string.Empty;

        await controller.ExecuteAsync(
            new Project(),
            "XTF",
            new ImportVsaEvaluationActions(
                SetProgress: _ => { },
                AppendSummary: value => summary += value));

        Assert.Contains("Tageslog", summary);
        Assert.DoesNotContain("Interner VSA-Fehler", summary);
        Assert.Contains(logger.Messages, message => message.Contains("Interner VSA-Fehler", StringComparison.Ordinal));
    }

    public void Dispose() => _loggerFactory.Dispose();

    private sealed class PortabilityFake : IProjectPortabilityService
    {
        public int Calls { get; private set; }

        public ProjectPortabilityResult MakePortable(string projectFolder, Project project, bool dryRun = false)
        {
            Calls++;
            return new ProjectPortabilityResult(
                RelinkedPaths: 3,
                FotosCopied: 2,
                Unresolved: 1,
                Messages: new[] { "Foto A" });
        }
    }

    private sealed class PhotoAssignmentFake : IProjectPhotoAssignmentService
    {
        public int Calls { get; private set; }

        public ProjectPhotoAssignmentResult AssignFromFolder(
            string projectFolder,
            string sourceFolder,
            Project project)
        {
            Calls++;
            return new ProjectPhotoAssignmentResult(
                HoldingsMatched: 2,
                PhotosAssigned: 3,
                PhotosCopied: 4,
                UnmatchedFiles: 1,
                Messages: new[] { "Foto B" });
        }
    }

    private sealed class ProtocolDistributionFake : INameBasedProtocolDistributor
    {
        public int Calls { get; private set; }
        public string ProjectFolder { get; private set; } = string.Empty;
        public string SourceFolder { get; private set; } = string.Empty;
        public object? CollectionLock { get; private set; }

        public ProtocolDistributionReport Distribute(
            Project project,
            string projectFolder,
            string sourceFolder,
            object? collectionLock = null)
        {
            Calls++;
            ProjectFolder = projectFolder;
            SourceFolder = sourceFolder;
            CollectionLock = collectionLock;
            return new ProtocolDistributionReport(
                HaltungProtokolle: 2,
                SchachtProtokolle: 3,
                SchaechteAngelegt: 1,
                NichtZugeordnet: new[] { "nicht.pdf" },
                Meldungen: new[] { "fehler.pdf: Zugriff verweigert" });
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(
                formatter(state, exception)
                + (exception is null ? string.Empty : " " + exception));
    }

    private sealed class ProtocolRegenerationFake : IProtocolRegenerationService
    {
        public int Calls { get; private set; }
        public ICodeCatalogProvider? ReceivedCatalog { get; private set; }

        public ProtocolRegenerationResult RegenerateAll(
            Project project,
            string projectFolder,
            ICodeCatalogProvider? codeCatalog = null)
        {
            Calls++;
            ReceivedCatalog = codeCatalog;
            return new ProtocolRegenerationResult(
                Generated: 2,
                Errors: 1,
                Messages: new[] { "Haltung A" });
        }
    }

    private sealed class OneClickImporterFake : IOneClickProjectImportService
    {
        private readonly OneClickProjectImportFormat _format;

        public OneClickImporterFake(OneClickProjectImportFormat format)
        {
            _format = format;
        }

        public int Calls { get; private set; }
        public ImportRunContext? Context { get; private set; }

        public OneClickProjectImportResult Import(
            string sourceFolder,
            string projectFolder,
            Project project,
            ImportRunContext? context = null)
        {
            Calls++;
            Context = context;
            return new OneClickProjectImportResult(
                _format,
                Found: 4,
                Created: 2,
                Updated: 2,
                Errors: 1,
                Conflicts: 3,
                Messages: new[] { "Importmeldung" });
        }
    }

    private sealed class OneClickReportWriterFake : IOneClickImportReportWriter
    {
        public int Calls { get; private set; }

        public void TryWrite(string projectFolder, OneClickProjectImportResult result)
            => Calls++;
    }

    private sealed class ImportSummaryExporterFake : IImportSummaryExporter
    {
        public int Calls { get; private set; }
        public string ResultPath { get; init; } = @"C:\Projekt\__IMPORT_REPORTS\import.csv";
        public Exception? Error { get; init; }

        public string Export(string projectPath, Project project)
        {
            Calls++;
            if (Error is not null)
                throw Error;
            return ResultPath;
        }
    }

    private sealed class VsaEvaluationFake : IVsaEvaluationService
    {
        private readonly bool _success;

        public VsaEvaluationFake(bool success)
        {
            _success = success;
        }

        public int Calls { get; private set; }

        public Result<IReadOnlyList<VsaConditionResult>> Evaluate(Project project)
        {
            Calls++;
            return _success
                ? Result<IReadOnlyList<VsaConditionResult>>.Success([])
                : Result<IReadOnlyList<VsaConditionResult>>.Fail("VSA-01", "Interner VSA-Fehler");
        }

        public Result<bool> EvaluateRecord(HaltungRecord record)
            => Result<bool>.Success(true);

        public Result<string> Explain(Project project, HaltungRecord record)
            => Result<string>.Success(string.Empty);
    }

    private sealed class CodeCatalogFake : ICodeCatalogProvider
    {
        public IReadOnlyList<CodeDefinition> GetAll() => [];
        public bool TryGet(string code, out CodeDefinition def)
        {
            def = new CodeDefinition();
            return false;
        }

        public void Save(IReadOnlyList<CodeDefinition> codes) { }
        public IReadOnlyList<string> AllowedCodes() => [];
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => [];
    }

    private sealed class DialogFake : IDialogService
    {
        public string? SelectedFolder { get; init; }
        public string LastInfoMessage { get; private set; } = string.Empty;
        public string LastInfoTitle { get; private set; } = string.Empty;
        public string LastErrorMessage { get; private set; } = string.Empty;
        public string LastErrorTitle { get; private set; } = string.Empty;

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => SelectedFolder;
        public void Info(string message, string title = "Hinweis")
        {
            LastInfoMessage = message;
            LastInfoTitle = title;
        }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler")
        {
            LastErrorMessage = message;
            LastErrorTitle = title;
        }
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }
}

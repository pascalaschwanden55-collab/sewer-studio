using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.Dossiers;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der Ablauf des Dossier-Cockpits, durchgespielt ohne echte Fenster.
///
/// Bis die acht Fenster hinter <see cref="IDossierDialogs"/> lagen, war genau
/// das unmoeglich: die Pruefungen kamen nicht weiter als bis zu den
/// Textbausteinen. Ruecksetzen nach misslungenem Speichern liess sich nur am
/// Quelltext ablesen, nicht ausprobieren.
/// </summary>
public sealed class DossiersPageActionFlowTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "dossier_ablauf_" + Guid.NewGuid().ToString("N"));

    private readonly FakeStore _store = new();
    private readonly FakeDialogs _fenster = new();
    private readonly Project _project = new();

    // Eigene Einstellungsdatei je Test: der Merker des Kopfblocks wird
    // beim Umschalten gespeichert, und das darf die echte nicht anfassen.
    private readonly AuswertungPro.Next.UI.AppSettings _einstellungen = new();

    public DossiersPageActionFlowTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private DossiersPageViewModel BaueCockpit(
        IDossierAttachmentService? attachments = null,
        IDossierPdfAssemblyService? pdfAssembly = null)
    {
        var vm = new DossiersPageViewModel(
            getProject: () => _project,
            getProjectFolder: () => _root,
            getProjectFilePath: () => Path.Combine(_root, "projekt.json"),
            store: _store,
            wordExport: new NichtGebraucht(),
            attachments: attachments ?? new NichtGebraucht(),
            pdfAssembly: pdfAssembly ?? new NichtGebraucht(),
            dialogWindows: _fenster,
            costStores: new LeereKosten(),
            dialogs: new StilleDialoge(),
            toasts: new ToastService(),
            shellOpen: new NichtsOeffnen(),
            explorerReveal: new NichtsZeigen(),
            holdingActions: new DossierHoldingActionController(
                () => _project, new StilleDialoge(), _ => { }, _ => { }, _ => { }),
            shaftActions: new DossierShaftActionController(
                () => _project, new StilleDialoge(), _ => { }, _ => { }),
            settings: _einstellungen);

        // Der Konstruktor laedt im Hintergrund; abwarten.
        SpinWait.SpinUntil(() => _store.Geladen, TimeSpan.FromSeconds(5));
        return vm;
    }

    private void Schacht(string nummer)
    {
        var s = new SchachtRecord();
        s.SetFieldValue("Schachtnummer", nummer);
        _project.SchaechteData.Add(s);
    }

    private HaltungRecord Haltung(string nummer)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(
            FieldKeys.HoldingName,
            nummer,
            FieldSource.Manual,
            userEdited: false);
        _project.Data.Add(record);
        return record;
    }

    [Fact]
    public async Task Neue_Liegenschaft_erhaelt_vor_dem_Speichern_ihren_Ordnernamen()
    {
        var definition = new DossierDefinition { Name = "Musterweg 1" };
        _fenster.NeueLiegenschaft = new DossierParcelLookupChoice(
            definition,
            Array.Empty<string>(),
            Array.Empty<string>());
        _fenster.StammdatenAenderung = _ => { };

        var vm = BaueCockpit();

        await vm.NewDossierCommand.ExecuteAsync(null);

        var gespeichert = Assert.Single(_store.Dokument.Dossiers);
        Assert.Equal("Musterweg 1", gespeichert.FolderName);
        Assert.Equal(1, _store.Speicherlaeufe);
    }

    [Fact]
    public async Task Eine_gewaehlte_Schachtauswahl_wird_gespeichert()
    {
        Schacht("80551");
        Schacht("36051");

        var dossier = new DossierDefinition { Name = "Musterweg 1", ShaftNumbers = { "80551" } };
        _store.Dokument.Dossiers.Add(dossier);

        var vm = BaueCockpit();
        _fenster.SchachtAuswahl = new List<string> { "80551", "36051" };

        await vm.EditShaftsCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "80551", "36051" }, dossier.ShaftNumbers);
        Assert.Equal(2, vm.ShaftRows.Count);
        Assert.Equal("2 Schächte zugeordnet.", vm.StatusMessage);
    }

    [Fact]
    public async Task Ein_misslungenes_Speichern_laesst_die_alte_Schachtauswahl_stehen()
    {
        Schacht("80551");
        Schacht("36051");

        var dossier = new DossierDefinition { Name = "Musterweg 1", ShaftNumbers = { "80551" } };
        _store.Dokument.Dossiers.Add(dossier);

        var vm = BaueCockpit();
        _fenster.SchachtAuswahl = new List<string> { "80551", "36051" };
        _store.SpeichernScheitert = true;

        await vm.EditShaftsCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "80551" }, dossier.ShaftNumbers);
        Assert.Contains("Speichern fehlgeschlagen", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_Abbruch_im_Auswahlfenster_aendert_nichts()
    {
        Schacht("80551");

        var dossier = new DossierDefinition { Name = "Musterweg 1", ShaftNumbers = { "80551" } };
        _store.Dokument.Dossiers.Add(dossier);

        var vm = BaueCockpit();
        _fenster.SchachtAuswahl = null;   // abgebrochen

        await vm.EditShaftsCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "80551" }, dossier.ShaftNumbers);
        Assert.Equal(0, _store.Speicherlaeufe);
    }

    [Fact]
    public async Task Ein_misslungenes_Speichern_der_Stammdaten_setzt_den_Namen_zurueck()
    {
        var dossier = new DossierDefinition { Name = "Alter Name" };
        _store.Dokument.Dossiers.Add(dossier);

        var vm = BaueCockpit();
        _fenster.StammdatenAenderung = d => d.Name = "Neuer Name";
        _store.SpeichernScheitert = true;

        await vm.EditDossierCommand.ExecuteAsync(null);

        Assert.Equal("Alter Name", _store.Dokument.Dossiers[0].Name);
    }

    [Fact]
    public async Task Ein_misslungenes_Speichern_der_Gebietsangaben_setzt_sie_zurueck()
    {
        _store.Dokument.Area.AreaTitle = "Erstfeld West";

        var vm = BaueCockpit();
        _fenster.GebietsAenderung = a => a.AreaTitle = "Verstellt";
        _store.SpeichernScheitert = true;

        await vm.EditAreaCommand.ExecuteAsync(null);

        Assert.Equal("Erstfeld West", _store.Dokument.Area.AreaTitle);
    }

    [Fact]
    public void Die_Liste_zeigt_die_gespeicherte_Reihenfolge_statt_alphabetisch_zu_sortieren()
    {
        _store.Dokument.Dossiers.Add(new DossierDefinition { Name = "Zweite Anzeige" });
        _store.Dokument.Dossiers.Add(new DossierDefinition { Name = "Erste Anzeige" });

        var vm = BaueCockpit();
        SpinWait.SpinUntil(() => vm.Dossiers.Count == 2, TimeSpan.FromSeconds(5));

        Assert.Equal(
            new[] { "Zweite Anzeige", "Erste Anzeige" },
            vm.Dossiers.Select(dossier => dossier.Name));
    }

    [Fact]
    public async Task Eine_Liegenschaft_kann_nach_unten_verschoben_und_gespeichert_werden()
    {
        var erste = new DossierDefinition { Name = "Liegenschaft A" };
        var zweite = new DossierDefinition { Name = "Liegenschaft B" };
        _store.Dokument.Dossiers.Add(erste);
        _store.Dokument.Dossiers.Add(zweite);

        var vm = BaueCockpit();
        SpinWait.SpinUntil(() => vm.Dossiers.Count == 2, TimeSpan.FromSeconds(5));
        vm.Selected = vm.Dossiers[0];

        await vm.MoveDossierDownCommand.ExecuteAsync(null);

        Assert.Equal(new[] { zweite, erste }, _store.Dokument.Dossiers);
        Assert.Equal(erste.Id, vm.Selected?.Id);
        Assert.Equal(1, _store.Speicherlaeufe);
    }

    [Fact]
    public async Task Misslungenes_Speichern_setzt_die_Reihenfolge_zurueck()
    {
        var erste = new DossierDefinition { Name = "Liegenschaft A" };
        var zweite = new DossierDefinition { Name = "Liegenschaft B" };
        _store.Dokument.Dossiers.Add(erste);
        _store.Dokument.Dossiers.Add(zweite);

        var vm = BaueCockpit();
        SpinWait.SpinUntil(() => vm.Dossiers.Count == 2, TimeSpan.FromSeconds(5));
        vm.Selected = vm.Dossiers[0];
        _store.SpeichernScheitert = true;

        await vm.MoveDossierDownCommand.ExecuteAsync(null);

        Assert.Equal(new[] { erste, zweite }, _store.Dokument.Dossiers);
        Assert.Equal(erste.Id, vm.Selected?.Id);
        Assert.Contains("Speichern fehlgeschlagen", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Fehlt_ein_Schacht_im_Projekt_wird_gewarnt_statt_still_weggelassen()
    {
        Schacht("80551");

        var dossier = new DossierDefinition
        {
            Name = "Musterweg 1",
            ShaftNumbers = { "80551", "gibt-es-nicht" }
        };
        _store.Dokument.Dossiers.Add(dossier);

        var vm = BaueCockpit();

        Assert.Single(vm.ShaftRows);
        Assert.True(vm.HasMissingWarning);
        Assert.Contains("gibt-es-nicht", vm.MissingWarning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Alles_zu_einem_Pdf_sammelt_zuerst_alle_ausgewaehlten_Protokolle()
    {
        var haltungA = Haltung("100-200");
        var haltungB = Haltung("200-300");
        Schacht("100");
        Schacht("200");
        _store.Dokument.Dossiers.Add(new DossierDefinition
        {
            Name = "Musterweg 1",
            HoldingIds = { haltungA.Id, haltungB.Id },
            ShaftNumbers = { "100", "200" }
        });
        var flow = new RecordingPdfFlow();
        var vm = BaueCockpit(flow, flow);

        await vm.AssemblePdfCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Sammeln", "Zusammenführen" }, flow.Calls);
        Assert.Equal(2, flow.LastSnapshot!.HoldingCount);
        Assert.Equal(2, flow.LastSnapshot.ShaftCount);
    }

    [Fact]
    public async Task Vor_dem_Gesamt_Pdf_werden_die_Blaetter_gezeigt()
    {
        // Pascal will jedes Blatt sehen und einzeln abwaehlen koennen, bevor
        // die Datei entsteht.
        var haltung = Haltung("100-200");
        _store.Dokument.Dossiers.Add(new DossierDefinition
        {
            Name = "Musterweg 1",
            HoldingIds = { haltung.Id }
        });

        var flow = new RecordingPdfFlow();
        var vm = BaueCockpit(flow, flow);

        await vm.AssemblePdfCommand.ExecuteAsync(null);

        Assert.True(flow.WurdeNachBlaetternGefragt, "Es wurde nicht nach den Blaettern gefragt.");
        Assert.Equal(1, _fenster.BlattFragen);
    }

    [Fact]
    public async Task Fehlendes_Protokoll_stoppt_ein_unvollstaendiges_Gesamt_Pdf()
    {
        var haltung = Haltung("100-200");
        _store.Dokument.Dossiers.Add(new DossierDefinition
        {
            Name = "Musterweg 1",
            HoldingIds = { haltung.Id }
        });
        var flow = new RecordingPdfFlow
        {
            AttachmentResult = new DossierAttachmentResult(
                new[]
                {
                    new DossierAttachment(
                        string.Empty,
                        string.Empty,
                        DossierAttachmentKind.Missing,
                        "100-200")
                },
                new[] { "Haltung '100-200': kein Protokoll-PDF gefunden." })
        };
        var vm = BaueCockpit(flow, flow);

        await vm.AssemblePdfCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Sammeln" }, flow.Calls);
        Assert.Contains("nicht erstellt", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verschwundenes_Dossier_nimmt_neu_veroeffentlichten_Plan_zurueck()
    {
        var dossier = new DossierDefinition { Name = "Musterweg 1" };
        _store.Dokument.Dossiers.Add(dossier);
        var publication = new FakePlanPublication();

        var vm = BaueCockpit();
        _fenster.VorschauUebernahme = new DossierPreviewChoice(
            new DossierAreaSettings(),
            DossierDeepCopy.Of(dossier),
            publication);
        _fenster.VorVorschauRueckgabe = () => _store.Dokument.Dossiers.Clear();

        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.Equal(1, publication.RollbackCalls);
        Assert.Equal(0, publication.AcceptCalls);
        Assert.Equal(0, _store.Speicherlaeufe);
        Assert.Contains("verschwunden", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Speicherfehler_nimmt_nur_die_neue_Planveroeffentlichung_zurueck()
    {
        var dossier = new DossierDefinition
        {
            Name = "Musterweg 1",
            OverviewPlanPath = "alter-plan.png"
        };
        _store.Dokument.Dossiers.Add(dossier);
        var publication = new FakePlanPublication();
        var bearbeitet = DossierDeepCopy.Of(dossier);
        bearbeitet.OverviewPlanPath = "neuer-plan.png";

        var vm = BaueCockpit();
        _fenster.VorschauUebernahme = new DossierPreviewChoice(
            new DossierAreaSettings(),
            bearbeitet,
            publication);
        _store.SpeichernScheitert = true;

        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.Equal(1, publication.RollbackCalls);
        Assert.Equal(0, publication.AcceptCalls);
        Assert.Equal("alter-plan.png", _store.Dokument.Dossiers[0].OverviewPlanPath);
        Assert.Contains("bleiben wie vorher", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Erfolgreiches_Speichern_bestaetigt_die_neue_Planveroeffentlichung()
    {
        var dossier = new DossierDefinition { Name = "Musterweg 1" };
        _store.Dokument.Dossiers.Add(dossier);
        var publication = new FakePlanPublication();
        var bearbeitet = DossierDeepCopy.Of(dossier);
        bearbeitet.OverviewPlanPath = "neuer-plan.png";

        var vm = BaueCockpit();
        _fenster.VorschauUebernahme = new DossierPreviewChoice(
            new DossierAreaSettings(),
            bearbeitet,
            publication);

        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.Equal(1, publication.AcceptCalls);
        Assert.Equal(0, publication.RollbackCalls);
        Assert.Equal("neuer-plan.png", _store.Dokument.Dossiers[0].OverviewPlanPath);
    }

    // ── Attrappen ─────────────────────────────────────────────────────────

    private sealed class FakeDialogs : IDossierDialogs
    {
        public List<string>? SchachtAuswahl { get; set; }
        public List<Guid>? LeitungsAuswahl { get; set; }
        public Action<DossierDefinition>? StammdatenAenderung { get; set; }
        public Action<DossierAreaSettings>? GebietsAenderung { get; set; }
        public DossierParcelLookupChoice? NeueLiegenschaft { get; set; }
        public DossierPreviewChoice? VorschauUebernahme { get; set; }
        public Action? VorVorschauRueckgabe { get; set; }

        public DossierParcelLookupChoice? NewProperty(
            IReadOnlyDictionary<string, Guid> holdingIdsByName,
            IReadOnlyList<string> projectShaftNumbers) => NeueLiegenschaft;

        public bool EditDossier(DossierDefinition definition, bool isNew)
        {
            if (StammdatenAenderung is null)
                return false;

            StammdatenAenderung(definition);
            return true;
        }

        public bool EditArea(DossierAreaSettings area)
        {
            if (GebietsAenderung is null)
                return false;

            GebietsAenderung(area);
            return true;
        }

        public IReadOnlyList<DossierDefinition> CreateFromProject(
            IReadOnlyList<string> projectHoldingNames,
            IReadOnlyDictionary<string, Guid> holdingIdsByName,
            IReadOnlyList<string> projectShaftNumbers,
            IReadOnlyList<string> parcelsWithDossier) => Array.Empty<DossierDefinition>();

        public List<Guid>? PickHoldings(Project project, IReadOnlyCollection<Guid> chosen)
            => LeitungsAuswahl;

        public List<string>? PickShafts(Project project, IReadOnlyCollection<string> chosen)
            => SchachtAuswahl;

        public DossierPreviewChoice? Preview(
            DossierExportRequest request, string templatePath)
        {
            VorVorschauRueckgabe?.Invoke();
            return VorschauUebernahme;
        }

        public DossierRefreshChoice? Refresh(string dossierName, DossierRefreshProposal proposal)
            => null;

        /// <summary>Was die Blattauswahl antworten soll — Standard: alles behalten.</summary>
        public IReadOnlySet<int>? BlattAntwort { get; set; } = new HashSet<int>();

        public int BlattFragen { get; private set; }

        public IReadOnlySet<int>? ChoosePages(byte[] pdf)
        {
            BlattFragen++;
            return BlattAntwort;
        }
    }

    private sealed class FakePlanPublication : IDossierPlanPublication
    {
        private bool _finished;

        public string PublishedPath => "neuer-plan.png";

        public int AcceptCalls { get; private set; }

        public int RollbackCalls { get; private set; }

        public void Accept()
        {
            if (_finished)
                return;

            _finished = true;
            AcceptCalls++;
        }

        public DossierPlanRollbackResult Rollback()
        {
            if (_finished)
                return DossierPlanRollbackResult.Ok();

            _finished = true;
            RollbackCalls++;
            return DossierPlanRollbackResult.Ok();
        }

        public void Dispose()
            => _ = Rollback();
    }

    private sealed class FakeStore : IDossierStore
    {
        public DossierDocument Dokument { get; } = new();
        public bool SpeichernScheitert { get; set; }
        public bool Geladen { get; private set; }
        public int Speicherlaeufe { get; private set; }

        public Task<DossierDocument> LoadAsync(string projectRoot, CancellationToken ct = default)
        {
            Geladen = true;
            return Task.FromResult(Dokument);
        }

        public Task SaveAsync(
            string projectRoot, DossierDocument document, CancellationToken ct = default)
        {
            Speicherlaeufe++;
            return SpeichernScheitert
                ? Task.FromException(new IOException("Platte voll"))
                : Task.CompletedTask;
        }
    }

    private sealed class NichtGebraucht
        : IDossierWordExportService, IDossierAttachmentService, IDossierPdfAssemblyService
    {
        public Task<DossierWordExportResult> ExportAsync(
            DossierExportRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<DossierAttachmentResult> CollectAsync(
            DossierExportRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<DossierPdfAssemblyResult> AssembleAsync(
            DossierExportRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<DossierPdfAssemblyResult> AssembleAsync(
            string dossierFolder,
            Func<byte[], CancellationToken, Task<IReadOnlySet<int>?>>? waehleSeiten = null,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingPdfFlow
        : IDossierAttachmentService, IDossierPdfAssemblyService
    {
        public List<string> Calls { get; } = new();
        public DossierSnapshot? LastSnapshot { get; private set; }
        public DossierAttachmentResult AttachmentResult { get; set; }
            = new(Array.Empty<DossierAttachment>(), Array.Empty<string>());

        public Task<DossierAttachmentResult> CollectAsync(
            DossierExportRequest request,
            CancellationToken ct = default)
        {
            Calls.Add("Sammeln");
            LastSnapshot = request.Snapshot;
            return Task.FromResult(AttachmentResult);
        }

        /// <summary>Wurde vor dem Schreiben nach den Blaettern gefragt?</summary>
        public bool WurdeNachBlaetternGefragt { get; private set; }

        public async Task<DossierPdfAssemblyResult> AssembleAsync(
            string dossierFolder,
            Func<byte[], CancellationToken, Task<IReadOnlySet<int>?>>? waehleSeiten = null,
            CancellationToken ct = default)
        {
            Calls.Add("Zusammenführen");

            if (waehleSeiten is not null)
            {
                WurdeNachBlaetternGefragt = true;
                await waehleSeiten(Array.Empty<byte>(), ct).ConfigureAwait(false);
            }

            return new DossierPdfAssemblyResult(
                true,
                Path.Combine(dossierFolder, "Eigentuemerdossier_komplett.pdf"),
                "Gesamt-PDF erstellt.");
        }
    }

    private sealed class LeereKosten : ICostStoreFactory
    {
        public IProjectCostStoreRepository CreateProjectCostStore(string fileName = "costs.json")
            => new LeeresRepository();

        public ICostCatalogStore CreateCostCatalogStore(string? userOverridePath = null)
            => throw new NotSupportedException();

        public IMeasureTemplateStore CreateMeasureTemplateStore(string? userOverridePath = null)
            => throw new NotSupportedException();

        public IPositionTemplateStore CreatePositionTemplateStore(string? userOverridePath = null)
            => throw new NotSupportedException();

        public CostCalculationStores CreateCalculationStores(
            string projectCostFileName = "costs.json")
            => throw new NotSupportedException();

        private sealed class LeeresRepository : IProjectCostStoreRepository
        {
            public ProjectCostStore Load(string? projectPath) => new();

            public ProjectCostStore Load(string? projectPath, out string? loadError)
            {
                loadError = null;
                return new ProjectCostStore();
            }

            public void Save(string? projectPath, ProjectCostStore store) { }

            public bool Save(string? projectPath, ProjectCostStore store, out string? saveError)
            {
                saveError = null;
                return true;
            }

            public string GetStorePath(string projectPath) => projectPath;
        }
    }

    private sealed class StilleDialoge : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;

        public string? SaveFile(
            string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => null;

        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();

        public string? SelectFolder(string title, string? initialPath = null) => null;

        public void Info(string message, string title = "Hinweis") { }

        public void Warn(string message, string title = "Warnung") { }

        public void Error(string message, string title = "Fehler") { }

        public bool Confirm(string message, string title = "Bestaetigung") => true;

        public bool ConfirmWarn(
            string message, string title = "Bestaetigung", bool defaultNo = true) => true;

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
            => DialogConfirm.Yes;
    }

    private sealed class NichtsOeffnen : ISafeShellOpenService
    {
        public bool TryOpen(string? path, out string? error)
        {
            error = null;
            return true;
        }
    }

    private sealed class NichtsZeigen : IExplorerRevealService
    {
        public bool TryReveal(string? targetPath, out string? error)
        {
            error = null;
            return true;
        }
    }
}

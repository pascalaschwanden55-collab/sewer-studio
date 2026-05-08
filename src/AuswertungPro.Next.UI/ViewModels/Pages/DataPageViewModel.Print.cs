using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.Hydraulik;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

// PDF-Druck-Befehle: AWU-Haltungsprotokoll, Hydraulik-Bericht, Haltungs-
// Dossier (mit optionaler Hydraulik-Berechnung, Kosten, Originalprotokollen,
// historischer Referenz). Helper EnsureProtocolDocumentForPdf stellt
// sicher dass ein ProtocolDocument fuer die Erzeugung vorhanden ist.
public sealed partial class DataPageViewModel
{
    private void PrintAwuHaltungsprotokollPdf(HaltungRecord? record)
    {
        if (record is null)
        {
            _dialogs.ShowMessage("Bitte zuerst eine Haltung auswaehlen.", "Haltungsprotokoll AWU", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var doc = EnsureProtocolDocumentForPdf(record);
        var holding = record.GetFieldValue("Haltungsname");
        var defaultName = $"Haltungsprotokoll_AWU_{SanitizeFilenamePart(holding)}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = App.Resolve<IDialogService>().SaveFile(
            "Haltungsprotokoll AWU als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = new Application.Reports.HaltungsprotokollPdfOptions
            {
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null
            };

            var projectFolder = _shell.GetProjectFolder() ?? string.Empty;
            var pdf = App.Resolve<AuswertungPro.Next.Application.Reports.ProtocolPdfExporter>().BuildHaltungsprotokollPdf(
                _shell.Project,
                record,
                doc,
                projectFolder,
                options);

            File.WriteAllBytes(output, pdf);
            _dialogs.ShowMessage($"AWU-Haltungsprotokoll wurde erstellt:\n{output}", "Haltungsprotokoll AWU", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage($"AWU-Haltungsprotokoll konnte nicht erstellt werden:\n{ex.Message}", "Haltungsprotokoll AWU", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private ProtocolDocument EnsureProtocolDocumentForPdf(HaltungRecord record)
    {
        if (record.Protocol is not null)
        {
            record.Protocol.Current ??= new ProtocolRevision
            {
                Comment = "Arbeitskopie",
                Entries = new List<ProtocolEntry>()
            };

            if ((record.Protocol.Original.Entries.Count == 0)
                && (record.Protocol.Current.Entries.Count == 0)
                && record.VsaFindings is { Count: > 0 })
            {
                var imported = BuildEntriesFromFindings(record.VsaFindings);
                record.Protocol = App.Resolve<AuswertungPro.Next.Application.Protocol.IProtocolService>().EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", imported, null);
            }

            return record.Protocol;
        }

        var entries = record.VsaFindings is { Count: > 0 }
            ? BuildEntriesFromFindings(record.VsaFindings)
            : Array.Empty<ProtocolEntry>();
        return App.Resolve<AuswertungPro.Next.Application.Protocol.IProtocolService>().EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", entries, null);
    }

    private async void PrintHydraulikPdf(HaltungRecord? record)
    {
        if (record is null)
        {
            _dialogs.ShowMessage("Bitte zuerst eine Haltung auswaehlen.", "Hydraulik PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Build input from record
        var dn = TryParseDnMm(record.GetFieldValue("DN_mm")) ?? 300;
        var materialRaw = record.GetFieldValue("Rohrmaterial") ?? "";
        var vm = new HydraulikPanelViewModel();
        vm.LoadFromRecord(dn, materialRaw, null);

        var mat = vm.SelectedMaterial;
        double kb = vm.IsNeuzustand ? mat.KbNeu : mat.KbAlt;
        double wasserstand = dn / 2; // default half-fill

        var input = new HydraulikInput(
            DN_mm: dn,
            Wasserstand_mm: wasserstand,
            Gefaelle_Promille: vm.Gefaelle,
            Kb: kb,
            AbwasserTyp: "MR",
            Temperatur_C: vm.Temperatur);

        var result = HydraulikEngine.Berechne(input);
        if (result is null)
        {
            _dialogs.ShowMessage("Hydraulik-Berechnung konnte nicht durchgefuehrt werden.\nBitte DN und Gefaelle pruefen.", "Hydraulik PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Phase 1.4 Followup: Generischer PrintOptionsDialog ersetzt HydraulikPrintDialog.
        var dialog = new PrintOptionsDialog(PrintDialogFactory.CreateHydraulikConfig());
        dialog.Owner = System.Windows.Application.Current?.MainWindow;
        if (App.Resolve<IDialogService>().ShowDialog(dialog) != true)
            return;
        var hydraulikOpts = PrintDialogFactory.ToHydraulikOptions(dialog.GetSelectedOptions());

        // SaveFile dialog
        var holding = record.GetFieldValue("Haltungsname") ?? "Haltung";
        var defaultName = $"Hydraulik_{SanitizeFilenamePart(holding)}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = App.Resolve<IDialogService>().SaveFile(
            "Hydraulik-Bericht als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = hydraulikOpts with
            {
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null
            };

            var calc = new Application.Reports.HydraulikCalcResult
            {
                DN_mm = input.DN_mm,
                Wasserstand_mm = input.Wasserstand_mm,
                Gefaelle_Promille = input.Gefaelle_Promille,
                Kb = input.Kb,
                AbwasserTyp = input.AbwasserTyp,
                Temperatur_C = input.Temperatur_C,
                Material = mat.Label,
                V_T = result.V_T,
                Q_T = result.Q_T,
                A_T = result.A_T,
                Lu_T = result.Lu_T,
                Rhy_T = result.Rhy_T,
                Bsp = result.Bsp,
                V_V = result.V_V,
                Q_V = result.Q_V,
                Re = result.Re,
                Fr = result.Fr,
                Lambda = result.Lambda,
                Tau = result.Tau,
                Ny = result.Ny,
                Vc = result.Abl.Vc,
                Ic = result.Abl.Ic,
                TauC = result.Abl.TauC,
                Auslastung = result.Auslastung,
                VelocityOk = result.VelocityOk,
                ShearOk = result.ShearOk,
                FroudeOk = result.Fr <= 1,
                AblagerungOk = result.AblagerungOk,
            };

            // PDF-Erzeugung auf Background-Thread (verhindert UI-Freeze)
            var pdf = await Task.Run(() => Application.Reports.HydraulikPdfBuilder.Build(record, calc, options));
            await Task.Run(() => File.WriteAllBytes(output, pdf));

            _dialogs.ShowMessage($"PDF wurde erstellt:\n{output}", "Hydraulik PDF", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage($"PDF konnte nicht erstellt werden:\n{ex.Message}", "Hydraulik PDF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void PrintDossierPdf(HaltungRecord? record)
    {
        if (record is null)
        {
            _dialogs.ShowMessage("Bitte zuerst eine Haltung auswaehlen.", "Dossier", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var holdingLabel = record.GetFieldValue("Haltungsname") ?? "";
        var (vonNr, bisNr) = Application.Reports.ProtocolPdfExporter.SplitHoldingNodes(holdingLabel);

        var schachtVon = FindSchachtByNummer(vonNr);
        var schachtBis = FindSchachtByNummer(bisNr);

        // Hydraulik pruefen
        var dn = TryParseDnMm(record.GetFieldValue("DN_mm"));
        var gefaelleRaw = record.GetFieldValue("Gefaelle_Promille");
        double? gefaelle = null;
        if (!string.IsNullOrWhiteSpace(gefaelleRaw))
        {
            var gText = gefaelleRaw.Trim().Replace(',', '.');
            if (double.TryParse(gText, NumberStyles.Float, CultureInfo.InvariantCulture, out var gVal))
                gefaelle = gVal;
        }
        var hydraulikAvailable = dn.HasValue && dn.Value > 0 && gefaelle.HasValue && gefaelle.Value > 0;

        // Kosten pruefen
        var projectFolder = _shell.GetProjectFolder() ?? "";
        var costRepo = new Infrastructure.Costs.ProjectCostStoreRepository();
        var costStore = costRepo.Load(App.Resolve<AppSettings>().LastProjectPath);
        Domain.Models.HoldingCost? holdingCost = null;
        if (costStore.ByHolding.TryGetValue(holdingLabel.Trim(), out var hc))
            holdingCost = hc;
        var kostenField = record.GetFieldValue("Kosten");
        var kostenAvailable = holdingCost?.Measures is { Count: > 0 }
            || !string.IsNullOrWhiteSpace(kostenField)
            || !string.IsNullOrWhiteSpace(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));

        // Original-PDFs pruefen (Haltung + Schaechte)
        var originalPdfPaths = ResolveOriginalPdfPaths(record, projectFolder);
        if (schachtVon != null)
            ResolveSchachtPdfPaths(schachtVon, projectFolder, originalPdfPaths);
        if (schachtBis != null)
            ResolveSchachtPdfPaths(schachtBis, projectFolder, originalPdfPaths);

        // Dialog oeffnen
        var dialog = new DossierPrintDialog();
        dialog.Owner = System.Windows.Application.Current?.MainWindow;
        dialog.SetAvailability(
            schachtVon != null, vonNr,
            schachtBis != null, bisNr,
            hydraulikAvailable,
            kostenAvailable,
            originalPdfPaths.Count);

        if (App.Resolve<IDialogService>().ShowDialog(dialog) != true || dialog.SelectedOptions is null)
            return;

        // SaveFileDialog
        var defaultName = $"Dossier_{SanitizeFilenamePart(holdingLabel)}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = App.Resolve<IDialogService>().SaveFile(
            "Haltungsdossier als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            // Hydraulik berechnen falls gewuenscht
            Application.Reports.HydraulikCalcResult? calcResult = null;
            if (dialog.SelectedOptions.IncludeHydraulik && hydraulikAvailable)
            {
                var materialRaw = record.GetFieldValue("Rohrmaterial") ?? "";
                var vm = new HydraulikPanelViewModel();
                vm.LoadFromRecord(dn!.Value, materialRaw, gefaelle);

                var mat = vm.SelectedMaterial;
                double kb = vm.IsNeuzustand ? mat.KbNeu : mat.KbAlt;
                double wasserstand = dn.Value / 2;

                var input = new HydraulikInput(
                    DN_mm: dn.Value,
                    Wasserstand_mm: wasserstand,
                    Gefaelle_Promille: vm.Gefaelle,
                    Kb: kb,
                    AbwasserTyp: "MR",
                    Temperatur_C: vm.Temperatur);

                var result = HydraulikEngine.Berechne(input);
                if (result != null)
                {
                    calcResult = new Application.Reports.HydraulikCalcResult
                    {
                        DN_mm = input.DN_mm,
                        Wasserstand_mm = input.Wasserstand_mm,
                        Gefaelle_Promille = input.Gefaelle_Promille,
                        Kb = input.Kb,
                        AbwasserTyp = input.AbwasserTyp,
                        Temperatur_C = input.Temperatur_C,
                        Material = mat.Label,
                        V_T = result.V_T, Q_T = result.Q_T, A_T = result.A_T,
                        Lu_T = result.Lu_T, Rhy_T = result.Rhy_T, Bsp = result.Bsp,
                        V_V = result.V_V, Q_V = result.Q_V,
                        Re = result.Re, Fr = result.Fr, Lambda = result.Lambda,
                        Tau = result.Tau, Ny = result.Ny,
                        Vc = result.Abl.Vc, Ic = result.Abl.Ic, TauC = result.Abl.TauC,
                        Auslastung = result.Auslastung,
                        VelocityOk = result.VelocityOk, ShearOk = result.ShearOk,
                        FroudeOk = result.Fr <= 1, AblagerungOk = result.AblagerungOk,
                    };
                }
            }

            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");

            // Historische Vergleichsreferenz: Profil-Lookup aus Buerglen 2024-2026 Auswertungen
            Application.Reports.HistorischeReferenz? histRef = null;
            if (dialog.SelectedOptions.IncludeKostenschaetzung)
            {
                try
                {
                    var dnVal = double.TryParse(record.GetFieldValue("DN_mm"), out var d) ? d : 0;
                    var matVal = record.GetFieldValue("Rohrmaterial");
                    var nutzVal = record.GetFieldValue("Nutzungsart");
                    var profile = App.Resolve<AuswertungPro.Next.Infrastructure.Devis.HistorischeSanierungenService>().FindMatchingProfile(dnVal, matVal, nutzVal);
                    if (profile is { AnzahlFaelle: >= 3 })
                    {
                        histRef = new Application.Reports.HistorischeReferenz
                        {
                            ProfilLabel = $"{profile.DnKlasse}, {profile.Material}, {profile.Nutzungsart}",
                            AnzahlFaelle = profile.AnzahlFaelle,
                            KostenProMMedianChf = (decimal?)profile.KostenProMMedianChf,
                            KostenProMMinChf = (decimal?)profile.KostenProMMinChf,
                            KostenProMMaxChf = (decimal?)profile.KostenProMMaxChf,
                            KostenProHaltungMedianChf = (decimal?)profile.KostenProHaltungMedianChf,
                            TypischeMassnahmen = profile.TypischeMassnahmen,
                            Quelle = "Auswertungen Bürglen 2024-2026",
                        };
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Dossier] Historische Referenz nicht ermittelbar: {ex.Message}");
                }
            }

            var options = dialog.SelectedOptions with
            {
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null,
                HoldingCost = dialog.SelectedOptions.IncludeKostenschaetzung ? holdingCost : null,
                OriginalPdfPaths = dialog.SelectedOptions.IncludeOriginalProtokolle ? originalPdfPaths : null,
                HistorischeReferenz = histRef,
            };

            var hasDossierBaseSection =
                options.IncludeDeckblatt
                || options.IncludeHaltungsprotokoll
                || (options.IncludeFotos && HasPrintableDossierPhotos(record, projectFolder))
                || (options.IncludeSchachtVon && schachtVon != null)
                || (options.IncludeSchachtBis && schachtBis != null)
                || (options.IncludeHydraulik && calcResult != null)
                || (options.IncludeKostenschaetzung && kostenAvailable);

            // Pruefung ob druckbar (muss auf UI-Thread, wegen MessageBox)
            if (!hasDossierBaseSection && !(options.IncludeOriginalProtokolle && originalPdfPaths.Count > 0))
            {
                _dialogs.ShowMessage(
                    "Die ausgewaehlte Kombination enthaelt keine druckbaren Inhalte.",
                    "Dossier",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // PDF-Erzeugung auf Background-Thread (verhindert UI-Freeze)
            // Alle CPU-intensiven Operationen: Build, Merge, WriteAllBytes
            var localHasDossierBase = hasDossierBaseSection;
            await Task.Run(() =>
            {
                var originalsAlreadyMerged = false;
                byte[] pdf;
                if (localHasDossierBase)
                {
                    pdf = Application.Reports.HaltungsDossierPdfBuilder.Build(
                        _shell.Project, record, schachtVon, schachtBis, calcResult, projectFolder, options);
                }
                else
                {
                    pdf = Infrastructure.Media.PdfMergeHelper.MergeOriginals(originalPdfPaths);
                    if (pdf.Length == 0)
                        throw new InvalidOperationException("Die Original-Protokolle konnten nicht zusammengefuehrt werden.");
                    originalsAlreadyMerged = true;
                }

                // Original-PDFs anhaengen
                if (!originalsAlreadyMerged && options.IncludeOriginalProtokolle && originalPdfPaths.Count > 0)
                    pdf = Infrastructure.Media.PdfMergeHelper.MergeWithOriginals(pdf, originalPdfPaths);

                File.WriteAllBytes(output, pdf);
            });

            _dialogs.ShowMessage($"Dossier wurde erstellt:\n{output}", "Dossier", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage($"Dossier konnte nicht erstellt werden:\n{ex.Message}", "Dossier", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

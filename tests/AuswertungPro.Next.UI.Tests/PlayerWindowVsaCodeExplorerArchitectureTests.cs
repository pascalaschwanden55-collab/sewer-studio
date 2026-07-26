using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowVsaCodeExplorerArchitectureTests
{
    [Fact]
    public void PlayerWindow_vsa_code_explorer_window_creation_lives_in_dialog_service()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var servicePath = Path.Combine(uiRoot, "Services", "VsaCodeExplorerDialogService.cs");
        var factoryPath = Path.Combine(uiRoot, "Services", "VsaCodeExplorerDialogServiceFactory.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingCodeExplorerWorkflowService.cs");
        var workflowFactoryPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingCodeExplorerWorkflowServiceFactory.cs");
        var serviceCreationWorkflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingCodeExplorerServiceCreationWorkflow.cs");

        Assert.True(File.Exists(servicePath), "VSA-Code-Explorer-Dialoggrenze muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(factoryPath), "VSA-Code-Explorer-Fenstererzeugung muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowPath), "Coding-Code-Explorer-Workflow muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowFactoryPath), "Coding-Code-Explorer-Workflow muss ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(serviceCreationWorkflowPath), "Coding-Code-Explorer-Serviceerstellung soll ausserhalb der PlayerWindow-Partials liegen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var codeExplorerDialog = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.Coding.CodeExplorer.Dialog.cs"));
        var service = File.ReadAllText(servicePath);
        var factory = File.ReadAllText(factoryPath);
        var workflow = File.ReadAllText(workflowPath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);
        var serviceCreationWorkflow = File.Exists(serviceCreationWorkflowPath) ? File.ReadAllText(serviceCreationWorkflowPath) : "";

        Assert.Contains("CodingCodeExplorerServiceCreationWorkflow.Create", codeExplorerDialog);
        Assert.Contains("CreateVsaCodeExplorerLiveSnapshotProvider", playerWindowText);
        Assert.Contains("public sealed record VsaCodeExplorerDialogRequest", service);
        Assert.Contains("public sealed record VsaCodeExplorerDialogResult", service);
        Assert.Contains("new VsaCodeExplorerWindow", factory);
        Assert.Contains("LiveSnapshotProvider", factory);
        Assert.Contains("CodingExplorerEntryFactory.CreateSeed", workflow);
        Assert.Contains("VsaCodeExplorerDialogServiceFactory.Create", workflowFactory);
        Assert.Contains("CodingCodeExplorerWorkflowServiceFactory.Create", serviceCreationWorkflow);
        Assert.Contains("actions.CreateService(createViewModel)", serviceCreationWorkflow);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_foto_vorschau_planung()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var plannerPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerPhotoPreviewPlanner.cs");

        Assert.True(File.Exists(plannerPath), "Foto-Vorschau-Entscheidung muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var updateMethod = ExtractMethodBody(windowSource, "private void UpdateFotoImages()");
        var plannerSource = File.ReadAllText(plannerPath);

        Assert.Contains("VsaCodeExplorerPhotoPreviewPlanner.Plan(", updateMethod, StringComparison.Ordinal);
        Assert.Contains("File.Exists", plannerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.FotoPaths.Count > 0", updateMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.FotoPaths.Count > 1", updateMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists(_vm.FotoPaths[0])", updateMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists(_vm.FotoPaths[1])", updateMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_foto_vorschau_rendering_an_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerPhotoPreviewRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Foto-Vorschau-Rendering muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var updateMethod = ExtractMethodBody(windowSource, "private void UpdateFotoImages()");
        var rendererSource = File.ReadAllText(rendererPath);

        Assert.Contains("VsaCodeExplorerPhotoPreviewRenderer.Apply(", updateMethod, StringComparison.Ordinal);
        Assert.Contains("BitmapImage", rendererSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void ApplyFotoPreview(", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new System.Windows.Media.Imaging.BitmapImage", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DecodePixelHeight", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("placeholder.Visibility", windowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_photo_assistant_ergebnis_an_workflow()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerPhotoResultWorkflow.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerPhotoResultRenderer.cs");

        Assert.True(File.Exists(workflowPath), "PhotoAssistant-Ergebnislogik muss ausserhalb der Window-Code-behind liegen.");
        Assert.True(File.Exists(rendererPath), "PhotoAssistant-Ergebnis-Rendering muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var applyMethod = ExtractMethodBody(windowSource, "private void ApplyPhotoResult(");
        var workflowSource = File.ReadAllText(workflowPath);

        Assert.Contains("VsaCodeExplorerPhotoResultWorkflow.Apply(", applyMethod, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerPhotoResultRenderer.Apply(", applyMethod, StringComparison.Ordinal);
        Assert.Contains("PhotoMeasurementResultMapper.Map", workflowSource, StringComparison.Ordinal);
        Assert.Contains("File.Exists", workflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PhotoMeasurementResultMapper.Map", applyMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists(result.OverlayPhotoPath)", applyMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("while (_vm.FotoPaths.Count <= photoIndex)", applyMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.FotoPaths[photoIndex] = result.OverlayPhotoPath", applyMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("result.UpdatedCalibration", applyMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtQ1Value.Text =", applyMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockVon.Text =", applyMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_photo_assistant_open_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerPhotoAssistantOpenPolicy.cs");

        Assert.True(File.Exists(policyPath), "PhotoAssistant-Oeffnungsentscheidung muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var openMethod = ExtractMethodBody(windowSource, "private void OpenPhotoAssistant(");
        var policySource = File.ReadAllText(policyPath);

        Assert.Contains("VsaCodeExplorerPhotoAssistantOpenPolicy.Resolve(", openMethod, StringComparison.Ordinal);
        Assert.Contains("File.Exists", policySource, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.FotoPaths.Count <= photoIndex", openMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("string.IsNullOrEmpty(_vm.FotoPaths[photoIndex])", openMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists(_vm.FotoPaths[photoIndex])", openMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("new PhotoMeasurementWindow(_vm.FotoPaths[photoIndex]", openMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_foto_capture_an_workflow()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerPhotoCaptureWorkflow.cs");

        Assert.True(File.Exists(workflowPath), "Foto-Capture-Orchestrierung muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var captureMethod = ExtractMethodBody(windowSource, "private async System.Threading.Tasks.Task CapturePhotoAsync(");
        var workflowSource = File.ReadAllText(workflowPath);

        Assert.Contains("VsaCodeExplorerPhotoCaptureWorkflow.CaptureWithDefaultsAsync(", captureMethod, StringComparison.Ordinal);
        Assert.Contains("FfmpegLocator.ResolveFfmpeg", workflowSource, StringComparison.Ordinal);
        Assert.Contains("VideoFrameExtractor.TryExtractFramePngAsync", workflowSource, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.TryParseExact", workflowSource, StringComparison.Ordinal);
        Assert.Contains("File.WriteAllBytesAsync", workflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("LiveSnapshotProvider()", captureMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("FfmpegLocator.ResolveFfmpeg", captureMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("VideoFrameExtractor.TryExtractFramePngAsync", captureMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("TimeSpan.TryParseExact", captureMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllBytesAsync", captureMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.GetTempPath", captureMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("Guid.NewGuid", captureMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("while (_vm.FotoPaths.Count <= fotoIndex)", captureMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.FotoPaths[fotoIndex] = tempPath", captureMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_foto_capture_buttons_an_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerPhotoCaptureButtonsRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Foto-Capture-Button-Zustand soll ausserhalb der Window-Code-behind gerendert werden.");

        var windowSource = File.ReadAllText(windowPath);
        var captureMethod = ExtractMethodBody(windowSource, "private async System.Threading.Tasks.Task CapturePhotoAsync(");

        Assert.Contains("VsaCodeExplorerPhotoCaptureButtonsRenderer.Apply(", captureMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("BtnCaptureFoto1.IsEnabled =", captureMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("BtnCaptureFoto2.IsEnabled =", captureMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_clock_text_changed_an_workflow()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerClockTextWorkflow.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerClockTextRenderer.cs");

        Assert.True(File.Exists(workflowPath), "Uhr-Textbox-Logik muss ausserhalb der Window-Code-behind liegen.");
        Assert.True(File.Exists(rendererPath), "Uhr-Textbox-Rendering muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var ctorSource = ExtractMethodBody(windowSource, "public VsaCodeExplorerWindow(VsaCodeExplorerViewModel vm,");
        var workflowSource = File.ReadAllText(workflowPath);
        var rendererSource = File.ReadAllText(rendererPath);

        Assert.Contains("VsaCodeExplorerClockTextWorkflow.ApplyVonChanged(", ctorSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerClockTextWorkflow.ApplyBisChanged(", ctorSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerClockTextRenderer.ApplyVonChanged(", ctorSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerClockTextRenderer.ApplyBisChanged(", ctorSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerClockTextRenderTargets", rendererSource, StringComparison.Ordinal);
        Assert.Contains("ClockTransferFormatter.Format", workflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.ClockVon = TxtClockVon.Text", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.ClockBis = TxtClockBis.Text", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.ClockMode == \"single\"", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.IsNullOrWhiteSpace(TxtClockVon.Text) ? string.Empty : \"00\"", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockBis.Text = result.ClockBisText", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockTransfer.Text = result.TransferText", ctorSource, StringComparison.Ordinal);

        Assert.DoesNotContain("private void UpdateClockTransfer()", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ClockTransferFormatter.Format", windowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_clock_picker_textbox_sync_an_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerClockPickerRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Uhr-Picker-zu-Textbox-Sync muss ausserhalb der Window-Code-behind gerendert werden.");

        var windowSource = File.ReadAllText(windowPath);
        var ctorSource = ExtractMethodBody(windowSource, "public VsaCodeExplorerWindow(VsaCodeExplorerViewModel vm,");
        var rendererSource = File.ReadAllText(rendererPath);

        Assert.Contains("VsaCodeExplorerClockPickerRenderer.ApplySingleValueChanged(", ctorSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerClockPickerRenderer.ApplyRangeFromChanged(", ctorSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerClockPickerRenderer.ApplyRangeToChanged(", ctorSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerClockPickerRenderTargets", rendererSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.IsNullOrWhiteSpace(val)", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockVon.Text = val", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockBis.Text = \"00\"", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockVon.Text = ClockRange.ValueFrom ?? \"\"", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockBis.Text = ClockRange.ValueTo ?? \"\"", ctorSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_streckenschaden_ui_an_workflow_und_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerStreckenschadenWorkflow.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerStreckenschadenRenderer.cs");

        Assert.True(File.Exists(workflowPath), "Streckenschaden-Zustandslogik soll ausserhalb der Window-Code-behind liegen.");
        Assert.True(File.Exists(rendererPath), "Streckenschaden-WPF-Rendering soll ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var ctorSource = ExtractMethodBody(windowSource, "public VsaCodeExplorerWindow(VsaCodeExplorerViewModel vm,");

        Assert.Contains("VsaCodeExplorerStreckenschadenWorkflow.ApplyChecked(", ctorSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerStreckenschadenWorkflow.ApplyUnchecked(", ctorSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerStreckenschadenWorkflow.BuildInitial(", ctorSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerStreckenschadenRenderer.Apply(", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.IsNullOrWhiteSpace(_vm.StreckenschadenTyp)", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StreckeTypPanel.Visibility =", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("LstStreckeTyp.SelectedIndex =", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.StreckenschadenTyp = \"Anfang\"", ctorSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_initiale_videozeit_an_workflow()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerInitialTimeWorkflow.cs");

        Assert.True(File.Exists(workflowPath), "Initiale Videozeit soll ausserhalb der Window-Code-behind entschieden werden.");

        var windowSource = File.ReadAllText(windowPath);
        var ctorSource = ExtractMethodBody(windowSource, "public VsaCodeExplorerWindow(VsaCodeExplorerViewModel vm,");
        var workflowSource = File.ReadAllText(workflowPath);

        Assert.Contains("VsaCodeExplorerInitialTimeWorkflow.Build(", ctorSource, StringComparison.Ordinal);
        Assert.Contains("ProtocolEntryInputNormalizer.FormatTime", workflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProtocolEntryInputNormalizer.FormatTime", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("!string.IsNullOrWhiteSpace(_vm.Zeit)", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_currentVideoTime.HasValue && _currentVideoTime.Value > TimeSpan.Zero", ctorSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_initiale_formularfelder_an_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerInitialFieldsRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Initiale Formularfeld-Werte muessen ausserhalb der Window-Code-behind gerendert werden.");

        var windowSource = File.ReadAllText(windowPath);
        var ctorSource = ExtractMethodBody(windowSource, "public VsaCodeExplorerWindow(VsaCodeExplorerViewModel vm,");
        var rendererSource = File.ReadAllText(rendererPath);

        Assert.Contains("VsaCodeExplorerInitialFieldsRenderer.Apply(", ctorSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerInitialFieldValues", ctorSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerInitialFieldsRenderTargets", rendererSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtMeterStart.Text = _vm.MeterStart", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtMeterEnd.Text = _vm.MeterEnd", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtBemerkungen.Text = _vm.Bemerkungen", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtQ1Value.Text = _vm.Q1Value", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtQ2Value.Text = _vm.Q2Value", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockVon.Text = _vm.ClockVon", ctorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockBis.Text = _vm.ClockBis", ctorSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_clock_panel_praesentation_an_presenter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var presenterPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerClockPanelPresenter.cs");

        Assert.True(File.Exists(presenterPath), "Uhr-Panel-Praesentation muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var updateSource = ExtractMethodBody(windowSource, "private void UpdateClockPanel()");
        var presenterSource = File.ReadAllText(presenterPath);

        Assert.Contains("VsaCodeExplorerClockPanelPresenter.Build(", updateSource, StringComparison.Ordinal);
        Assert.Contains("ClockTransferFormatter.Format", presenterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var mode = _vm.ClockMode", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("mode == \"none\"", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("mode == \"single\"", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("mode == \"range\"", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.IsNullOrWhiteSpace(TxtClockVon.Text) ? string.Empty : \"00\"", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(TxtClockVon.Text?.Trim(), \"00\"", updateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_clock_panel_rendering_an_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerClockPanelRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Uhr-Panel-WPF-Rendering muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var updateSource = ExtractMethodBody(windowSource, "private void UpdateClockPanel()");
        var rendererSource = File.ReadAllText(rendererPath);

        Assert.Contains("VsaCodeExplorerClockPanelRenderer.Apply(", updateSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerClockPanelRenderTargets", rendererSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ClockPanel.Visibility = Visibility.Visible", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ClockPanel.Visibility = Visibility.Collapsed", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockTitle.Text = presentation.Title", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockHint.Text = presentation.Hint", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ClockSinglePanel.Visibility", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ClockRangePanel.Visibility", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BtnClockRechts.Visibility", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ClockSingle.Value = presentation.ClockSingleValue", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ClockRange.ValueFrom = presentation.ClockRangeFrom", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ClockRange.ValueTo = presentation.ClockRangeTo", updateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_clock_preset_parsing_an_workflow()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerClockPresetWorkflow.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerClockPresetRenderer.cs");

        Assert.True(File.Exists(workflowPath), "Uhr-Schnellwahl-Parsing muss ausserhalb der Window-Code-behind liegen.");
        Assert.True(File.Exists(rendererPath), "Uhr-Schnellwahl-Rendering muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var clickSource = ExtractMethodBody(windowSource, "private void ClockPreset_Click(");

        Assert.Contains("VsaCodeExplorerClockPresetWorkflow.Resolve(", clickSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerClockPresetRenderer.Apply(", clickSource, StringComparison.Ordinal);
        Assert.DoesNotContain("tag.Split(',')", clickSource, StringComparison.Ordinal);
        Assert.DoesNotContain("parts.Length != 2", clickSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockVon.Text = parts[0]", clickSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockBis.Text = parts[1]", clickSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockVon.Text = result.ClockVonText", clickSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtClockBis.Text = result.ClockBisText", clickSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_breadcrumb_praesentation_an_presenter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var presenterPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerBreadcrumbPresenter.cs");

        Assert.True(File.Exists(presenterPath), "Breadcrumb-Praesentation muss ausserhalb der Window-Code-behind berechnet werden.");

        var windowSource = File.ReadAllText(windowPath);
        var updateSource = ExtractMethodBody(windowSource, "private void UpdateBreadcrumb()");

        Assert.Contains("VsaCodeExplorerBreadcrumbPresenter.Build(", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("for (int i = 0; i < _vm.BreadcrumbItems.Count; i++)", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var isLast = i == _vm.BreadcrumbItems.Count - 1", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("if (i > 0)", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("item.Level", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("isLast ? FontWeights.SemiBold", updateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_breadcrumb_rendering_an_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerBreadcrumbRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Breadcrumb-WPF-Rendering muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var updateSource = ExtractMethodBody(windowSource, "private void UpdateBreadcrumb()");
        var rendererSource = File.ReadAllText(rendererPath);

        Assert.Contains("VsaCodeExplorerBreadcrumbRenderer.Apply(", updateSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerBreadcrumbRenderTargets", rendererSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BreadcrumbPanel.Items.Clear()", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new TextBlock", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new Button", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("btn.Click +=", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.NavigateToBreadcrumb(level)", updateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_enthaelt_keinen_unbenutzten_legacy_tile_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");

        var windowSource = File.ReadAllText(windowPath);

        Assert.DoesNotContain("private void RenderTiles()", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private Button CreateTileButton(", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Legacy: wird nicht mehr verwendet", windowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_column_layout_an_presenter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var presenterPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerColumnLayoutPresenter.cs");

        Assert.True(File.Exists(presenterPath), "Spaltenlayout-Entscheidungen sollen ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var renderSource = ExtractMethodBody(windowSource, "private void RenderColumnTiles(");

        Assert.Contains("VsaCodeExplorerColumnLayoutPresenter.Build(", renderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var hasChar2 = _vm.Char2Tiles.Count > 0", renderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Char2Column.Width = hasChar2", renderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Char2Sep.Width = hasChar2", renderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Char2SepBorder.Visibility = hasChar2", renderSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_column_tile_praesentation_an_presenter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var presenterPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerColumnTilePresenter.cs");

        Assert.True(File.Exists(presenterPath), "Column-Tile-Praesentation soll ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var createSource = ExtractMethodBody(windowSource, "private Button CreateColumnTileButton(");

        Assert.Contains("VsaCodeExplorerColumnTilePresenter.Build(", createSource, StringComparison.Ordinal);
        Assert.DoesNotContain("tile.IsInvalid ? InvalidBrush", createSource, StringComparison.Ordinal);
        Assert.DoesNotContain("tile.IsSelected ? _accentBrush", createSource, StringComparison.Ordinal);
        Assert.DoesNotContain("tile.BadgeText is not null", createSource, StringComparison.Ordinal);
        Assert.DoesNotContain("tile.IsFinal && !tile.IsSelected", createSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.IsNullOrEmpty(tile.Description)", createSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_column_tile_rendering_an_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerColumnTileRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Column-Tile-WPF-Aufbau soll ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var createSource = ExtractMethodBody(windowSource, "private Button CreateColumnTileButton(");

        Assert.Contains("VsaCodeExplorerColumnTileRenderer.CreateButton(", createSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerColumnTileRenderer.CreateButtonStyle(", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new DockPanel", createSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new Border", createSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new TextBlock", createSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new Button", createSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateBadge(", createSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveColumnTileBrush(", createSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private Style BuildTileButtonStyle()", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static Border CreateBadge(", windowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_quant_panel_praesentation_an_presenter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var presenterPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerQuantPanelPresenter.cs");

        Assert.True(File.Exists(presenterPath), "Q1/Q2-Panel-Praesentation muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var updateSource = ExtractMethodBody(windowSource, "private void UpdateQuantPanel()");
        var presenterSource = File.ReadAllText(presenterPath);

        Assert.Contains("VsaCodeExplorerQuantPanelPresenter.Build(", updateSource, StringComparison.Ordinal);
        Assert.Contains("QuantField", presenterSource, StringComparison.Ordinal);
        Assert.Contains("Pflicht == \"P\"", presenterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var q1 = _vm.Q1Rule", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var q2 = _vm.Q2Rule", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("q1.Min.HasValue", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("q1.Max.HasValue", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("q1.Pflicht == \"P\"", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtQ1Label.Text = $\"Q1:", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtQ2Label.Text = $\"Q2:", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"PFLICHT\"", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("requiredBadge.Background", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("((TextBlock)requiredBadge.Child)", windowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_quant_panel_rendering_an_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerQuantPanelRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Q1/Q2-WPF-Rendering muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var updateSource = ExtractMethodBody(windowSource, "private void UpdateQuantPanel()");
        var rendererSource = File.ReadAllText(rendererPath);

        Assert.Contains("VsaCodeExplorerQuantPanelRenderer.Apply(", updateSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerQuantPanelRenderTargets", rendererSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerQuantPanelRenderBrushes", rendererSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private void ApplyQuantFieldPresentation(", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private void ApplyQuantRequiredBadgePresentation(", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private Color ResolveQuantBrushColor(", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private Brush ResolveQuantBrush(", windowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_result_panel_praesentation_an_presenter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var presenterPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerResultPanelPresenter.cs");

        Assert.True(File.Exists(presenterPath), "Ergebnis-Panel-Praesentation muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var updateSource = ExtractMethodBody(windowSource, "private void UpdateResultPanel()");
        var presenterSource = File.ReadAllText(presenterPath);

        Assert.Contains("VsaCodeExplorerResultPanelPresenter.Build(", updateSource, StringComparison.Ordinal);
        Assert.Contains("ShouldUpdateDetailPanels", presenterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("if (_vm.ShowResultPanel)", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtFinalLabel.Text = _vm.FinalLabel", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtWarn.Text = _vm.WarnMessage", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.IsNullOrEmpty(_vm.WarnMessage)", updateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_result_panel_rendering_an_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerResultPanelRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Ergebnis-Panel-WPF-Rendering muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var updateSource = ExtractMethodBody(windowSource, "private void UpdateResultPanel()");
        var rendererSource = File.ReadAllText(rendererPath);

        Assert.Contains("VsaCodeExplorerResultPanelRenderer.Apply(", updateSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerResultPanelRenderTargets", rendererSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ResultPanel.Visibility =", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CodeHintPanel.Visibility =", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtFinalCode.Text =", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtFinalLabel.Text =", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtWarn.Text =", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtWarn.Visibility =", updateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_final_property_changes_an_result_presenter_updates()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var presenterPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerResultPanelPresenter.cs");

        Assert.True(File.Exists(presenterPath), "Finalcode/Label/Warnung sollen ueber Ergebnis-Presenter aktualisiert werden.");

        var windowSource = File.ReadAllText(windowPath);
        var applySource = ExtractMethodBody(windowSource, "private void ApplyViewModelPropertyChanged(");

        Assert.Contains("UpdateResultPanel();", applySource, StringComparison.Ordinal);
        Assert.Contains("UpdateProgress();", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtFinalCode.Text = _vm.FinalCode", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtCodePreview.Text = _vm.FinalCode", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtFinalLabel.Text = _vm.FinalLabel", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtWarn.Text = _vm.WarnMessage", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.IsNullOrEmpty(_vm.WarnMessage)", applySource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_validierungs_praesentation_an_presenter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var presenterPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerValidationPresenter.cs");

        Assert.True(File.Exists(presenterPath), "Footer-Validierungsanzeige muss ausserhalb der Window-Code-behind berechnet werden.");

        var windowSource = File.ReadAllText(windowPath);
        var syncSource = ExtractMethodBody(windowSource, "private void SyncValidationUi()");

        Assert.Contains("VsaCodeExplorerValidationPresenter.Build(", syncSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BtnApply.IsEnabled = _vm.CanConfirm", syncSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtValidation.Text = _vm.ValidationMessage", syncSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.IsNullOrWhiteSpace(TxtValidation.Text)", syncSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_validierungs_rendering_an_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerValidationRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Footer-Validierungsrendering muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var syncSource = ExtractMethodBody(windowSource, "private void SyncValidationUi()");
        var rendererSource = File.ReadAllText(rendererPath);

        Assert.Contains("VsaCodeExplorerValidationRenderer.Apply(", syncSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerValidationRenderTargets", rendererSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BtnApply.IsEnabled =", syncSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtValidation.Text =", syncSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtValidation.Visibility =", syncSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_property_changed_routing_an_workflow()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerPropertyChangeWorkflow.cs");

        Assert.True(File.Exists(workflowPath), "PropertyChanged-Routing muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var applySource = ExtractMethodBody(windowSource, "private void ApplyViewModelPropertyChanged(");
        var workflowSource = File.ReadAllText(workflowPath);

        Assert.Contains("VsaCodeExplorerPropertyChangeWorkflow.Resolve(", applySource, StringComparison.Ordinal);
        Assert.Contains("UpdateResultPanel", workflowSource, StringComparison.Ordinal);
        Assert.Contains("UpdateQuantPanel", workflowSource, StringComparison.Ordinal);
        Assert.Contains("UpdateClockPanel", workflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("switch (propertyName)", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("case nameof(VsaCodeExplorerViewModel.CurrentLevel)", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("case nameof(VsaCodeExplorerViewModel.FinalCode)", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("case nameof(VsaCodeExplorerViewModel.Q1Error)", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("propertyName is nameof(VsaCodeExplorerViewModel.BreadcrumbItems)", applySource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_q1_q2_fehler_rendering()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var presenterPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerFieldErrorPresenter.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerFieldErrorRenderer.cs");

        Assert.True(File.Exists(presenterPath), "Q1/Q2-Fehlerpraesentation muss ausserhalb der Window-Code-behind berechnet werden.");
        Assert.True(File.Exists(rendererPath), "Q1/Q2-Fehlerrendering muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var applySource = ExtractMethodBody(windowSource, "private void ApplyViewModelPropertyChanged(");

        Assert.Contains("VsaCodeExplorerFieldErrorPresenter.Build(", applySource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerFieldErrorRenderer.Apply(", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtQ1Error.Text =", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtQ1Error.Visibility =", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtQ2Error.Text =", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtQ2Error.Visibility =", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.Q1Error is not null", applySource, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.Q2Error is not null", applySource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_progress_praesentation_an_presenter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var presenterPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerProgressPresenter.cs");

        Assert.True(File.Exists(presenterPath), "Fortschritts-Praesentation muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var updateSource = ExtractMethodBody(windowSource, "private void UpdateProgress()");
        var presenterSource = File.ReadAllText(presenterPath);

        Assert.Contains("VsaCodeExplorerProgressPresenter.Build(", updateSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerProgressBarRole.CurrentGroup", presenterSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerProgressLabelRole.Secondary", presenterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var level = _vm.CurrentLevel", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var isFinal = _vm.ShowResultPanel", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("if (isFinal && i >= level)", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("else if (i < level)", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("else if (i == level)", updateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_progress_rendering_an_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var rendererPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerProgressRenderer.cs");

        Assert.True(File.Exists(rendererPath), "Fortschritts-WPF-Rendering muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var updateSource = ExtractMethodBody(windowSource, "private void UpdateProgress()");
        var rendererSource = File.ReadAllText(rendererPath);

        Assert.Contains("VsaCodeExplorerProgressRenderer.Apply(", updateSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerProgressRenderTargets", rendererSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerProgressRenderBrushes", rendererSource, StringComparison.Ordinal);
        Assert.DoesNotContain("bars[i].Background", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("labels[i].FontWeight", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("labels[i].Foreground", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TxtCodePreview.Text = presentation.CodePreviewText", updateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private Color ResolveProgressBarColor(", windowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void VsaCodeExplorerWindow_delegiert_keyboard_navigation_an_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowPath = Path.Combine(uiRoot, "Views", "Windows", "VsaCodeExplorerWindow.xaml.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "Vsa", "VsaCodeExplorerKeyboardNavigationPolicy.cs");

        Assert.True(File.Exists(policyPath), "Keyboard-Navigation muss ausserhalb der Window-Code-behind liegen.");

        var windowSource = File.ReadAllText(windowPath);
        var keyDownSource = ExtractMethodBody(windowSource, "private void OnPreviewKeyDown(");
        var policySource = File.ReadAllText(policyPath);

        Assert.Contains("VsaCodeExplorerKeyboardNavigationPolicy.Resolve(", keyDownSource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerKeyboardNavigationAction.NavigateBack", policySource, StringComparison.Ordinal);
        Assert.Contains("VsaCodeExplorerKeyboardNavigationAction.ApplyAndClose", policySource, StringComparison.Ordinal);
        Assert.DoesNotContain("e.Key == Key.Escape", keyDownSource, StringComparison.Ordinal);
        Assert.DoesNotContain("e.Key == Key.Back", keyDownSource, StringComparison.Ordinal);
        Assert.DoesNotContain("e.Key == Key.S", keyDownSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Keyboard.FocusedElement is not TextBox", keyDownSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_vm.ShowResultPanel || _vm.CurrentLevel > 0", keyDownSource, StringComparison.Ordinal);
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Methode nicht gefunden: {signature}");

        var openBraceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(openBraceIndex > signatureIndex, $"Methoden-Anfang nicht gefunden: {signature}");

        var depth = 0;
        for (var i = openBraceIndex; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
                depth--;

            if (depth == 0)
                return source[signatureIndex..(i + 1)];
        }

        throw new InvalidOperationException($"Methoden-Ende nicht gefunden: {signature}");
    }
}

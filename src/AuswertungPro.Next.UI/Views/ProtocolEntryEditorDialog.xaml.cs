using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views;

public partial class ProtocolEntryEditorDialog : Window
{
    private readonly ServiceProvider? _sp;
    private readonly ProtocolEntryVM _entryVm;
    private readonly ProtocolEntryEditorViewModel? _paramVm;
    private readonly string? _haltungId;
    private readonly string? _videoPath;
    private readonly string? _projectFolder;
    private bool _isKiBusy;

    public ProtocolEntryEditorDialog()
        : this(new ProtocolEntryVM(new ProtocolEntry()), App.Services as ServiceProvider, null, null, null)
    {
    }

    public ProtocolEntryEditorDialog(
        ProtocolEntryVM entryVm,
        ServiceProvider? sp = null,
        string? haltungId = null,
        string? videoPath = null,
        string? projectFolder = null)
    {
        InitializeComponent();

        _entryVm = entryVm;
        _sp = sp ?? (App.Services as ServiceProvider);
        _haltungId = haltungId;
        _videoPath = videoPath;
        _projectFolder = projectFolder;

        _paramVm = _sp?.CodeCatalog is null ? null : new ProtocolEntryEditorViewModel(_sp.CodeCatalog);

        LoadFromEntry();

        CatalogButton.Click += (_, _) => OpenCodePicker();
        KiSuggestButton.Click += async (_, _) => await SuggestWithKiAsync();
        ZeitFromVideoButton.Click += (_, _) => SetZeitFromVideo();
        SeekToZeitButton.Click += (_, _) => SeekToZeit();
        OkButton.Click += (_, _) => ApplyAndClose();
        CodeTextBox.TextChanged += (_, _) => RefreshParameterBindings();
        CancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        if (string.IsNullOrWhiteSpace(_entryVm.Code))
        {
            Dispatcher.BeginInvoke(new Action(OpenCodePicker), DispatcherPriority.Loaded);
        }
    }

    private void LoadFromEntry()
    {
        CodeTextBox.Text = _entryVm.Code;
        MeterStartTextBox.Text = _entryVm.MeterStart?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;
        MeterEndTextBox.Text = _entryVm.MeterEnd?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;
        ZeitTextBox.Text = _entryVm.Zeit is null ? string.Empty : FormatTime(_entryVm.Zeit.Value);
        MpegTextBox.Text = _entryVm.Mpeg ?? string.Empty;
        BeschreibungTextBox.Text = _entryVm.Beschreibung;
        StreckenschadenCheckBox.IsChecked = _entryVm.Model.IsStreckenschaden;
        AiStatusText.Text = string.IsNullOrWhiteSpace(_entryVm.AiSuggestedCode)
            ? string.Empty
            : $"Vorheriger KI-Vorschlag: {_entryVm.AiSuggestedCode} ({_entryVm.AiConfidence:P0})";
        ValidationStatus.Text = string.Empty;

        if (_paramVm is not null)
        {
            _paramVm.LoadFromEntry(_entryVm.Model);
            ParametersItems.ItemsSource = _paramVm.Parameters;
        }
    }

    private void RefreshParameterBindings()
    {
        if (_paramVm is null)
            return;

        var code = (CodeTextBox.Text ?? string.Empty).Trim();
        if (string.Equals(_paramVm.Code, code, StringComparison.OrdinalIgnoreCase))
            return;

        _paramVm.Code = code;
        ParametersItems.ItemsSource = _paramVm.Parameters;
    }

    private async Task SuggestWithKiAsync()
    {
        if (_isKiBusy)
            return;

        if (_sp is null || _sp.CodeCatalog is null)
        {
            ValidationStatus.Text = "KI nicht verfuegbar: Service fehlt.";
            return;
        }

        if (_sp.ProtocolAi is NoopProtocolAiService)
        {
            ValidationStatus.Text = "KI ist deaktiviert. Setze AUSWERTUNGPRO_AI_ENABLED=1 und starte neu.";
            return;
        }

        var allowedCodes = _sp.CodeCatalog.AllowedCodes();
        if (allowedCodes.Count == 0)
        {
            ValidationStatus.Text = "KI nicht moeglich: Code-Katalog ist leer.";
            return;
        }

        if (!TryParseOptionalDouble(MeterStartTextBox.Text, out var meterStart))
        {
            ValidationStatus.Text = "MeterStart ist ungueltig.";
            return;
        }

        if (!TryParseOptionalDouble(MeterEndTextBox.Text, out var meterEnd))
        {
            ValidationStatus.Text = "MeterEnd ist ungueltig.";
            return;
        }

        if (!TryParseOptionalTimeSpan(ZeitTextBox.Text, out var zeit))
        {
            ValidationStatus.Text = "Zeit ist ungueltig.";
            return;
        }

        var preferredMeter = meterStart ?? meterEnd;
        var code = (CodeTextBox.Text ?? string.Empty).Trim();
        var description = (BeschreibungTextBox.Text ?? string.Empty).Trim();

        var videoPath = ResolveExistingPath(_videoPath);
        var imagePaths = ResolveImagePaths(_entryVm.Model.FotoPaths);
        var projectFolder = ResolveProjectFolder();

        _isKiBusy = true;
        KiSuggestButton.IsEnabled = false;
        AiStatusText.Text = "KI-Vorschlag wird geladen...";
        ValidationStatus.Text = string.Empty;

        try
        {
            var input = new AiInput(
                ProjectFolderAbs: projectFolder,
                HaltungId: string.IsNullOrWhiteSpace(_haltungId) ? null : _haltungId,
                Meter: preferredMeter,
                ExistingCode: string.IsNullOrWhiteSpace(code) ? null : code,
                ExistingText: string.IsNullOrWhiteSpace(description) ? null : description,
                AllowedCodes: allowedCodes,
                VideoPathAbs: videoPath,
                Zeit: zeit,
                ImagePathsAbs: imagePaths.Count == 0 ? null : imagePaths);

            var suggestion = await _sp.ProtocolAi.SuggestAsync(input);
            if (suggestion is null)
            {
                AiStatusText.Text = "Kein KI-Vorschlag erhalten.";
                return;
            }

            _entryVm.ApplyAiSuggestionToModelAndVm(suggestion);

            if (string.IsNullOrWhiteSpace(suggestion.SuggestedCode))
            {
                AiStatusText.Text = $"KI-Vorschlag ohne Code ({suggestion.Confidence:P0}).";
            }
            else if (_sp.CodeCatalog.TryGet(suggestion.SuggestedCode, out _))
            {
                CodeTextBox.Text = suggestion.SuggestedCode.Trim().ToUpperInvariant();
                AiStatusText.Text = $"KI-Vorschlag uebernommen: {suggestion.SuggestedCode} ({suggestion.Confidence:P0}).";
            }
            else
            {
                AiStatusText.Text = $"KI-Code '{suggestion.SuggestedCode}' ist nicht im Katalog.";
            }

            if (!string.IsNullOrWhiteSpace(suggestion.ReasonShort))
                ValidationStatus.Text = "KI-Hinweis: " + Truncate(suggestion.ReasonShort, 180);
        }
        catch (Exception ex)
        {
            ValidationStatus.Text = $"KI-Fehler: {ex.Message}";
        }
        finally
        {
            _isKiBusy = false;
            KiSuggestButton.IsEnabled = true;
        }
    }

    private void OpenCodePicker()
    {
        if (_sp?.CodeCatalog is null)
        {
            ValidationStatus.Text = "Code-Katalog ist nicht verfuegbar.";
            return;
        }

        var vm = new ProtocolCodePickerViewModel(_sp.CodeCatalog, _entryVm);
        var picker = new ProtocolCodePickerDialog
        {
            Owner = this,
            DataContext = vm
        };

        if (picker.ShowDialog() == true)
        {
            LoadFromEntry();
            ValidationStatus.Text = "Code uebernommen.";
        }
    }

    private void ApplyAndClose()
    {
        ValidationStatus.Text = string.Empty;

        if (!TryParseOptionalDouble(MeterStartTextBox.Text, out var meterStart))
        {
            ValidationStatus.Text = "MeterStart ist ungueltig.";
            return;
        }

        if (!TryParseOptionalDouble(MeterEndTextBox.Text, out var meterEnd))
        {
            ValidationStatus.Text = "MeterEnd ist ungueltig.";
            return;
        }

        if (!TryParseOptionalTimeSpan(ZeitTextBox.Text, out var zeit))
        {
            ValidationStatus.Text = "Zeit ist ungueltig.";
            return;
        }

        var mpeg = (MpegTextBox.Text ?? string.Empty).Trim();
        if (zeit is null && !string.IsNullOrWhiteSpace(mpeg))
            zeit = TryParseTimeFallback(mpeg);

        var code = (CodeTextBox.Text ?? string.Empty).Trim();
        if (code.Length > 0 && (_sp?.CodeCatalog is null || !_sp.CodeCatalog.TryGet(code, out _)))
        {
            ValidationStatus.Text = "Code ist nicht im Katalog vorhanden.";
            return;
        }

        if (_paramVm is not null && code.Length > 0)
        {
            _paramVm.Code = code;
            _paramVm.Validate();
            if (!_paramVm.IsValid)
            {
                ValidationStatus.Text = "Pflicht-Parameter fehlen oder sind ungueltig.";
                return;
            }
        }

        _entryVm.Code = code;
        _entryVm.Beschreibung = BeschreibungTextBox.Text ?? string.Empty;
        _entryVm.MeterStart = meterStart;
        _entryVm.MeterEnd = meterEnd;
        _entryVm.Zeit = zeit;
        _entryVm.Mpeg = string.IsNullOrWhiteSpace(mpeg) ? null : mpeg;
        _entryVm.Model.IsStreckenschaden = StreckenschadenCheckBox.IsChecked == true;

        if (_entryVm.Model.IsStreckenschaden)
        {
            if (!_entryVm.MeterStart.HasValue || !_entryVm.MeterEnd.HasValue)
            {
                ValidationStatus.Text = "Streckenschaden: MeterStart und MeterEnde sind Pflicht.";
                return;
            }
            if (_entryVm.MeterEnd < _entryVm.MeterStart)
            {
                ValidationStatus.Text = "Streckenschaden: MeterEnde muss groesser/gleich MeterStart sein.";
                return;
            }
        }

        if (_entryVm.Model.CodeMeta is null && _entryVm.Code.Length > 0)
        {
            _entryVm.Model.CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = _entryVm.Code,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        if (_entryVm.Model.CodeMeta is not null)
        {
            _entryVm.Model.CodeMeta.Code = _entryVm.Code;
            _entryVm.Model.CodeMeta.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (_paramVm is not null && _entryVm.Code.Length > 0)
            _paramVm.ApplyToEntry(_entryVm.Model);

        _entryVm.EnsureVsaDefaults();
        _entryVm.ApplyStreckenLogik();

        var vsaErrors = ValidateVsaFields();
        if (vsaErrors.Count > 0)
        {
            ValidationStatus.Text = string.Join(Environment.NewLine, vsaErrors);
            return;
        }

        if (string.IsNullOrWhiteSpace(_entryVm.Beschreibung)
            && _sp?.CodeCatalog is not null
            && _sp.CodeCatalog.TryGet(_entryVm.Code, out var def))
        {
            _entryVm.Beschreibung = BuildDefaultDescription(def, _entryVm.Parameters, _entryVm.MeterStart, _entryVm.MeterEnd);
        }

        if (_entryVm.Model.Ai is not null)
        {
            var suggested = _entryVm.Model.Ai.SuggestedCode?.Trim();
            _entryVm.Model.Ai.FinalCode = _entryVm.Code;
            _entryVm.Model.Ai.Accepted = !string.IsNullOrWhiteSpace(suggested)
                                          && string.Equals(suggested, _entryVm.Code, StringComparison.OrdinalIgnoreCase);
        }

        DialogResult = true;
        Close();
    }

    private List<string> ValidateVsaFields()
    {
        var errors = new List<string>();
        var code = (_entryVm.Code ?? string.Empty).Trim();
        if (code.Length == 0)
            return errors;

        // Distanz Pflicht bei VSA-Kanal-/Schachtschaden
        var distanz = _entryVm.VsaDistanz;
        if (string.IsNullOrWhiteSpace(distanz))
        {
            errors.Add("VSA: Distanz (m) ist erforderlich.");
        }

        // Streckenschaden -> Strecke erforderlich
        if (_entryVm.Model.IsStreckenschaden)
        {
            if (string.IsNullOrWhiteSpace(_entryVm.VsaStrecke))
            {
                errors.Add("VSA: Strecke (A/B/C) ist erforderlich.");
            }
        }

        // Schachtbereich nur erlaubte Werte
        var schacht = _entryVm.VsaSchachtbereich;
        if (!string.IsNullOrWhiteSpace(schacht))
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A", "B", "D", "F", "H", "I", "J" };
            if (!allowed.Contains(schacht.Trim()))
            {
                errors.Add("VSA: Schachtbereich nur A/B/D/F/H/I/J.");
            }
        }

        if (_sp?.CodeCatalog is not null && _sp.CodeCatalog.TryGet(code, out var def))
        {
            var hasClock = def.Parameters.Any(p => string.Equals(p.Type, "clock", StringComparison.OrdinalIgnoreCase));
            if (hasClock && string.IsNullOrWhiteSpace(_entryVm.VsaUhrVon))
            {
                errors.Add("VSA: Uhr von ist erforderlich.");
            }

            var hasQuant1 = def.Parameters.Any(p => string.Equals(p.Name, "Quant1", StringComparison.OrdinalIgnoreCase)
                                                    || string.Equals(p.Name, "Quantifizierung 1", StringComparison.OrdinalIgnoreCase));
            var hasQuant2 = def.Parameters.Any(p => string.Equals(p.Name, "Quant2", StringComparison.OrdinalIgnoreCase)
                                                    || string.Equals(p.Name, "Quantifizierung 2", StringComparison.OrdinalIgnoreCase));

            if (!hasQuant1 && !string.IsNullOrWhiteSpace(_entryVm.VsaQ1))
            {
                errors.Add("VSA: Quantifizierung 1 ist fuer diesen Code nicht vorgesehen.");
            }

            if (!hasQuant2 && !string.IsNullOrWhiteSpace(_entryVm.VsaQ2))
            {
                errors.Add("VSA: Quantifizierung 2 ist fuer diesen Code nicht vorgesehen.");
            }
        }

        return errors;
    }

    private string ResolveProjectFolder()
    {
        if (!string.IsNullOrWhiteSpace(_projectFolder))
            return _projectFolder;

        var fromSettings = _sp?.Settings.LastProjectPath;
        if (!string.IsNullOrWhiteSpace(fromSettings))
        {
            var dir = Path.GetDirectoryName(fromSettings);
            if (!string.IsNullOrWhiteSpace(dir))
                return dir;
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private string? ResolveExistingPath(string? raw)
    {
        var path = raw?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (File.Exists(path))
            return path;

        if (Path.IsPathRooted(path))
            return null;

        var baseDir = ResolveProjectFolder();
        if (string.IsNullOrWhiteSpace(baseDir))
            return null;

        var combined = Path.GetFullPath(Path.Combine(baseDir, path));
        return File.Exists(combined) ? combined : null;
    }

    private IReadOnlyList<string> ResolveImagePaths(IReadOnlyList<string> rawPaths)
    {
        var result = new List<string>();
        foreach (var raw in rawPaths)
        {
            var path = ResolveExistingPath(raw);
            if (string.IsNullOrWhiteSpace(path))
                continue;
            if (!result.Contains(path, StringComparer.OrdinalIgnoreCase))
                result.Add(path);
        }

        return result;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;
        return value.Substring(0, maxLength) + "...";
    }

    private static bool TryParseOptionalDouble(string raw, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var normalized = raw.Trim().Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return false;

        value = parsed;
        return true;
    }

    private static bool TryParseOptionalTimeSpan(string raw, out TimeSpan? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static TimeSpan? TryParseTimeFallback(string raw)
    {
        if (TryParseOptionalTimeSpan(raw, out var value))
            return value;
        return null;
    }

    private static string FormatTime(TimeSpan value)
        => value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");

    private static string BuildDefaultDescription(
        AuswertungPro.Next.Application.Protocol.CodeDefinition def,
        IReadOnlyDictionary<string, string> parameters,
        double? meterStart,
        double? meterEnd)
    {
        var title = def.Title ?? string.Empty;
        var parts = new List<string>();

        if (parameters is not null && parameters.Count > 0)
        {
            foreach (var p in def.Parameters)
            {
                if (!parameters.TryGetValue(p.Name, out var value) || string.IsNullOrWhiteSpace(value))
                    continue;
                var unit = string.IsNullOrWhiteSpace(p.Unit) ? "" : $" {p.Unit}";
                parts.Add($"{p.Name}={value}{unit}".Trim());
            }
        }

        if (def.RequiresRange && meterStart.HasValue && meterEnd.HasValue)
        {
            parts.Add($"Strecke {meterStart:0.00}-{meterEnd:0.00} m");
        }

        if (parts.Count == 0)
            return title;

        return $"{title} ({string.Join(", ", parts)})";
    }

    private void SetZeitFromVideo()
    {
        if (!PlayerWindow.TryGetCurrentTime(out var time))
        {
            ValidationStatus.Text = "Kein Video aktiv. Bitte zuerst Video oeffnen.";
            return;
        }

        ZeitTextBox.Text = FormatTime(time);
        if (string.IsNullOrWhiteSpace(MpegTextBox.Text))
            MpegTextBox.Text = FormatTime(time);
        ValidationStatus.Text = "Zeit aus Video uebernommen.";
    }

    private void SeekToZeit()
    {
        if (!TryParseOptionalTimeSpan(ZeitTextBox.Text, out var zeit) || zeit is null)
        {
            ValidationStatus.Text = "Zeit ist ungueltig.";
            return;
        }

        if (!PlayerWindow.TrySeekTo(zeit.Value))
            ValidationStatus.Text = "Video ist nicht aktiv.";
        else
            ValidationStatus.Text = "Video zu Zeitposition gesetzt.";
    }
}

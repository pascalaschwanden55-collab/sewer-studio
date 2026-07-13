using System.ComponentModel;
using System.Runtime.CompilerServices;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Protocol;

public sealed record ProtocolEntryKiSuggestionRequest(
    string ProjectFolderAbs,
    string? HaltungId,
    string MeterStartText,
    string MeterEndText,
    string ZeitText,
    string ExistingCode,
    string ExistingText,
    string? VideoPathAbs,
    IReadOnlyList<string>? ImagePathsAbs);

public sealed record ProtocolEntryKiSuggestionResult(
    AiSuggestion? Suggestion,
    string StatusText,
    string ValidationText,
    string? AcceptedCode,
    bool RequestStarted);

/// <summary>
/// Kapselt den KI-Vorschlagsablauf des Protokoll-Editors. Der Dialog liefert nur die
/// sichtbaren Eingaben und zeigt das Ergebnis an; Validierung, Request-Aufbau und Fehlerbehandlung
/// bleiben dadurch ohne WPF-Fenster testbar.
/// </summary>
public sealed class ProtocolEntryEditorKiViewModel : INotifyPropertyChanged
{
    private readonly IProtocolAiService _aiService;
    private readonly ProtocolEntryVM? _entryVm;
    private readonly Action<string>? _warningLogger;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ProtocolEntryEditorViewModel Editor { get; }
    public AiSuggestion? KiSuggestion { get; private set; }
    public bool IsKiLoading { get; private set; }
    public string KiStatus { get; private set; } = string.Empty;

    public ProtocolEntryEditorKiViewModel(
        ProtocolEntryEditorViewModel editor,
        IProtocolAiService aiService,
        ProtocolEntryVM? entryVm = null,
        Action<string>? warningLogger = null)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _entryVm = entryVm;
        _warningLogger = warningLogger;
    }

    /// <summary>
    /// Bestehende öffentliche Fassade für ältere Aufrufer. Neue Aufrufer geben den vollständigen
    /// Editor-Kontext über <see cref="SuggestAsync"/> weiter.
    /// </summary>
    public async Task GetKiSuggestionAsync()
    {
        await SuggestAsync(new ProtocolEntryKiSuggestionRequest(
            ProjectFolderAbs: string.Empty,
            HaltungId: null,
            MeterStartText: string.Empty,
            MeterEndText: string.Empty,
            ZeitText: string.Empty,
            ExistingCode: string.Empty,
            ExistingText: string.Empty,
            VideoPathAbs: null,
            ImagePathsAbs: null));
    }

    public async Task<ProtocolEntryKiSuggestionResult> SuggestAsync(
        ProtocolEntryKiSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsKiLoading)
            return Result(KiSuggestion, KiStatus, string.Empty, acceptedCode: null, requestStarted: false);

        if (_aiService is NoopProtocolAiService)
        {
            return Result(
                suggestion: null,
                statusText: string.Empty,
                validationText: "KI ist deaktiviert. Setze SEWERSTUDIO_AI_ENABLED=1 und starte neu.",
                acceptedCode: null,
                requestStarted: false);
        }

        var allowedCodes = Editor.AllowedCodes;
        if (allowedCodes.Count == 0)
        {
            return Result(
                suggestion: null,
                statusText: string.Empty,
                validationText: "KI nicht möglich: Code-Katalog ist leer.",
                acceptedCode: null,
                requestStarted: false);
        }

        if (!ProtocolEntryInputNormalizer.TryParseOptionalDouble(request.MeterStartText, out var meterStart))
            return InvalidInput("MeterStart ist ungültig.");

        if (!ProtocolEntryInputNormalizer.TryParseOptionalDouble(request.MeterEndText, out var meterEnd))
            return InvalidInput("MeterEnd ist ungültig.");

        if (!ProtocolEntryInputNormalizer.TryParseOptionalTimeSpan(request.ZeitText, out var zeit))
            return InvalidInput("Zeit ist ungültig.");

        SetLoading(true, "KI-Vorschlag wird geladen...");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var input = new AiInput(
                ProjectFolderAbs: request.ProjectFolderAbs ?? string.Empty,
                HaltungId: NullIfWhiteSpace(request.HaltungId),
                Meter: meterStart ?? meterEnd,
                ExistingCode: NullIfWhiteSpace(request.ExistingCode),
                ExistingText: NullIfWhiteSpace(request.ExistingText),
                AllowedCodes: allowedCodes,
                VideoPathAbs: NullIfWhiteSpace(request.VideoPathAbs),
                Zeit: zeit,
                ImagePathsAbs: request.ImagePathsAbs is { Count: > 0 } ? request.ImagePathsAbs : null);

            var suggestion = await _aiService.SuggestAsync(input, cancellationToken);
            KiSuggestion = suggestion;
            OnPropertyChanged(nameof(KiSuggestion));

            if (suggestion is null)
            {
                return Result(
                    suggestion: null,
                    statusText: "Kein KI-Vorschlag erhalten.",
                    validationText: string.Empty,
                    acceptedCode: null,
                    requestStarted: true);
            }

            _entryVm?.ApplyAiSuggestionToModelAndVm(suggestion);

            var suggestedCode = suggestion.SuggestedCode?.Trim();
            string statusText;
            string? acceptedCode = null;
            if (string.IsNullOrWhiteSpace(suggestedCode))
            {
                statusText = $"KI-Vorschlag ohne Code ({suggestion.Confidence:P0}).";
            }
            else if (Editor.IsKnownCode(suggestedCode))
            {
                acceptedCode = suggestedCode.ToUpperInvariant();
                statusText = $"KI-Vorschlag übernommen: {suggestedCode} ({suggestion.Confidence:P0}).";
            }
            else
            {
                statusText = $"KI-Code '{suggestedCode}' ist nicht im Katalog.";
            }

            var validationText = string.IsNullOrWhiteSpace(suggestion.ReasonShort)
                ? string.Empty
                : "KI-Hinweis: " + Truncate(suggestion.ReasonShort, 180);

            return Result(suggestion, statusText, validationText, acceptedCode, requestStarted: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result(
                suggestion: null,
                statusText: "KI-Vorschlag abgebrochen.",
                validationText: string.Empty,
                acceptedCode: null,
                requestStarted: true);
        }
        catch (Exception ex)
        {
            ReportWarning($"[ProtocolEntryEditor.KI] Vorschlag fehlgeschlagen: {ex}");
            return Result(
                suggestion: null,
                statusText: "KI-Vorschlag fehlgeschlagen.",
                validationText: "KI-Fehler. Details stehen im Tageslog.",
                acceptedCode: null,
                requestStarted: true);
        }
        finally
        {
            SetLoading(false, KiStatus);
        }
    }

    private ProtocolEntryKiSuggestionResult InvalidInput(string message)
        => Result(
            suggestion: null,
            statusText: string.Empty,
            validationText: message,
            acceptedCode: null,
            requestStarted: false);

    private ProtocolEntryKiSuggestionResult Result(
        AiSuggestion? suggestion,
        string statusText,
        string validationText,
        string? acceptedCode,
        bool requestStarted)
    {
        KiStatus = statusText;
        OnPropertyChanged(nameof(KiStatus));
        return new ProtocolEntryKiSuggestionResult(
            suggestion,
            statusText,
            validationText,
            acceptedCode,
            requestStarted);
    }

    private void SetLoading(bool isLoading, string status)
    {
        IsKiLoading = isLoading;
        KiStatus = status;
        OnPropertyChanged(nameof(IsKiLoading));
        OnPropertyChanged(nameof(KiStatus));
    }

    private void ReportWarning(string message)
    {
        if (_warningLogger is not null)
        {
            _warningLogger(message);
            return;
        }

        BestEffort.ReportWarning(message);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "...";

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

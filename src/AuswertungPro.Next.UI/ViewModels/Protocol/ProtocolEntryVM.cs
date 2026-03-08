using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Protocol;

public sealed class ProtocolEntryVM : INotifyPropertyChanged
{
    public ProtocolEntry Model { get; }

    // --- KI-Properties (VM) ---
    public string? AiSuggestedCode { get; private set; }
    public double AiConfidence { get; private set; }
    public string? AiReason { get; private set; }
    public List<string> AiFlags { get; } = new();
    public bool AiWasAccepted { get; private set; }
    public string? AiFinalCode { get; set; }

    public ProtocolEntryVM(ProtocolEntry model)
    {
        Model = model;
        LoadAiFromModel();
    }

    public string Code
    {
        get => Model.Code;
        set
        {
            if (Model.Code == value)
                return;
            Model.Code = value;
            OnPropertyChanged();
        }
    }

    public string Beschreibung
    {
        get => Model.Beschreibung;
        set
        {
            if (Model.Beschreibung == value)
                return;
            Model.Beschreibung = value;
            OnPropertyChanged();
        }
    }

    public double? MeterStart
    {
        get => Model.MeterStart;
        set
        {
            if (Model.MeterStart == value)
                return;
            Model.MeterStart = value;
            OnPropertyChanged();
        }
    }

    public double? MeterEnd
    {
        get => Model.MeterEnd;
        set
        {
            if (Model.MeterEnd == value)
                return;
            Model.MeterEnd = value;
            OnPropertyChanged();
        }
    }

    public string? Mpeg
    {
        get => Model.Mpeg;
        set
        {
            if (Model.Mpeg == value)
                return;
            Model.Mpeg = value;
            OnPropertyChanged();
        }
    }

    public TimeSpan? Zeit
    {
        get => Model.Zeit;
        set
        {
            if (Model.Zeit == value)
                return;
            Model.Zeit = value;
            OnPropertyChanged();
        }
    }

    public string? Severity => Model.CodeMeta?.Severity;
    public int? Count => Model.CodeMeta?.Count;
    public string? CodeNotes => Model.CodeMeta?.Notes;
    public IReadOnlyDictionary<string, string> Parameters => Model.CodeMeta?.Parameters ?? new Dictionary<string, string>();

    public int FotoCount => Model.FotoPaths.Count;

    // --- VSA-KEK Parameter Mapping (CodeMeta.Parameters) ---
    public string? VsaDistanz
    {
        get => GetFirstParam("vsa.distanz", "Distance");
        set => SetParamAliases(value, "vsa.distanz", "Distance");
    }

    public string? VsaUhrVon
    {
        get => GetFirstParam("vsa.uhr.von", "ClockPos1");
        set => SetParamAliases(value, "vsa.uhr.von", "ClockPos1");
    }

    public string? VsaUhrBis
    {
        get => GetFirstParam("vsa.uhr.bis", "ClockPos2");
        set => SetParamAliases(value, "vsa.uhr.bis", "ClockPos2");
    }

    public string? VsaQ1
    {
        get => GetFirstParam("vsa.q1", "Q1", "Quantifizierung1");
        set => SetParamAliases(value, "vsa.q1", "Q1", "Quantifizierung1");
    }

    public string? VsaQ2
    {
        get => GetFirstParam("vsa.q2", "Q2", "Quantifizierung2");
        set => SetParamAliases(value, "vsa.q2", "Q2", "Quantifizierung2");
    }

    public string? VsaStrecke
    {
        get => GetParam("vsa.strecke");
        set => SetParam("vsa.strecke", value);
    }

    public bool VsaVerbindung
    {
        get
        {
            var v = GetParam("vsa.verbindung");
            return string.Equals(v, "ja", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(v, "1", StringComparison.OrdinalIgnoreCase);
        }
        set => SetParam("vsa.verbindung", value ? "ja" : null);
    }

    public string? VsaVideo
    {
        get => GetFirstParam("vsa.video", "TimeCtr");
        set => SetParamAliases(value, "vsa.video", "TimeCtr");
    }

    public string? VsaAnsicht
    {
        get => GetParam("vsa.ansicht");
        set => SetParam("vsa.ansicht", value);
    }

    public string? VsaEz
    {
        get => GetParam("vsa.ez");
        set => SetParam("vsa.ez", value);
    }

    public string? VsaAnmerkung
    {
        get => GetParam("vsa.anmerkung");
        set => SetParam("vsa.anmerkung", value);
    }

    public string? VsaSchachtbereich
    {
        get => GetParam("vsa.schachtbereich");
        set => SetParam("vsa.schachtbereich", value);
    }

    public void ApplyCodeSelection(
        string code,
        IReadOnlyDictionary<string, string> parameters,
        double? meterStart,
        double? meterEnd,
        string? severity,
        int? count,
        string? notes)
    {
        Code = code;
        MeterStart = meterStart;
        MeterEnd = meterEnd;

        var normalizedParams = NormalizeSecAliases(parameters, code);

        Model.CodeMeta ??= new ProtocolEntryCodeMeta();
        Model.CodeMeta.Code = code;
        Model.CodeMeta.Parameters = normalizedParams;
        Model.CodeMeta.Severity = string.IsNullOrWhiteSpace(severity) ? null : severity.Trim();
        Model.CodeMeta.Count = count;
        Model.CodeMeta.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Model.CodeMeta.UpdatedAt = DateTimeOffset.UtcNow;

        OnPropertyChanged(nameof(Severity));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(CodeNotes));
        OnPropertyChanged(nameof(Parameters));
    }

    public void EnsureVsaDefaults()
    {
        if (string.IsNullOrWhiteSpace(GetFirstParam("vsa.code", "Code")) && !string.IsNullOrWhiteSpace(Code))
            SetParamAliases(Code, "vsa.code", "Code");

        if (string.IsNullOrWhiteSpace(VsaDistanz) && MeterStart is not null)
            VsaDistanz = MeterStart.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(VsaVideo) && Zeit is not null)
            VsaVideo = FormatTime(Zeit.Value);
    }

    public void ApplyStreckenLogik()
    {
        if (!Model.IsStreckenschaden)
        {
            SetParam("vsa.strecke", null);
            return;
        }

        var strecke = (GetParam("vsa.strecke") ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(strecke))
        {
            SetParam("vsa.strecke", "A1");
            return;
        }

        if (strecke.Length == 1 && (strecke == "A" || strecke == "B" || strecke == "C"))
        {
            SetParam("vsa.strecke", strecke + "1");
            return;
        }

        if (strecke.Length >= 2 && (strecke[0] == 'A' || strecke[0] == 'B' || strecke[0] == 'C')
            && strecke.Substring(1).All(char.IsDigit))
        {
            SetParam("vsa.strecke", strecke);
            return;
        }

        SetParam("vsa.strecke", "A1");
    }

    // --- KI/Model Mapping Methoden ---
    public void ApplyAiSuggestionToModelAndVm(AiSuggestion s)
    {
        Model.Ai = new ProtocolEntryAiMeta
        {
            SuggestedCode = s.SuggestedCode,
            Confidence = s.Confidence,
            Reason = s.Reason,
            Flags = s.Flags.ToList()
        };
        LoadAiFromModel();
    }

    public void LoadAiFromModel()
    {
        var ai = Model.Ai;
        AiSuggestedCode = ai?.SuggestedCode;
        AiConfidence = ai?.Confidence ?? 0;
        AiReason = ai?.Reason;
        AiFlags.Clear();
        if (ai?.Flags != null)
            AiFlags.AddRange(ai.Flags);
        AiWasAccepted = ai?.Accepted ?? false;
        AiFinalCode = ai?.FinalCode;
        OnPropertyChanged(nameof(AiSuggestedCode));
        OnPropertyChanged(nameof(AiConfidence));
        OnPropertyChanged(nameof(AiReason));
        OnPropertyChanged(nameof(AiFlags));
        OnPropertyChanged(nameof(AiWasAccepted));
        OnPropertyChanged(nameof(AiFinalCode));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string? GetParam(string key)
    {
        if (Model.CodeMeta?.Parameters is null)
            return null;
        return Model.CodeMeta.Parameters.TryGetValue(key, out var v) ? v : null;
    }

    private string? GetFirstParam(params string[] keys)
    {
        if (Model.CodeMeta?.Parameters is null || keys.Length == 0)
            return null;

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (!Model.CodeMeta.Parameters.TryGetValue(key, out var value))
                continue;
            if (string.IsNullOrWhiteSpace(value))
                continue;
            return value;
        }

        return null;
    }

    private void SetParam(string key, string? value)
    {
        Model.CodeMeta ??= new ProtocolEntryCodeMeta { Code = Model.Code };
        Model.CodeMeta.Parameters ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(value))
        {
            Model.CodeMeta.Parameters.Remove(key);
        }
        else
        {
            Model.CodeMeta.Parameters[key] = value.Trim();
        }

        OnPropertyChanged(nameof(Parameters));
    }

    private void SetParamAliases(string? value, params string[] keys)
    {
        if (keys.Length == 0)
            return;

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            SetParam(key, value);
        }
    }

    private static Dictionary<string, string> NormalizeSecAliases(
        IReadOnlyDictionary<string, string> parameters,
        string code)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in parameters)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;
            var value = kv.Value?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;
            result[kv.Key.Trim()] = value;
        }

        void Mirror(string[] keys)
        {
            var value = keys
                .Select(k => result.TryGetValue(k, out var v) ? v : null)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            if (string.IsNullOrWhiteSpace(value))
                return;
            foreach (var key in keys)
                result[key] = value;
        }

        Mirror(new[] { "vsa.code", "Code" });
        Mirror(new[] { "vsa.distanz", "Distance" });
        Mirror(new[] { "vsa.video", "TimeCtr" });
        Mirror(new[] { "vsa.uhr.von", "ClockPos1" });
        Mirror(new[] { "vsa.uhr.bis", "ClockPos2" });
        Mirror(new[] { "vsa.q1", "Q1", "Quantifizierung1" });
        Mirror(new[] { "vsa.q2", "Q2", "Quantifizierung2" });

        if (!string.IsNullOrWhiteSpace(code))
        {
            result["vsa.code"] = code.Trim();
            result["Code"] = code.Trim();
        }

        return result;
    }

    private static string FormatTime(TimeSpan value)
        => value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");
}

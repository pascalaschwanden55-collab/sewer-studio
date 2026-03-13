using System.ComponentModel;
using System.Runtime.CompilerServices;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Protocol;

public class ProtocolEntryEditorViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string Code
    {
        get => _code;
        set
        {
            _code = value;
            OnPropertyChanged();
            UpdateDefinition();
        }
    }

    private string _code = string.Empty;

    public AppProtocol.CodeDefinition? Definition { get; private set; }
    public List<CodeParameterViewModel> Parameters { get; } = new();
    public IReadOnlyList<string> AllowedCodes => _catalog.AllowedCodes();
    public string ValidationStatus { get; private set; } = string.Empty;
    public IReadOnlyList<string> ValidationMessages { get; private set; } = Array.Empty<string>();
    public bool IsValid { get; private set; }

    private readonly AppProtocol.ICodeCatalogProvider _catalog;

    public ProtocolEntryEditorViewModel(AppProtocol.ICodeCatalogProvider catalog)
    {
        _catalog = catalog;
    }

    private void UpdateDefinition()
    {
        Definition = _catalog.TryGet(Code, out var def) ? def : null;

        Parameters.Clear();
        if (Definition != null)
        {
            foreach (var param in Definition.Parameters)
                Parameters.Add(new CodeParameterViewModel(param));
        }

        Validate();
        OnPropertyChanged(nameof(Definition));
        OnPropertyChanged(nameof(Parameters));
    }

    public void Validate()
    {
        var messages = new List<string>();

        if (Definition is null)
        {
            messages.Add("Code nicht im Katalog.");
        }
        else
        {
            foreach (var parameter in Parameters)
            {
                parameter.Validate();
                if (parameter.IsValid)
                    continue;

                var detail = string.IsNullOrWhiteSpace(parameter.ErrorMessage)
                    ? "ungueltig"
                    : parameter.ErrorMessage;
                messages.Add($"{parameter.DisplayName}: {detail}");
            }
        }

        IsValid = messages.Count == 0;
        ValidationMessages = messages;
        ValidationStatus = IsValid
            ? "Code gueltig."
            : string.Join(Environment.NewLine, ValidationMessages.Take(8));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(ValidationMessages));
        OnPropertyChanged(nameof(ValidationStatus));
    }

    public void LoadFromEntry(AuswertungPro.Next.Domain.Protocol.ProtocolEntry entry)
    {
        Code = entry.Code ?? string.Empty;

        foreach (var param in Parameters)
        {
            if (entry.CodeMeta?.Parameters is null)
                continue;
            if (entry.CodeMeta.Parameters.TryGetValue(param.Name, out var v))
                param.Value = v ?? string.Empty;
        }
    }

    public void ApplyToEntry(AuswertungPro.Next.Domain.Protocol.ProtocolEntry entry)
    {
        entry.Code = Code ?? string.Empty;

        entry.CodeMeta ??= new AuswertungPro.Next.Domain.Protocol.ProtocolEntryCodeMeta();
        entry.CodeMeta.Code = entry.Code;
        entry.CodeMeta.Parameters ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Merge parameter values to avoid wiping VSA/other metadata in CodeMeta.Parameters.
        var paramNames = new HashSet<string>(Parameters.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var name in paramNames)
            entry.CodeMeta.Parameters.Remove(name);

        foreach (var p in Parameters)
        {
            if (string.IsNullOrWhiteSpace(p.Value))
                continue;
            entry.CodeMeta.Parameters[p.Name] = p.Value.Trim();
        }

        entry.CodeMeta.UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public class CodeParameterViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string Name { get; }
    public string Type { get; }
    public string? Unit { get; }
    public IReadOnlyList<string> AllowedValues { get; }
    public bool Required { get; }
    public bool HasAllowedValues => AllowedValues.Count > 0;
    public string DisplayName => Required ? $"{Name} *" : Name;
    public string? UnitSuffix => string.IsNullOrWhiteSpace(Unit) ? null : Unit;

    private string _value = string.Empty;
    public string Value
    {
        get => _value;
        set
        {
            _value = value;
            OnPropertyChanged();
            Validate();
        }
    }

    public bool IsValid { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    public CodeParameterViewModel(AppProtocol.CodeParameter param)
    {
        Name = param.Name;
        Type = param.Type;
        Unit = param.Unit;
        AllowedValues = param.AllowedValues?.ToList() ?? new List<string>();
        Required = param.Required;
        Validate();
    }

    public void Validate()
    {
        if (Required && string.IsNullOrWhiteSpace(Value))
        {
            IsValid = false;
            ErrorMessage = "Pflichtfeld.";
        }
        else if (string.Equals(Type, "enum", StringComparison.OrdinalIgnoreCase) && AllowedValues.Count > 0)
        {
            IsValid = string.IsNullOrWhiteSpace(Value) || AllowedValues.Contains(Value, StringComparer.OrdinalIgnoreCase);
            ErrorMessage = IsValid
                ? string.Empty
                : $"Nur erlaubt: {string.Join(", ", AllowedValues)}";
        }
        else if (string.Equals(Type, "number", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(Value))
            {
                IsValid = !Required;
                ErrorMessage = IsValid ? string.Empty : "Pflichtfeld.";
            }
            else
            {
                var normalized = Value.Trim().Replace(',', '.');
                IsValid = double.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
                ErrorMessage = IsValid ? string.Empty : "Numerischer Wert erwartet.";
            }
        }
        else
        {
            IsValid = true;
            ErrorMessage = string.Empty;
        }

        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(ErrorMessage));
    }
}

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Windows;

public sealed class RecordDetailItem : INotifyPropertyChanged
{
    private readonly Action<string> _commitValue;
    private string _value;

    public RecordDetailItem(
        string label,
        string value,
        Action<string> commitValue,
        bool isReadOnly = false,
        bool isMultiline = false,
        bool isCombo = false,
        bool allowFreeText = false,
        bool digitsOnly = false,
        IEnumerable<string>? options = null,
        ICommand? editOptionsCommand = null,
        ICommand? previewOptionsCommand = null,
        ICommand? resetOptionsCommand = null,
        ICommand? addOptionCommand = null,
        ICommand? removeOptionCommand = null)
    {
        Label = label;
        _value = value ?? string.Empty;
        _commitValue = commitValue;
        IsReadOnly = isReadOnly;
        IsMultiline = isMultiline;
        IsCombo = isCombo;
        AllowFreeText = allowFreeText;
        DigitsOnly = digitsOnly;
        Options = options is null ? Array.Empty<string>() : new List<string>(options);
        EditOptionsCommand = editOptionsCommand;
        PreviewOptionsCommand = previewOptionsCommand;
        ResetOptionsCommand = resetOptionsCommand;
        AddOptionCommand = addOptionCommand;
        RemoveOptionCommand = removeOptionCommand;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; }
    public bool IsReadOnly { get; }
    public bool IsMultiline { get; }
    public bool IsCombo { get; }
    public bool AllowFreeText { get; }
    public bool DigitsOnly { get; }
    public IEnumerable<string> Options { get; }
    public ICommand? EditOptionsCommand { get; }
    public ICommand? PreviewOptionsCommand { get; }
    public ICommand? ResetOptionsCommand { get; }
    public ICommand? AddOptionCommand { get; }
    public ICommand? RemoveOptionCommand { get; }
    public bool CanEdit => !IsReadOnly;
    public bool HasManagedOptions =>
        EditOptionsCommand is not null ||
        PreviewOptionsCommand is not null ||
        ResetOptionsCommand is not null ||
        AddOptionCommand is not null ||
        RemoveOptionCommand is not null;

    public string Value
    {
        get => _value;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_value, next, StringComparison.Ordinal))
                return;

            _value = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(SelectedOption));
            _commitValue(_value);
        }
    }

    public string SelectedOption
    {
        get => _value;
        set => Value = value ?? string.Empty;
    }

    public bool IsEmpty => string.IsNullOrWhiteSpace(_value);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record RecordDetailGroup(string Title, string Description, IReadOnlyList<RecordDetailItem> Items);

public sealed class RecordDetailEditorTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? MultilineTemplate { get; set; }
    public DataTemplate? EditableComboTemplate { get; set; }
    public DataTemplate? FixedComboTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        _ = container;

        if (item is not RecordDetailItem detailItem)
            return TextTemplate;

        if (detailItem.IsCombo)
            return detailItem.AllowFreeText ? EditableComboTemplate : FixedComboTemplate;

        return detailItem.IsMultiline ? MultilineTemplate : TextTemplate;
    }
}

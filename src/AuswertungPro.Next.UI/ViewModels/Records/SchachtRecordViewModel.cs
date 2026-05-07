using System;
using System.ComponentModel;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.ViewModels.Records;

/// <summary>
/// ARCH-H1 Phase 1 (Audit 2026-04-23): MVVM-Wrapper um den Domain-POCO
/// <see cref="SchachtRecord"/>. Spiegelt das Pattern von
/// <see cref="HaltungRecordViewModel"/>.
/// </summary>
public sealed partial class SchachtRecordViewModel : ObservableObject, IDisposable
{
    public SchachtRecord Model { get; }

    public SchachtRecordViewModel(SchachtRecord model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Model.PropertyChanged += OnModelPropertyChanged;
    }

    public Guid Id => Model.Id;
    public DateTime CreatedAtUtc => Model.CreatedAtUtc;
    public DateTime ModifiedAtUtc => Model.ModifiedAtUtc;

    public string this[string fieldName] => Model.GetFieldValue(fieldName);

    public void SetFieldValue(string fieldName, string? value)
    {
        Model.SetFieldValue(fieldName, value);
        OnPropertyChanged($"Item[{fieldName}]");
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) OnPropertyChanged(string.Empty);
        else OnPropertyChanged(e.PropertyName);
    }

    public void Dispose()
    {
        Model.PropertyChanged -= OnModelPropertyChanged;
    }
}

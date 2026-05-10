using System;
using System.Collections.Generic;
using System.ComponentModel;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.ViewModels.Records;

/// <summary>
/// P2.1 Domain-INPC Entkopplung — Wrapper-VM fuer
/// <see cref="SchachtRecord"/> (additiv, Step 1).
///
/// Analog zu <see cref="HaltungRecordViewModel"/> — Wrapper subscribt
/// Model.PropertyChanged und leitet 1:1 weiter, plus Indexer-Event.
/// </summary>
public sealed partial class SchachtRecordViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    public SchachtRecord Model { get; }

    public SchachtRecordViewModel(SchachtRecord model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Model.PropertyChanged += OnModelPropertyChanged;
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e);
        if (e.PropertyName is { } name &&
            name.StartsWith("Fields[", StringComparison.Ordinal) &&
            name.EndsWith("]", StringComparison.Ordinal))
        {
            var inner = name.Substring("Fields[".Length, name.Length - "Fields[".Length - 1);
            OnPropertyChanged($"Item[{inner}]");
        }
    }

    public string this[string fieldName] => Model.GetFieldValue(fieldName);

    public void SetFieldValue(string fieldName, string? value)
        => Model.SetFieldValue(fieldName, value);

    public Guid Id => Model.Id;
    public IReadOnlyDictionary<string, string> Fields => Model.Fields;
    public DateTime CreatedAtUtc => Model.CreatedAtUtc;
    public DateTime ModifiedAtUtc => Model.ModifiedAtUtc;

    public AuswertungPro.Next.Domain.Protocol.ProtocolDocument? Protocol
    {
        get => Model.Protocol;
        set
        {
            if (ReferenceEquals(Model.Protocol, value)) return;
            Model.Protocol = value;
            OnPropertyChanged();
        }
    }

    public static SchachtRecordViewModel Wrap(SchachtRecord record) => new(record);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Model.PropertyChanged -= OnModelPropertyChanged;
    }
}

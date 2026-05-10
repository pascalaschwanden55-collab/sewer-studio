using System;
using System.Collections.Generic;
using System.ComponentModel;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.ViewModels.Records;

/// <summary>
/// P2.1 Domain-INPC Entkopplung — Wrapper-VM fuer
/// <see cref="HaltungRecord"/> (additiv, Step 1).
///
/// Aktuell (2026-05-10): HaltungRecord traegt selbst INPC. Wrapper
/// subscribt <c>Model.PropertyChanged</c> und leitet 1:1 weiter — so
/// kann der Caller bereits jetzt auf den VM-Wrapper umstellen, ohne
/// dass HaltungRecord schon POCO sein muesste.
///
/// Step 4 der Migration (Plan in
/// docs/adrs/2026-05-10-p2-1-domain-inpc-decouple.md) entfernt INPC
/// aus HaltungRecord. Der Wrapper bleibt dann der einzige
/// PropertyChanged-Quell — alle UI-Bindings haengen dann am VM.
///
/// Tests: <c>HaltungRecordViewModelTests</c> in Pipeline.Tests.
/// </summary>
public sealed partial class HaltungRecordViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    /// <summary>Underlying POCO. Bleibt die Wahrheit.</summary>
    public HaltungRecord Model { get; }

    public HaltungRecordViewModel(HaltungRecord model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Model.PropertyChanged += OnModelPropertyChanged;
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Domain-Events 1:1 als VM-Events durchreichen, plus den
        // Indexer-typischen "Item[fieldName]"-Event fuer XAML-Bindings
        // der Form <c>{Binding [Haltungsname]}</c>.
        OnPropertyChanged(e);
        if (e.PropertyName is { } name &&
            name.StartsWith("Fields[", StringComparison.Ordinal) &&
            name.EndsWith("]", StringComparison.Ordinal))
        {
            var inner = name.Substring("Fields[".Length, name.Length - "Fields[".Length - 1);
            OnPropertyChanged($"Item[{inner}]");
        }
    }

    /// <summary>Indexer fuer XAML-Bindings: <c>{Binding [Haltungsname]}</c>.
    /// Read-Only — Schreiben geht ueber <see cref="SetFieldValue"/>.</summary>
    public string this[string fieldName] => Model.GetFieldValue(fieldName);

    /// <summary>Setzt einen Field-Wert ueber das Domain-Modell.
    /// Respektiert die UserEdited-Schutzlogik im Domain-Modell —
    /// die PropertyChanged-Notifications kommen ueber den Event-
    /// Forward-Pfad zurueck zum VM und damit zu den XAML-Bindings.</summary>
    public void SetFieldValue(string fieldName, string? value, FieldSource source, bool userEdited)
        => Model.SetFieldValue(fieldName, value, source, userEdited);

    public Guid Id => Model.Id;
    public IReadOnlyDictionary<string, string> Fields => Model.Fields;
    public IReadOnlyDictionary<string, FieldMetadata> FieldMeta => Model.FieldMeta;
    public DateTime CreatedAtUtc => Model.CreatedAtUtc;
    public DateTime ModifiedAtUtc => Model.ModifiedAtUtc;

    public IReadOnlyList<VsaFinding> VsaFindings => Model.VsaFindings;

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

    public static HaltungRecordViewModel Wrap(HaltungRecord record) => new(record);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Model.PropertyChanged -= OnModelPropertyChanged;
    }
}

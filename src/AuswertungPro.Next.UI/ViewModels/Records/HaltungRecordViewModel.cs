using System;
using System.ComponentModel;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.ViewModels.Records;

/// <summary>
/// ARCH-H1 Phase 1 (Audit 2026-04-23): MVVM-Wrapper um den Domain-POCO
/// <see cref="HaltungRecord"/>. Liefert ObservableObject + indizierte
/// Property-Aenderungen, ohne dass der Domain-Record selbst INotifyPropertyChanged
/// kennen muss.
///
/// Migrationsstrategie:
///  - Schritt 1 (jetzt): Wrapper existiert, kann optional vorgesetzt werden.
///    Bestehende Bindings auf <c>HaltungRecord</c> direkt funktionieren weiter
///    (Record hat aktuell noch INotifyPropertyChanged).
///  - Schritt 2: ViewModels (DataPageViewModel, BuilderPageViewModel) auf
///    <c>ObservableCollection&lt;HaltungRecordViewModel&gt;</c> umstellen.
///  - Schritt 3: <see cref="HaltungRecord.PropertyChanged"/> + Implements
///    <see cref="INotifyPropertyChanged"/> aus Domain entfernen.
///
/// Die Properties spiegeln den Record 1:1 — XAML-Bindings koennen auf
/// gleichen Property-Namen umgestellt werden ohne Aenderung.
/// </summary>
public sealed partial class HaltungRecordViewModel : ObservableObject, IDisposable
{
    /// <summary>Der gewrappte Domain-Record.</summary>
    public HaltungRecord Model { get; }

    public HaltungRecordViewModel(HaltungRecord model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        // Wenn das Domain-Modell noch INotifyPropertyChanged hat, dessen
        // Aenderungs-Events 1:1 weiterleiten. Nach Schritt 3 (Domain-POCO)
        // faellt dieser Hook weg und Aenderungen gehen ueber SetFieldValue
        // (siehe unten).
        Model.PropertyChanged += OnModelPropertyChanged;
    }

    public Guid Id => Model.Id;
    public DateTime CreatedAtUtc => Model.CreatedAtUtc;
    public DateTime ModifiedAtUtc => Model.ModifiedAtUtc;

    /// <summary>
    /// Feldwert lesen — XAML-Binding via Indexer:
    /// <c>{Binding Fields[Haltungslaenge_m]}</c>.
    /// </summary>
    public string this[string fieldName] => Model.GetFieldValue(fieldName);

    /// <summary>
    /// Feldwert setzen mit Source-Tracking. Loest sowohl PropertyChanged
    /// auf <see cref="ModifiedAtUtc"/> als auch ein Indexer-PropertyChanged
    /// aus, damit DataGrid-Cells aktualisieren.
    /// </summary>
    public void SetFieldValue(string fieldName, string? value, FieldSource source, bool userEdited)
    {
        var prev = Model.GetFieldValue(fieldName);
        Model.SetFieldValue(fieldName, value, source, userEdited);
        // Falls das Domain-Modell selbst PropertyChanged feuert, hat
        // OnModelPropertyChanged das schon abgedeckt. Sonst manuell:
        if (!ReferenceEquals(prev, Model.GetFieldValue(fieldName)))
            OnPropertyChanged($"Item[{fieldName}]");
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 1:1 weiterleiten — Bindings auf VM-Properties triggern
        if (string.IsNullOrEmpty(e.PropertyName)) OnPropertyChanged(string.Empty);
        else OnPropertyChanged(e.PropertyName);
    }

    public void Dispose()
    {
        Model.PropertyChanged -= OnModelPropertyChanged;
    }
}

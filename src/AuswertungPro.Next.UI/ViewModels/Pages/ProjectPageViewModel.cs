using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class ProjectPageViewModel : ObservableObject, IDisposable
{
    private readonly ShellViewModel _shell;
    private readonly IDialogService _dialogs;
    private readonly IDropdownOptionsStore _dropdownOptions;
    private bool _disposed;

    public Project Project => _shell.Project;
    public IRelayCommand SaveCommand => _shell.SaveCommand;

    public IRelayCommand SaveAsCommand { get; }
    public IRelayCommand AnlegenCommand { get; }
    public IRelayCommand AbbrechenCommand { get; }

    [ObservableProperty] private string _draftName = string.Empty;

    /// <summary>True im Draft-Modus (neues, noch nicht angelegtes Projekt).</summary>
    public bool IsDraft => _shell.CurrentMode == ShellMode.Draft;

    /// <summary>True wenn kein Draft-Modus (für Sichtbarkeits-Binding ohne Inverter).</summary>
    public bool IsNotDraft => !IsDraft;

    // --- Sanieren/Eigentuemer Dropdown-Logik ---
    public ObservableCollection<string> SanierenOptions { get; }
    public ObservableCollection<string> EigentuemerOptions { get; }

    [ObservableProperty]
    private string _sanierenValue = string.Empty;

    [ObservableProperty]
    private string _eigentuemerValue = string.Empty;

    public IRelayCommand EditSanierenOptionsCommand { get; }
    public IRelayCommand PreviewSanierenOptionsCommand { get; }
    public IRelayCommand ResetSanierenOptionsCommand { get; }
    public IRelayCommand<object?> AddSanierenOptionCommand { get; }
    public IRelayCommand<object?> RemoveSanierenOptionCommand { get; }
    public IRelayCommand EditEigentuemerOptionsCommand { get; }
    public IRelayCommand PreviewEigentuemerOptionsCommand { get; }
    public IRelayCommand ResetEigentuemerOptionsCommand { get; }
    public IRelayCommand<object?> AddEigentuemerOptionCommand { get; }
    public IRelayCommand<object?> RemoveEigentuemerOptionCommand { get; }

    public ProjectPageViewModel(ShellViewModel shell, ServiceProvider services)
        : this(shell, services.Dialogs, services.DropdownOptions)
    {
    }

    public ProjectPageViewModel(
        ShellViewModel shell,
        IDialogService? dialogs = null,
        IDropdownOptionsStore? dropdownOptions = null)
    {
        _shell = shell;
        _dialogs = dialogs ?? new DialogService();
        _dropdownOptions = dropdownOptions ?? DropdownOptionsCompatibility.Default;

        // Dropdown-Optionen laden
        SanierenOptions = new ObservableCollection<string>(_dropdownOptions.LoadSanierenOptions());
        EigentuemerOptions = new ObservableCollection<string>(_dropdownOptions.LoadEigentuemerOptions());
        EnforceEigentuemerOptionsExact();

        // Projektwert ggf. temporaer ergaenzen
        if (Project.Metadata.TryGetValue("Sanieren", out var s) && !SanierenOptions.Contains(s))
            SanierenOptions.Insert(0, s);

        SyncDropdownsFromProject();

        EditSanierenOptionsCommand = new RelayCommand(EditSanierenOptions);
        PreviewSanierenOptionsCommand = new RelayCommand(PreviewSanierenOptions);
        ResetSanierenOptionsCommand = new RelayCommand(ResetSanierenOptions);
        AddSanierenOptionCommand = new RelayCommand<object?>(AddSanierenOption);
        RemoveSanierenOptionCommand = new RelayCommand<object?>(RemoveSanierenOption);
        EditEigentuemerOptionsCommand = new RelayCommand(EditEigentuemerOptions);
        PreviewEigentuemerOptionsCommand = new RelayCommand(PreviewEigentuemerOptions);
        ResetEigentuemerOptionsCommand = new RelayCommand(ResetEigentuemerOptions);
        AddEigentuemerOptionCommand = new RelayCommand<object?>(AddEigentuemerOption);
        RemoveEigentuemerOptionCommand = new RelayCommand<object?>(RemoveEigentuemerOption);

        SaveAsCommand = _shell.SaveAsProjectCommand;
        AnlegenCommand = new RelayCommand(
            () => _shell.CreateProjectFromDraft(),
            () => !string.IsNullOrWhiteSpace(DraftName));
        AbbrechenCommand = new RelayCommand(_shell.EnterLauncher);

        DraftName = Project.Name ?? string.Empty;

        _shell.PropertyChanged += ShellPropertyChanged;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _shell.PropertyChanged -= ShellPropertyChanged;
    }

    private void ShellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.Project))
        {
            OnPropertyChanged(nameof(Project));
            DraftName = Project.Name ?? string.Empty;
            SyncDropdownsFromProject();
        }
        else if (e.PropertyName == nameof(ShellViewModel.CurrentMode))
        {
            OnPropertyChanged(nameof(IsDraft));
            OnPropertyChanged(nameof(IsNotDraft));
        }
    }

    /// <summary>Schreibt den eingetippten Namen ins Projekt und aktualisiert den Anlegen-Button.</summary>
    partial void OnDraftNameChanged(string value)
    {
        Project.Name = value;
        (AnlegenCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void SyncDropdownsFromProject()
    {
        if (Project.Metadata.TryGetValue("Sanieren", out var sv) && !SanierenOptions.Contains(sv))
            SanierenOptions.Insert(0, sv);

        SanierenValue = Project.Metadata.TryGetValue("Sanieren", out var sanieren)
            ? sanieren
            : SanierenOptions.FirstOrDefault() ?? "Nein";
        EigentuemerValue = Project.Metadata.TryGetValue("Eigentuemer", out var eigentuemer)
            ? eigentuemer
            : EigentuemerOptions.FirstOrDefault() ?? "Privat";
    }

    partial void OnSanierenValueChanged(string value)
    {
        Project.Metadata["Sanieren"] = value;
        if (DropdownOptionList.AddIfMissing(SanierenOptions, value))
            SaveDropdownOptions();
    }

    partial void OnEigentuemerValueChanged(string value)
    {
        Project.Metadata["Eigentuemer"] = value;
    }

    private void EditSanierenOptions()
    {
        var vm = new OptionsEditorViewModel(SanierenOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            DropdownOptionList.ReplaceWith(SanierenOptions, vm.Items);
            DropdownOptionList.AddIfMissing(SanierenOptions, SanierenValue);
            SaveDropdownOptions();
        }
    }

    private void PreviewSanierenOptions()
    {
        var items = string.Join("\n", SanierenOptions);
        _dialogs.Info(items, "Sanieren-Liste");
    }

    private void ResetSanierenOptions()
    {
        DropdownOptionList.ReplaceWith(SanierenOptions, new[] { "Nein", "Ja" });
        SaveDropdownOptions();
    }

    private void AddSanierenOption(object? value)
        => AddOptionIfMissing(SanierenOptions, ExtractText(value));

    private void RemoveSanierenOption(object? value)
        => RemoveOptionFromList(SanierenOptions, ExtractText(value));

    private void EditEigentuemerOptions()
    {
        var vm = new OptionsEditorViewModel(EigentuemerOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            DropdownOptionList.ReplaceWith(EigentuemerOptions, vm.Items);
            EnforceEigentuemerOptionsExact();
            SaveDropdownOptions();
        }
    }

    private void PreviewEigentuemerOptions()
    {
        var items = string.Join("\n", EigentuemerOptions);
        _dialogs.Info(items, "Eigentuemer-Liste");
    }

    private void ResetEigentuemerOptions()
    {
        EnforceEigentuemerOptionsExact();
        SaveDropdownOptions();
    }

    private void AddEigentuemerOption(object? value)
    {
        _ = value;
        EnforceEigentuemerOptionsExact();
        SaveDropdownOptions();
    }

    private void RemoveEigentuemerOption(object? value)
    {
        _ = value;
        EnforceEigentuemerOptionsExact();
        SaveDropdownOptions();
    }

    private static string ExtractText(object? value)
        => DropdownOptionList.ExtractText(value);

    private void AddOptionIfMissing(ObservableCollection<string> options, string value)
    {
        if (DropdownOptionList.AddIfMissing(options, value))
            SaveDropdownOptions();
    }

    private void RemoveOptionFromList(ObservableCollection<string> options, string? value)
    {
        if (DropdownOptionList.Remove(options, value))
            SaveDropdownOptions();
    }
    private void SaveDropdownOptions()
    {
        EnforceEigentuemerOptionsExact();
        _dropdownOptions.SaveSanierenOptions(SanierenOptions);
        _dropdownOptions.SaveEigentuemerOptions(EigentuemerOptions);
    }

    private void EnforceEigentuemerOptionsExact()
    {
        DropdownOptionList.EnsureExact(EigentuemerOptions, _dropdownOptions.FixedEigentuemerOptions);
    }

}

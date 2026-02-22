using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using System.Collections.ObjectModel;
using System.Linq;
using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class ProjectPageViewModel : ObservableObject
{
    private static readonly string[] FixedEigentuemerOptions = { "Kanton", "Bund", "AWU", "Gemeinde", "Privat" };
    private readonly ShellViewModel _shell;

    public Project Project => _shell.Project;
    public IRelayCommand SaveCommand => _shell.SaveCommand;

    public IRelayCommand NewCommand { get; }
    public IRelayCommand OpenCommand { get; }
    public IRelayCommand SaveAsCommand { get; }

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

    public ProjectPageViewModel(ShellViewModel shell)
    {
        _shell = shell;

        // Dropdown-Optionen laden
        SanierenOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadSanierenOptions());
        EigentuemerOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadEigentuemerOptions());
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

        NewCommand = _shell.NewProjectCommand;
        OpenCommand = _shell.OpenProjectCommand;
        SaveAsCommand = _shell.SaveAsProjectCommand;

        _shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.Project))
            {
                OnPropertyChanged(nameof(Project));
                SyncDropdownsFromProject();
            }
        };
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
        if (!SanierenOptions.Contains(value))
        {
            SanierenOptions.Insert(0, value);
            SaveDropdownOptions();
        }
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
            SanierenOptions.Clear();
            foreach (var item in vm.Items)
                SanierenOptions.Add(item);
            if (!SanierenOptions.Contains(SanierenValue))
                SanierenOptions.Insert(0, SanierenValue);
            SaveDropdownOptions();
        }
    }

    private void PreviewSanierenOptions()
    {
        var items = string.Join("\n", SanierenOptions);
        System.Windows.MessageBox.Show(items, "Sanieren-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ResetSanierenOptions()
    {
        SanierenOptions.Clear();
        foreach (var item in new[] { "Nein", "Ja" })
            SanierenOptions.Add(item);
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
            EigentuemerOptions.Clear();
            foreach (var item in vm.Items)
                EigentuemerOptions.Add(item);
            EnforceEigentuemerOptionsExact();
            SaveDropdownOptions();
        }
    }

    private void PreviewEigentuemerOptions()
    {
        var items = string.Join("\n", EigentuemerOptions);
        System.Windows.MessageBox.Show(items, "Eigentuemer-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
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
    {
        if (value is null)
            return string.Empty;
        if (value is string text)
            return text;
        if (value is System.Windows.Controls.ComboBox combo)
            return combo.Text ?? string.Empty;
        return value.ToString() ?? string.Empty;
    }

    private void AddOptionIfMissing(ObservableCollection<string> options, string value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return;
        if (options.Any(x => x.Equals(text, StringComparison.OrdinalIgnoreCase)))
            return;
        options.Insert(0, text);
        SaveDropdownOptions();
    }

    private void RemoveOptionFromList(ObservableCollection<string> options, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return;
        var existing = options.FirstOrDefault(x => x.Equals(text, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return;
        options.Remove(existing);
        SaveDropdownOptions();
    }
    private void SaveDropdownOptions()
    {
        EnforceEigentuemerOptionsExact();
        DropdownOptionsStore.SaveSanierenOptions(SanierenOptions);
        DropdownOptionsStore.SaveEigentuemerOptions(EigentuemerOptions);
    }

    private void EnforceEigentuemerOptionsExact()
    {
        var same = EigentuemerOptions.Count == FixedEigentuemerOptions.Length;
        if (same)
        {
            for (var i = 0; i < FixedEigentuemerOptions.Length; i++)
            {
                if (!string.Equals(EigentuemerOptions[i], FixedEigentuemerOptions[i], StringComparison.Ordinal))
                {
                    same = false;
                    break;
                }
            }
        }

        if (same)
            return;

        EigentuemerOptions.Clear();
        foreach (var item in FixedEigentuemerOptions)
            EigentuemerOptions.Add(item);
    }

}

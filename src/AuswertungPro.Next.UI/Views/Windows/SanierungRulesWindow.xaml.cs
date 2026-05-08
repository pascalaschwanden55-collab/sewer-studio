using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.Domain.Sanierung;
using AuswertungPro.Next.Infrastructure.Sanierung;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class SanierungRulesWindow : Window
{
    private readonly SanierungUserRulesService _service;
    private readonly RehabilitationRulesEngine _engine;
    private ObservableCollection<RuleRowVm> _rows = new();
    private readonly IDialogService _dialogs = App.Resolve<IDialogService>();

    public SanierungRulesWindow(SanierungUserRulesService service, RehabilitationRulesEngine engine)
    {
        InitializeComponent();
        _service = service;
        _engine = engine;
        Reload();
    }

    private void Reload()
    {
        var file = _service.Load();
        _rows = new ObservableCollection<RuleRowVm>(
            file.Rules.Select(r => new RuleRowVm(r)));
        RulesGrid.ItemsSource = _rows;
    }

    private RuleRowVm? Selected => RulesGrid.SelectedItem as RuleRowVm;

    private void RulesGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var has = Selected is not null;
        EditButton.IsEnabled = has;
        ToggleButton.IsEnabled = has;
        DeleteButton.IsEnabled = has;
    }

    private void RulesGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Selected is not null) Edit_Click(this, new RoutedEventArgs());
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SanierungRuleEditDialog(new SanierungUserRule
        {
            Id = "rule_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Name = "Neue Regel",
            Enabled = true,
            CreatedBy = Environment.UserName,
            CreatedAt = DateTime.Now,
        }) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result is { } rule)
        {
            _rows.Add(new RuleRowVm(rule));
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is null) return;
        var dlg = new SanierungRuleEditDialog(Selected.Rule) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result is { } updated)
        {
            Selected.UpdateFrom(updated);
            RulesGrid.Items.Refresh();
        }
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is null) return;
        Selected.Rule.Enabled = !Selected.Rule.Enabled;
        Selected.RaiseChanged();
        RulesGrid.Items.Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is null) return;
        var confirm = _dialogs.ShowMessage(
            $"Regel '{Selected.Name}' wirklich loeschen?\n\n(Alternative: deaktivieren statt loeschen)",
            "Regel loeschen", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.OK)
            _rows.Remove(Selected);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var file = new SanierungUserRulesFile
            {
                Rules = _rows.Select(r => r.Rule).ToList(),
            };
            _service.Save(file);
            // Engine ueber Cache-Invalidate informieren - naechste Anfrage liest neu
            _service.Invalidate();
            _dialogs.ShowMessage(
                $"Gespeichert: {_rows.Count(r => r.Enabled)} aktive Regeln (von {_rows.Count} insgesamt).\n\n" +
                "Die KI verwendet ab der naechsten Anfrage die neuen Regeln.",
                "Sanierungsregeln", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage($"Speichern fehlgeschlagen:\n{ex.Message}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

/// <summary>ViewModel-Wrapper fuer DataGrid-Anzeige.</summary>
public sealed class RuleRowVm
{
    public SanierungUserRule Rule { get; private set; }
    public RuleRowVm(SanierungUserRule rule) => Rule = rule;

    public bool Enabled
    {
        get => Rule.Enabled;
        set { Rule.Enabled = value; }
    }
    public string Name => Rule.Name;
    public string Reason => Rule.Reason;
    public string ConditionsSummary => BuildConditionsSummary(Rule.Conditions);
    public string ExcludesSummary => string.Join(", ", Rule.ExcludeProcedureIds);

    public void UpdateFrom(SanierungUserRule updated) => Rule = updated;
    public void RaiseChanged() { /* placeholder for INotifyPropertyChanged if needed */ }

    private static string BuildConditionsSummary(RuleConditions c)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.MaterialContains)) parts.Add($"Material enth. '{c.MaterialContains}'");
        if (!string.IsNullOrWhiteSpace(c.MaterialNotContains)) parts.Add($"Material ohne '{c.MaterialNotContains}'");
        if (c.DnMin.HasValue) parts.Add($"DN >= {c.DnMin.Value}");
        if (c.DnMax.HasValue) parts.Add($"DN <= {c.DnMax.Value}");
        if (c.BendDegreesMin.HasValue) parts.Add($"Bogen >= {c.BendDegreesMin.Value}°");
        if (c.DamageGroupAnyOf is { Count: > 0 }) parts.Add($"Schaden: {string.Join("/", c.DamageGroupAnyOf)}");
        if (!string.IsNullOrWhiteSpace(c.VsaCodeStartsWith)) parts.Add($"VSA-Code beginnt mit '{c.VsaCodeStartsWith}'");
        if (c.ZustandsklasseMax.HasValue) parts.Add($"ZK <= {c.ZustandsklasseMax.Value}");
        if (c.Groundwater.HasValue) parts.Add(c.Groundwater.Value ? "Grundwasser ja" : "Grundwasser nein");
        if (!string.IsNullOrWhiteSpace(c.NutzungsartContains)) parts.Add($"Nutzung enth. '{c.NutzungsartContains}'");
        return parts.Count == 0 ? "(immer wahr)" : string.Join(" UND ", parts);
    }
}

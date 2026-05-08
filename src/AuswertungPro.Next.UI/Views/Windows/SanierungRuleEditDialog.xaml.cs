using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Sanierung;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class SanierungRuleEditDialog : Window
{
    public SanierungUserRule? Result { get; private set; }
    private readonly SanierungUserRule _original;
    private readonly IDialogService _dialogs = App.Resolve<IDialogService>();

    public SanierungRuleEditDialog(SanierungUserRule rule)
    {
        InitializeComponent();
        _original = rule;
        LoadFromRule(rule);
    }

    private void LoadFromRule(SanierungUserRule r)
    {
        NameBox.Text = r.Name;
        EnabledBox.IsChecked = r.Enabled;
        var c = r.Conditions;
        MaterialContainsBox.Text = c.MaterialContains ?? "";
        MaterialNotContainsBox.Text = c.MaterialNotContains ?? "";
        DnMinBox.Text = c.DnMin?.ToString(CultureInfo.InvariantCulture) ?? "";
        DnMaxBox.Text = c.DnMax?.ToString(CultureInfo.InvariantCulture) ?? "";
        BendDegBox.Text = c.BendDegreesMin?.ToString(CultureInfo.InvariantCulture) ?? "";
        DamageGroupsBox.Text = c.DamageGroupAnyOf is { Count: > 0 }
            ? string.Join(", ", c.DamageGroupAnyOf) : "";
        VsaCodeBox.Text = c.VsaCodeStartsWith ?? "";
        ZkMaxBox.Text = c.ZustandsklasseMax?.ToString(CultureInfo.InvariantCulture) ?? "";
        GroundwaterBox.SelectedIndex = c.Groundwater switch { true => 1, false => 2, _ => 0 };
        NutzungsartBox.Text = c.NutzungsartContains ?? "";
        ExcludeIdsBox.Text = string.Join(", ", r.ExcludeProcedureIds);
        ReasonBox.Text = r.Reason;
        NoteBox.Text = r.Note ?? "";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            _dialogs.ShowMessage("Bitte einen Regel-Namen angeben.", "Fehlende Eingabe",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(ExcludeIdsBox.Text))
        {
            _dialogs.ShowMessage("Bitte mindestens eine Verfahrens-ID zum Ausschliessen angeben.",
                "Fehlende Eingabe", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(ReasonBox.Text))
        {
            _dialogs.ShowMessage("Bitte eine Begruendung angeben (wird der KI im Prompt mitgegeben).",
                "Fehlende Eingabe", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new SanierungUserRule
        {
            Id = _original.Id,
            Name = NameBox.Text.Trim(),
            Enabled = EnabledBox.IsChecked == true,
            CreatedBy = _original.CreatedBy ?? Environment.UserName,
            CreatedAt = _original.CreatedAt,
            Conditions = new RuleConditions
            {
                MaterialContains = NullIfEmpty(MaterialContainsBox.Text),
                MaterialNotContains = NullIfEmpty(MaterialNotContainsBox.Text),
                DnMin = ParseInt(DnMinBox.Text),
                DnMax = ParseInt(DnMaxBox.Text),
                BendDegreesMin = ParseDouble(BendDegBox.Text),
                DamageGroupAnyOf = ParseList(DamageGroupsBox.Text),
                VsaCodeStartsWith = NullIfEmpty(VsaCodeBox.Text),
                ZustandsklasseMax = ParseInt(ZkMaxBox.Text),
                Groundwater = GroundwaterBox.SelectedIndex switch { 1 => true, 2 => false, _ => null },
                NutzungsartContains = NullIfEmpty(NutzungsartBox.Text),
            },
            ExcludeProcedureIds = ParseList(ExcludeIdsBox.Text) ?? new(),
            Reason = ReasonBox.Text.Trim(),
            Note = NullIfEmpty(NoteBox.Text),
        };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static int? ParseInt(string? s) =>
        int.TryParse(s?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double? ParseDouble(string? s) =>
        double.TryParse(s?.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static System.Collections.Generic.List<string>? ParseList(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return s.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }
}

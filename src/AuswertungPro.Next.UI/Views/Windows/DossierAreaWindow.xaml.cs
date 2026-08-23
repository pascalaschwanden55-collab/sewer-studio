using System;
using System.Windows;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Die gebietsweiten Angaben. Sie werden einmal je Projekt erfasst und gelten
/// fuer alle Dossiers; einzelne Liegenschaften duerfen sie ueberschreiben.
/// </summary>
public partial class DossierAreaWindow : Window
{
    private readonly DossierAreaSettings _target;

    private DossierAreaWindow(DossierAreaSettings target)
    {
        InitializeComponent();
        _target = target;
        Load();
    }

    public static bool ShowFor(DossierAreaSettings area)
    {
        ArgumentNullException.ThrowIfNull(area);

        var window = new DossierAreaWindow(area)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return window.ShowDialog() == true;
    }

    private void Load()
    {
        TitleBox.Text = _target.AreaTitle;
        AuthorsBox.Text = _target.Authors;
        ExecutionBox.Text = _target.ExecutionDate;
        ContactBox.Text = _target.ContactPerson;
        ContractorBox.Text = _target.Contractor;
        SiteBox.Text = _target.SiteManagement;
        ObstructionsBox.Text = _target.Obstructions;
        HouseConnectionBox.Text = _target.HouseConnectionText;
        StormWaterBox.Text = _target.StormWaterText;
        DeadlineBox.Text = _target.ResponseDeadline;
        FooterBox.Text = _target.FooterLine;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _target.AreaTitle = Trim(TitleBox.Text);
        _target.Authors = Trim(AuthorsBox.Text);
        _target.ExecutionDate = Trim(ExecutionBox.Text);
        _target.ContactPerson = Trim(ContactBox.Text);
        _target.Contractor = Trim(ContractorBox.Text);
        _target.SiteManagement = Trim(SiteBox.Text);
        _target.Obstructions = Trim(ObstructionsBox.Text);
        _target.HouseConnectionText = Trim(HouseConnectionBox.Text);
        _target.StormWaterText = Trim(StormWaterBox.Text);
        _target.ResponseDeadline = Trim(DeadlineBox.Text);
        _target.FooterLine = Trim(FooterBox.Text);

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private static string Trim(string? value) => value?.Trim() ?? "";
}

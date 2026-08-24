using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Die gebietsweiten Angaben. Sie werden einmal je Projekt erfasst und gelten
/// fuer alle Dossiers; einzelne Liegenschaften duerfen sie ueberschreiben.
///
/// Die Themen der Tabelle "Informationen" sind eine Liste, keine feste
/// Feldreihe: das eine Gebiet braucht "Hausanschluss Abwasser", das naechste
/// "Ausgangslage" und "Sanierungskonzept".
/// </summary>
public partial class DossierAreaWindow : Window
{
    private readonly DossierAreaSettings _target;
    private readonly ObservableCollection<DossierTopicItem> _topics = new();

    private DossierAreaWindow(DossierAreaSettings target)
    {
        InitializeComponent();
        _target = target;
        TopicList.ItemsSource = _topics;
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
        LocationBox.Text = _target.AreaLocation;
        ProjectNumberBox.Text = _target.ProjectNumber;
        DrawnByBox.Text = _target.DrawnBy;
        AuthorsBox.Text = _target.Authors;
        DeadlineBox.Text = _target.ResponseDeadline;
        FooterBox.Text = _target.FooterLine;

        foreach (var thema in _target.Topics ?? new())
        {
            if (thema is null)
                continue;

            _topics.Add(new DossierTopicItem { Title = thema.Title, Text = thema.Text });
        }

        // Ein Gebiet ohne Themen bekommt die Standardliste — sonst staende der
        // Benutzer vor einer leeren Seite und muesste elf Zeilen abtippen.
        if (_topics.Count == 0)
        {
            foreach (var titel in DossierDocumentMigration.DefaultTopicTitles)
            {
                DossierDocumentMigration.DefaultTopicTexts.TryGetValue(titel, out var text);
                _topics.Add(new DossierTopicItem { Title = titel, Text = text ?? "" });
            }
        }

        if (_topics.Count > 0)
            TopicList.SelectedIndex = 0;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _target.AreaTitle = Trim(TitleBox.Text);
        _target.AreaLocation = Trim(LocationBox.Text);
        _target.ProjectNumber = Trim(ProjectNumberBox.Text);
        _target.DrawnBy = Trim(DrawnByBox.Text);
        _target.Authors = Trim(AuthorsBox.Text);
        _target.ResponseDeadline = Trim(DeadlineBox.Text);
        _target.FooterLine = Trim(FooterBox.Text);

        // Eine Zeile ohne Thema hat keine Bedeutung; ihr Text bleibt im Dossier
        // nicht auffindbar und stuende als leere Tabellenzeile im Dokument.
        _target.Topics = _topics
            .Where(t => !string.IsNullOrWhiteSpace(t.Title))
            .Select(t => new DossierTopicRow { Title = t.Title.Trim(), Text = t.Text ?? "" })
            .ToList();

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnAddTopic(object sender, RoutedEventArgs e)
    {
        var neu = new DossierTopicItem { Title = "Neues Thema" };
        var stelle = TopicList.SelectedIndex < 0 ? _topics.Count : TopicList.SelectedIndex + 1;

        _topics.Insert(stelle, neu);
        TopicList.SelectedItem = neu;
        TopicTitleBox.Focus();
        TopicTitleBox.SelectAll();
    }

    private void OnRemoveTopic(object sender, RoutedEventArgs e)
    {
        if (TopicList.SelectedItem is not DossierTopicItem gewaehlt)
            return;

        var stelle = _topics.IndexOf(gewaehlt);
        _topics.Remove(gewaehlt);

        if (_topics.Count > 0)
            TopicList.SelectedIndex = Math.Min(stelle, _topics.Count - 1);
    }

    private void OnTopicUp(object sender, RoutedEventArgs e) => Verschiebe(-1);

    private void OnTopicDown(object sender, RoutedEventArgs e) => Verschiebe(+1);

    private void Verschiebe(int richtung)
    {
        var stelle = TopicList.SelectedIndex;
        var ziel = stelle + richtung;

        if (stelle < 0 || ziel < 0 || ziel >= _topics.Count)
            return;

        _topics.Move(stelle, ziel);
        TopicList.SelectedIndex = ziel;
    }

    private static string Trim(string? value) => value?.Trim() ?? "";
}

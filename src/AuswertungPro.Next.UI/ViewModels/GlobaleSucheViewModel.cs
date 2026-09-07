using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.UseCases.Suche;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Suchfeld der Kopfzeile (Strg+K). Sucht live; ein gewaehlter Treffer springt zur Seite.</summary>
public sealed partial class GlobaleSucheViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private GlobaleSucheTreffer? _gewaehlt;
    [ObservableProperty] private bool _listeOffen;
    public ObservableCollection<GlobaleSucheTreffer> Treffer { get; } = new();
    public string LeerText => Text.Trim().Length == 0 ? "Name, Nummer oder Strasse eingeben." : Treffer.Count == 0 ? "Kein Treffer" : string.Empty;

    public GlobaleSucheViewModel(ShellViewModel shell) => _shell = shell;

    partial void OnTextChanged(string value)
    {
        Treffer.Clear();
        if (_shell.IsProjectReady)
            foreach (var t in GlobaleSucheRegel.Suche(value, _shell.Project.Data, _shell.Project.SchaechteData, SchaechteColumnPolicy.GetSchachtNumber))
                Treffer.Add(t);
        ListeOffen = value.Trim().Length > 0;
        OnPropertyChanged(nameof(LeerText));
    }

    partial void OnGewaehltChanged(GlobaleSucheTreffer? value)
    {
        if (value is null) return;
        switch (value.Art)
        {
            case GlobaleSucheArt.Haltung: _shell.NavigateToHolding(value.Ziel as HaltungRecord); break;
            case GlobaleSucheArt.Schacht: _shell.NavigateToShaft(value.Ziel as SchachtRecord); break;
            case GlobaleSucheArt.Strasse: _shell.NavigateToDataPage(new DataPageStartFilter(FieldKeys.Street, (string)value.Ziel!)); break;
        }
        ListeOffen = false;
        Text = string.Empty;
    }

    /// <summary>Enter im Feld: markierten oder ersten Treffer waehlen (Inventar 3.5).</summary>
    public void WaehleErstenOderMarkierten()
    {
        if (Gewaehlt is null && Treffer.Count > 0)
            Gewaehlt = Treffer[0];
    }
}

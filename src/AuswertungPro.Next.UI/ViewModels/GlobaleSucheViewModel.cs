using System;
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
    [ObservableProperty] private bool _listeOffen;
    /// <summary>Ueber Pfeiltasten markierter Index (Inventar 3.5); -1 = keine Markierung.</summary>
    [ObservableProperty] private int _markiertIndex = -1;
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
        MarkiertIndex = -1;
        OnPropertyChanged(nameof(LeerText));
    }

    /// <summary>Treffer waehlen: navigiert zur passenden Seite, schliesst die Liste und leert das Feld.</summary>
    public void Waehle(GlobaleSucheTreffer? treffer)
    {
        if (treffer is null) return;
        switch (treffer.Art)
        {
            case GlobaleSucheArt.Haltung: _shell.NavigateToHolding(treffer.Ziel as HaltungRecord); break;
            case GlobaleSucheArt.Schacht: _shell.NavigateToShaft(treffer.Ziel as SchachtRecord); break;
            case GlobaleSucheArt.Strasse: _shell.NavigateToDataPage(new DataPageStartFilter(FieldKeys.Street, (string)treffer.Ziel!)); break;
        }
        ListeOffen = false;
        Text = string.Empty;
    }

    /// <summary>Pfeiltaste unten: naechsten Treffer markieren (Inventar 3.5).</summary>
    public void MarkiereNaechsten()
    {
        if (Treffer.Count == 0) return;
        MarkiertIndex = Math.Min(MarkiertIndex + 1, Treffer.Count - 1);
    }

    /// <summary>Pfeiltaste oben: vorherigen Treffer markieren (Inventar 3.5).</summary>
    public void MarkiereVorherigen()
    {
        if (Treffer.Count == 0) return;
        MarkiertIndex = Math.Max(MarkiertIndex - 1, 0);
    }

    /// <summary>Enter im Feld: markierten oder ersten Treffer waehlen (Inventar 3.5).</summary>
    public void WaehleErstenOderMarkierten()
    {
        if (Treffer.Count == 0) return;
        var index = MarkiertIndex >= 0 && MarkiertIndex < Treffer.Count ? MarkiertIndex : 0;
        Waehle(Treffer[index]);
    }
}

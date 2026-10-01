using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>
/// Eine Themenzeile waehrend der Bearbeitung. Sie meldet Aenderungen, damit die
/// Liste einen umbenannten Titel sofort zeigt — das Domaenenmodell bleibt dafuer
/// frei von Oberflaechenbelangen.
/// </summary>
public sealed class DossierTopicItem : INotifyPropertyChanged
{
    private string _title = "";
    private string _text = "";

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public string Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    public string ColorHex { get; set; } = "";

    public List<DossierTextStyleRange> StyleRanges { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set(ref string feld, string wert, [CallerMemberName] string? name = null)
    {
        var neu = wert ?? "";
        if (feld == neu)
            return;

        feld = neu;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

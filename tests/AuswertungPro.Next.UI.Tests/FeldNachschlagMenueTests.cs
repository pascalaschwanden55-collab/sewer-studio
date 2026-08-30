using System;
using System.IO;
using System.Windows.Input;
using AuswertungPro.Next.UI.Views.Windows;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der Menuepunkt "Beim Kanton nachschlagen" darf nur an einem leeren Feld
/// erscheinen, fuer das es ueberhaupt eine Quelle gibt. An einem gefuellten
/// Feld waere er eine Einladung zum versehentlichen Ueberschreiben, an einem
/// Kostenfeld eine leere Zusage.
/// </summary>
public sealed class FeldNachschlagMenueTests
{
    private sealed class NichtsTuerBefehl : ICommand
    {
        // Vom Test nie ausgeloest - WPF verlangt das Ereignis trotzdem.
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }

    private static RecordDetailItem Feld(string feldname, string wert, bool mitBefehl = true)
        => new(
            label: feldname,
            value: wert,
            commitValue: _ => { },
            nachschlagenCommand: mitBefehl ? new NichtsTuerBefehl() : null)
        {
            FieldName = feldname
        };

    [Fact]
    public void Ein_leeres_Feld_mit_Quelle_kann_nachschlagen()
    {
        Assert.True(Feld("Funktion", "").KannNachschlagen);
        Assert.True(Feld("Eigentuemer", "").KannNachschlagen);
        Assert.True(Feld("Strasse", "   ").KannNachschlagen);
    }

    [Fact]
    public void Ein_gefuelltes_Feld_kann_nicht_nachschlagen()
    {
        Assert.False(Feld("Funktion", "Schlammsammler").KannNachschlagen);
        Assert.False(Feld("Eigentuemer", "Muster, Hans").KannNachschlagen);
    }

    [Fact]
    public void Ein_Feld_ohne_Quelle_kann_nicht_nachschlagen()
    {
        Assert.False(Feld("Kosten", "").KannNachschlagen);
        Assert.False(Feld("Massnahmen", "").KannNachschlagen);
        Assert.False(Feld("Bemerkungen", "").KannNachschlagen);
    }

    [Fact]
    public void Ohne_Befehl_kann_nichts_nachgeschlagen_werden()
    {
        Assert.False(Feld("Funktion", "", mitBefehl: false).KannNachschlagen);
    }

    [Fact]
    public void Das_Kontextmenue_bietet_den_Nachschlag_an()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Controls", "RecordDetailsView.xaml"));

        Assert.Contains("Beim Kanton nachschlagen", xaml, StringComparison.Ordinal);
        Assert.Contains("NachschlagenCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("KannNachschlagen", xaml, StringComparison.Ordinal);
    }
}

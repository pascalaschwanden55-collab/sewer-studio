using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.4, Prototyp v2): Das Schachtprotokoll als Knopf statt als roher
/// Dateipfad — dieselbe Loesung wie die Protokollspalte der Haltungsliste
/// (<see cref="HaltungStatusColumnFactory"/>).
///
/// Es ist eine virtuelle Spalte (<see cref="NovaStatusSpalten.Protokoll"/>): kein Feld, kein
/// Export, keine Layout-Persistenz und ausdruecklich nur lesend. Bearbeitet wird der Pfad
/// weiterhin in "Alle Spalten" und in den Eingabefeldern.
///
/// Der Knopf ruft den bereits vorhandenen Weg der Seite auf
/// (<see cref="SchaechtePage.ProtokollOeffnenCommand"/>, der intern dieselbe Route wie das
/// Kontextmenue nimmt) — es entsteht kein zweiter Dateizugriff.
/// </summary>
public static class SchaechteProtokollColumnFactory
{
    public static DataGridTemplateColumn Create(string header)
        => new()
        {
            Header = header,
            CellTemplate = Vorlage(Zelle()),
            IsReadOnly = true,
            CanUserSort = false,
            Width = DataGridLength.SizeToHeader,
            MinWidth = 78
        };

    /// <summary>
    /// Eine fertige, versiegelte Zellvorlage. Versiegelt wird gleich beim Bauen: WPF versiegelt
    /// eine Vorlage sonst erst beim ersten Anwenden, und vorher laesst sich ihr Baum nicht
    /// erzeugen (das brauchen die Tests).
    /// </summary>
    private static DataTemplate Vorlage(FrameworkElementFactory inhalt)
    {
        var vorlage = new DataTemplate { VisualTree = inhalt };
        vorlage.Seal();
        return vorlage;
    }

    private static FrameworkElementFactory Zelle()
    {
        var huelle = new FrameworkElementFactory(typeof(Grid));
        huelle.AppendChild(Knopf());
        huelle.AppendChild(StatusZellenBausteine.Fehlt(
            SchachtProtokollSichtbarkeitConverter.Bindung(SchachtProtokollSichtbarkeitConverter.KeinProtokoll),
            "kein Protokoll"));
        return huelle;
    }

    private static FrameworkElementFactory Knopf()
        => StatusZellenBausteine.Aktionsknopf(
            befehl: new Binding(nameof(SchaechtePage.ProtokollOeffnenCommand))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(SchaechtePage), 1)
            },
            name: SchachtProtokollSichtbarkeitConverter.Bindung(SchachtProtokollNameConverter.Instance),
            hinweis: "Protokoll öffnen",
            sichtbarkeit: SchachtProtokollSichtbarkeitConverter.Bindung(SchachtProtokollSichtbarkeitConverter.Protokoll),
            inhalt: StatusZellenBausteine.Kuerzel("PDF"),
            breite: 36d);
}

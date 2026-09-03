using System.Collections.Generic;
using System.Windows;
using AuswertungPro.Next.Application.UseCases.Xtf;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Vorschaufenster vor dem XTF-Schreiben. Das Fenster orchestriert nur: Es zeigt die
/// <see cref="XtfExportVorschau"/> und meldet Ja/Nein zurueck; entschieden wird im UseCase.
/// </summary>
public partial class XtfExportVorschauWindow : Window
{
    public XtfExportVorschauWindow(XtfExportVorschau vorschau)
    {
        InitializeComponent();
        DataContext = new Anzeige(vorschau);
    }

    private void OnSchreiben(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    /// <summary>Reine Anzeigewerte fuer die Bindung; nichts davon wirkt auf den Ablauf zurueck.</summary>
    public sealed partial class Anzeige : ObservableObject
    {
        [ObservableProperty] private bool _detailsOffen;

        public Anzeige(XtfExportVorschau vorschau)
        {
            Titel = vorschau.Titel;
            Zusammenfassung = vorschau.Zusammenfassung;
            Zeilen = vorschau.Zeilen;
            KurzeWarnungen = vorschau.KurzeWarnungen;
            Details = vorschau.Details;
            IstFehler = vorschau.IstFehler;
            HatWarnungen = vorschau.HatWarnungen;
            HatZeilen = vorschau.HatZeilen && !vorschau.IstFehler;
            // Im Fehlerfall ist der Bericht das Wichtigste — gleich aufgeklappt.
            _detailsOffen = vorschau.IstFehler;
        }

        public string Titel { get; }
        public string Zusammenfassung { get; }
        public IReadOnlyList<XtfVorschauZeile> Zeilen { get; }
        public IReadOnlyList<string> KurzeWarnungen { get; }
        public string Details { get; }
        public bool IstFehler { get; }
        public bool IstVorschau => !IstFehler;
        public bool HatWarnungen { get; }
        public bool HatZeilen { get; }
        public bool OhneZeilen => !HatZeilen;

        public string OhneTabelleHinweis => IstFehler
            ? "Es wurde nichts geschrieben. Die Einzelheiten stehen unter Details."
            : "Diese Datei erhält neue Kennungen; es gibt keine Original-Werte zum Vergleichen. Was hineinkommt, steht unter Details.";
    }
}

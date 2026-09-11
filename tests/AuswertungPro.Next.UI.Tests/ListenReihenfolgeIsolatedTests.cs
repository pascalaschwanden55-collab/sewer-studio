using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Controls;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

namespace AuswertungPro.Next.UI.Tests;

[Collection("IsolatedWpf")]
public sealed class ListenReihenfolgeIsolatedTests
{
    [Fact]
    public async Task Beide_Listen_erlauben_direktes_Verschieben_und_sperren_unklare_Ziele()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(ListenReihenfolgeIsolatedTests).FullName + "." + nameof(Kindprozess), TimeSpan.FromSeconds(90));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            var haltungen = new ObservableCollection<HaltungRecord>(Enumerable.Range(1, 30).Select(i =>
            {
                var record = new HaltungRecord();
                record.SetFieldValue("Haltungsname", $"{80000 + i}-{80001 + i}", FieldSource.Manual, true);
                record.SetFieldValue("Strasse", "Gotthardstrasse", FieldSource.Manual, true);
                return record;
            }));
            HaltungRunningNumberService.AssignNr(haltungen);
            var haltung = new HaltungAufklappListe { ItemsSource = haltungen };
            Pruefe(haltung, haltung.Reihenfolge, haltungen,
                (record, position) => DataPageRecordOrderController.TryMoveToPosition(haltungen, record, position),
                record => record.GetFieldValue("NR"), "Haltungen");

            var schaechte = new ObservableCollection<SchachtRecord>(Enumerable.Range(1, 30).Select(i =>
            {
                var record = new SchachtRecord();
                record.SetFieldValue("Schachtnummer", $"{80000 + i}");
                record.SetFieldValue("Strasse", "Gotthardstrasse");
                return record;
            }));
            var nummerierung = new SchaechteRecordCollectionController(() => schaechte, () => ["NR."], new object());
            nummerierung.Renumber();
            var schacht = new SchachtAufklappListe { ItemsSource = schaechte };
            Pruefe(schacht, schacht.Reihenfolge, schaechte,
                (record, position) =>
                {
                    if (!nummerierung.TryMoveToPosition(record, position)) return false;
                    nummerierung.Renumber();
                    return true;
                }, record => record.GetFieldValue("NR."), "Schaechte");
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static void Pruefe<T>(UserControl control, ListenReihenfolgeController controller,
        ObservableCollection<T> records, Func<T, int, bool> move, Func<T, string> nummer, string bildname) where T : class
    {
        var host = new AdornerDecorator { Child = control };
        var liste = (ListBox)control.FindName("Liste");
        var leiste = (ListenReihenfolgeLeiste)control.FindName("ReihenfolgeLeiste");
        var erlaubt = true;
        var aufrufe = 0;
        controller.Datensaetze = () => records;
        controller.DarfVerschieben = () => erlaubt;
        controller.Verschiebe = (record, position) => { aufrufe++; return move((T)record, position); };
        Layout(host);
        var original = records.ToArray();
        var quelle = original[0];

        // Ziehen ist bis zum Ablegen schreibfrei; Abbruch veraendert nichts.
        Assert.True(controller.BereiteZiehenVor(quelle));
        Assert.Equal(original, records);
        controller.BeendeZiehen();
        Assert.False(controller.LegeAb(original[3], true));
        Assert.Equal(0, aufrufe);

        Assert.True(controller.BereiteZiehenVor(quelle));
        Assert.True(controller.LegeAb(original[3], false));
        controller.BeendeZiehen();
        Assert.Equal(new[] { original[1], original[2], quelle, original[3] }, records.Take(4));
        Assert.Equal("3", nummer(quelle));
        Assert.Same(quelle, liste.SelectedItem);

        // Echtes Enter im Positionsfeld, danach dieselben Knoepfe wie im Produkt.
        var box = (TextBox)leiste.FindName("PositionBox");
        box.Text = "20";
        using var source = new HwndSource(new HwndSourceParameters("Reihenfolge-Test") { Width = 1, Height = 1 });
        box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
        Assert.Same(quelle, records[19]);
        Assert.Equal("20", nummer(quelle));
        Klick(leiste, "An den Anfang");
        Assert.Same(quelle, records[0]);
        Klick(leiste, "Ans Ende");
        Assert.Same(quelle, records[^1]);
        Assert.Equal(records.Count.ToString(), nummer(quelle));

        var anzahlVorSperren = aufrufe;
        Assert.False(controller.VerschiebeAnPosition(quelle, 0));
        Assert.False(controller.VerschiebeAnPosition(quelle, records.Count + 1));
        erlaubt = false;
        Assert.False(controller.VerschiebeAnPosition(quelle, 1));
        erlaubt = true;
        var view = CollectionViewSource.GetDefaultView(records);
        view.Filter = item => ReferenceEquals(item, quelle);
        Assert.False(controller.BereiteZiehenVor(quelle));
        Assert.False(controller.VerschiebeAnPosition(quelle, 1));
        view.Filter = null;
        view.SortDescriptions.Add(new SortDescription(nameof(HaltungRecord.Id), ListSortDirection.Ascending));
        Assert.False(controller.VerschiebeAnPosition(quelle, 1));
        view.SortDescriptions.Clear();
        var sortierbareAnsicht = Assert.IsType<ListCollectionView>(view);
        // Fuer nicht vergleichbare Modelle ist ein stabiler Gleichstand der sichere Testsortierer.
        sortierbareAnsicht.CustomSort = new GleichstandSortierer();
        Assert.False(controller.VerschiebeAnPosition(quelle, 1));
        sortierbareAnsicht.CustomSort = null;
        Assert.Equal(anzahlVorSperren, aufrufe);

        // Eine fremde Bestandsaenderung waehrend des Ziehens verwirft das alte Ziel.
        Assert.True(controller.BereiteZiehenVor(quelle));
        records.Move(0, 1);
        Assert.False(controller.LegeAb(records[0], false));
        Assert.Equal(anzahlVorSperren, aufrufe);
        controller.BeendeZiehen();
        Assert.True(controller.VerschiebeAnPosition(quelle, 1));
        Assert.Equal(Enumerable.Range(1, records.Count).Select(i => i.ToString()), records.Select(nummer));

        Layout(host);
        var zeile = (ListBoxItem)liste.ItemContainerGenerator.ContainerFromItem(quelle);
        Assert.Contains(VisualTreeSafe.FindDescendants<TextBlock>(zeile), t => t.Text == "1");
        var linie = new ListenEinfuegelinie(zeile, true, (Brush)control.FindResource("AccentBrush"));
        var ebene = AdornerLayer.GetAdornerLayer(zeile);
        Assert.NotNull(ebene);
        ebene.Add(linie);
        Layout(host);
        OptionalesBild(host, bildname);
        ebene.Remove(linie);
        Klick(leiste, "Fertig");
        Assert.False(leiste.IstAktiv);
        controller.Beende();
    }

    private static void Klick(DependencyObject root, string text)
    {
        var button = VisualTreeSafe.FindDescendants<Button>(root).Single(b => Equals(b.Content, text));
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }

    private sealed class GleichstandSortierer : System.Collections.IComparer
    {
        public int Compare(object? x, object? y) => 0;
    }

    private static void Layout(UIElement element)
    {
        element.Measure(new Size(1400, 650));
        element.Arrange(new Rect(0, 0, 1400, 650));
        element.UpdateLayout();
    }

    private static void OptionalesBild(FrameworkElement element, string name)
    {
        if (Environment.GetEnvironmentVariable("SEWER_REIHENFOLGE_BILDER") != "1") return;
        var bitmap = new RenderTargetBitmap(1400, 650, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(TestRepoPaths.RepoFile(".tmp", $"reihenfolge-{name}.png"));
        encoder.Save(stream);
    }
}

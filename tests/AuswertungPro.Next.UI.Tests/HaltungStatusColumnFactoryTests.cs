using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 3 (Inventar 4.3): Die vier nur lesenden Statusspalten der
/// Haltungstabelle. Geprueft werden Kopf, Nur-Lesen, die gerechnete Zeile und dass Video und
/// Protokoll die vorhandenen Seitenbefehle mit dem Datensatz der Zeile aufrufen.
/// WPF-Objekte brauchen einen STA-Thread; deshalb laeuft jeder Fall im StaTestRunner.
/// </summary>
public sealed class HaltungStatusColumnFactoryTests
{
    [Fact]
    public void Die_vier_Spalten_tragen_ihren_Kopf_und_sind_nur_lesend()
    {
        StaTestRunner.Run(() =>
        {
            var spalten = new[]
            {
                (Kopf: "KI", Spalte: HaltungStatusColumnFactory.Ki("KI")),
                (Kopf: "PRÜFUNG", Spalte: HaltungStatusColumnFactory.Pruefung("PRÜFUNG")),
                (Kopf: "VIDEO", Spalte: HaltungStatusColumnFactory.Video("VIDEO")),
                (Kopf: "PROTOKOLL", Spalte: HaltungStatusColumnFactory.Protokoll("PROTOKOLL"))
            };

            foreach (var (kopf, spalte) in spalten)
            {
                Assert.Equal(kopf, spalte.Header);
                Assert.True(spalte.IsReadOnly, $"{kopf} muss nur lesend sein");
                Assert.False(spalte.CanUserSort, $"{kopf} ist keine Feldspalte und wird nicht sortiert");
                Assert.NotNull(spalte.CellTemplate);
                Assert.Null(spalte.CellEditingTemplate);
            }
        });
    }

    [Fact]
    public void Die_KI_Zelle_rechnet_den_Zeilenstatus_aus_dem_Datensatz()
    {
        StaTestRunner.Run(() =>
        {
            var traeger = Assert.IsType<ContentControl>(HaltungStatusColumnFactory.Ki("KI").CellTemplate.LoadContent());
            traeger.DataContext = MitOffenenKiBefunden(2);
            WpfBindungsPumpe.Leeren();

            var status = Assert.IsType<HaltungZeilenStatusErgebnis>(traeger.Content);
            Assert.Equal(KiAmpel.Offen, status.Ampel);
            Assert.Equal("2 offen", status.AmpelText);
        });
    }

    [Fact]
    public void Die_KI_Vorlage_zeigt_genau_einen_Ampelpunkt_und_den_Text()
    {
        StaTestRunner.Run(() =>
        {
            var traeger = Assert.IsType<ContentControl>(HaltungStatusColumnFactory.Ki("KI").CellTemplate.LoadContent());
            var zeile = Assert.IsType<StackPanel>(traeger.ContentTemplate.LoadContent());
            zeile.DataContext = HaltungZeilenStatus.Bestimme(MitOffenenKiBefunden(1));
            WpfBindungsPumpe.Leeren();

            var punkte = zeile.Children.OfType<Ellipse>().ToList();
            Assert.Equal(4, punkte.Count);
            Assert.Single(punkte, p => p.Visibility == Visibility.Visible);
            Assert.All(punkte, p => Assert.Equal(9d, p.Width));
            Assert.Equal("1 offen", zeile.Children.OfType<TextBlock>().Single().Text);
        });
    }

    [Fact]
    public void Die_Pruefung_zeigt_genau_eine_Kapsel_mit_dem_Pruefungstext()
    {
        StaTestRunner.Run(() =>
        {
            var traeger = Assert.IsType<ContentControl>(HaltungStatusColumnFactory.Pruefung("PRÜFUNG").CellTemplate.LoadContent());
            var huelle = Assert.IsType<Grid>(traeger.ContentTemplate.LoadContent());

            var record = new HaltungRecord();
            record.SetFieldValue(FieldKeys.WorkflowStatus, "abgeschlossen", FieldSource.Manual, userEdited: true);
            huelle.DataContext = HaltungZeilenStatus.Bestimme(record);
            WpfBindungsPumpe.Leeren();

            var kapseln = huelle.Children.OfType<Border>().ToList();
            Assert.Equal(3, kapseln.Count);
            var sichtbar = Assert.Single(kapseln.Where(k => k.Visibility == Visibility.Visible));
            Assert.Equal("fachlich geprüft", Assert.IsType<TextBlock>(sichtbar.Child).Text);
        });
    }

    [Fact]
    public void Der_Video_Knopf_ruft_den_PlayVideoCommand_der_Seite_mit_der_Zeile()
    {
        StaTestRunner.Run(() =>
        {
            var zelle = Assert.IsType<Grid>(HaltungStatusColumnFactory.Video("VIDEO").CellTemplate.LoadContent());
            var knopf = zelle.Children.OfType<Button>().Single();

            var befehl = Assert.IsType<Binding>(BindingOperations.GetBinding(knopf, ButtonBase.CommandProperty));
            Assert.Equal("DataContext.PlayVideoCommand", befehl.Path.Path);
            Assert.Equal(typeof(DataGrid), Assert.IsType<RelativeSource>(befehl.RelativeSource).AncestorType);

            var parameter = Assert.IsType<Binding>(BindingOperations.GetBinding(knopf, ButtonBase.CommandParameterProperty));
            Assert.Equal(".", parameter.Path.Path);

            Assert.Equal("Video abspielen", knopf.ToolTip);
            Assert.NotNull(BindingOperations.GetBinding(knopf, AutomationProperties.NameProperty));
        });
    }

    [Fact]
    public void Der_Protokoll_Knopf_ruft_den_OpenOriginalPdfCommand_der_Seite()
    {
        StaTestRunner.Run(() =>
        {
            var zelle = Assert.IsType<Grid>(HaltungStatusColumnFactory.Protokoll("PROTOKOLL").CellTemplate.LoadContent());
            var knopf = zelle.Children.OfType<Button>().Single();

            var befehl = Assert.IsType<Binding>(BindingOperations.GetBinding(knopf, ButtonBase.CommandProperty));
            Assert.Equal("DataContext.OpenOriginalPdfCommand", befehl.Path.Path);
            Assert.Equal("Protokoll öffnen", knopf.ToolTip);
            Assert.Equal("PDF", Assert.IsType<TextBlock>(knopf.Content).Text);
        });
    }

    [Fact]
    public void Ohne_Video_und_ohne_Protokoll_steht_ein_Gedankenstrich_mit_Hinweis()
    {
        StaTestRunner.Run(() =>
        {
            var leer = new HaltungRecord();

            var video = Assert.IsType<Grid>(HaltungStatusColumnFactory.Video("VIDEO").CellTemplate.LoadContent());
            video.DataContext = leer;
            WpfBindungsPumpe.Leeren();
            Assert.Equal(Visibility.Collapsed, video.Children.OfType<Button>().Single().Visibility);
            var videoStrich = video.Children.OfType<TextBlock>().Single();
            Assert.Equal(Visibility.Visible, videoStrich.Visibility);
            Assert.Equal("–", videoStrich.Text);
            Assert.Equal("kein Video", videoStrich.ToolTip);

            var protokoll = Assert.IsType<Grid>(HaltungStatusColumnFactory.Protokoll("PROTOKOLL").CellTemplate.LoadContent());
            protokoll.DataContext = leer;
            WpfBindungsPumpe.Leeren();
            Assert.Equal(Visibility.Collapsed, protokoll.Children.OfType<Button>().Single().Visibility);
            Assert.Equal("kein Protokoll", protokoll.Children.OfType<TextBlock>().Single().ToolTip);
        });
    }

    [Fact]
    public void Mit_Video_und_Protokoll_erscheinen_die_Knoepfe_statt_des_Strichs()
    {
        StaTestRunner.Run(() =>
        {
            var record = new HaltungRecord();
            record.SetFieldValue(FieldKeys.Link, @"D:\Medien\10001-10002.mp4", FieldSource.Manual, userEdited: true);
            record.SetFieldValue(FieldKeys.PdfPath, @"D:\Protokolle\10001-10002.pdf", FieldSource.Manual, userEdited: true);

            var video = Assert.IsType<Grid>(HaltungStatusColumnFactory.Video("VIDEO").CellTemplate.LoadContent());
            video.DataContext = record;
            WpfBindungsPumpe.Leeren();
            Assert.Equal(Visibility.Visible, video.Children.OfType<Button>().Single().Visibility);
            Assert.Equal(Visibility.Collapsed, video.Children.OfType<TextBlock>().Single().Visibility);

            var protokoll = Assert.IsType<Grid>(HaltungStatusColumnFactory.Protokoll("PROTOKOLL").CellTemplate.LoadContent());
            protokoll.DataContext = record;
            WpfBindungsPumpe.Leeren();
            Assert.Equal(Visibility.Visible, protokoll.Children.OfType<Button>().Single().Visibility);
        });
    }

    private static HaltungRecord MitOffenenKiBefunden(int anzahl)
    {
        var revision = new ProtocolRevision();
        for (var i = 0; i < anzahl; i++)
            revision.Entries.Add(new ProtocolEntry { Code = "BAB", Ai = new ProtocolEntryAiMeta { Accepted = false } });

        return new HaltungRecord { Protocol = new ProtocolDocument { Current = revision } };
    }
}

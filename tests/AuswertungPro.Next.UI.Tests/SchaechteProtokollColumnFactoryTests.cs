using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 6: In "Kompakt" und "Dokumente und Medien" steht das Schachtprotokoll
/// als Knopf statt als roher Dateipfad — dieselbe Loesung wie in der Haltungsliste. Der Knopf
/// ist nur lesend und ruft den bereits vorhandenen Weg der Seite auf; bearbeitet wird der Pfad
/// weiterhin in "Alle Spalten" und in den Eingabefeldern.
/// </summary>
public sealed class SchaechteProtokollColumnFactoryTests
{
    [Fact]
    public void Die_Spalte_ist_nur_lesend_und_traegt_keinen_Feldpfad()
    {
        StaTestRunner.Run(() =>
        {
            var spalte = SchaechteProtokollColumnFactory.Create("PROTOKOLL");

            Assert.Equal("PROTOKOLL", spalte.Header);
            Assert.True(spalte.IsReadOnly);
            Assert.Null(spalte.CellEditingTemplate);
        });
    }

    [Fact]
    public void Mit_Protokoll_erscheint_der_Knopf_mit_Namen_und_Hinweis()
    {
        StaTestRunner.Run(() =>
        {
            var (knopf, strich) = Zelle(
                ("Schachtnummer", "78998"),
                (FieldKeys.PdfPath, @"D:\Projekt\Schaechte\78998.pdf"));

            Assert.Equal(Visibility.Visible, knopf.Visibility);
            Assert.Equal(Visibility.Collapsed, strich.Visibility);
            Assert.Equal("PDF", Assert.IsType<TextBlock>(knopf.Content).Text);
            Assert.Equal("Protokoll öffnen", knopf.ToolTip);
            Assert.Equal("Protokoll 78998 öffnen", AutomationProperties.GetName(knopf));

            var befehl = Assert.IsType<Binding>(BindingOperations.GetBinding(knopf, ButtonBase.CommandProperty));
            Assert.Equal(nameof(SchaechtePage.ProtokollOeffnenCommand), befehl.Path.Path);
            Assert.Equal(typeof(SchaechtePage), befehl.RelativeSource.AncestorType);

            var parameter = Assert.IsType<Binding>(BindingOperations.GetBinding(knopf, ButtonBase.CommandParameterProperty));
            Assert.Equal(".", parameter.Path.Path);
        });
    }

    [Fact]
    public void Ohne_Protokoll_bleibt_ein_Gedankenstrich_mit_Hinweis()
    {
        StaTestRunner.Run(() =>
        {
            var (knopf, strich) = Zelle(("Schachtnummer", "78998"));

            Assert.Equal(Visibility.Collapsed, knopf.Visibility);
            Assert.Equal(Visibility.Visible, strich.Visibility);
            Assert.Equal("–", strich.Text);
            Assert.Equal("kein Protokoll", strich.ToolTip);
        });
    }

    /// <summary>
    /// Fix-Runde 1: Der Knopf erscheint genau dann, wenn der Oeffner etwas oeffnen wuerde.
    /// Nur PDF_Eigen (oder PDF_All) ist fuer ihn keine Quelle; ein Link auf eine PDF dagegen schon.
    /// </summary>
    [Fact]
    public void Ein_anderes_PDF_Feld_allein_zeigt_keinen_Knopf()
    {
        StaTestRunner.Run(() =>
        {
            var (knopf, strich) = Zelle((FieldKeys.PdfEigen, @"D:\Projekt\Schaechte\78998.pdf"));

            Assert.Equal(Visibility.Collapsed, knopf.Visibility);
            Assert.Equal(Visibility.Visible, strich.Visibility);
        });
    }

    [Fact]
    public void Ein_Link_auf_eine_PDF_zeigt_den_Knopf()
    {
        StaTestRunner.Run(() =>
        {
            var (knopf, strich) = Zelle((FieldKeys.Link, @"D:\Projekt\Schaechte\78998.pdf"));

            Assert.Equal(Visibility.Visible, knopf.Visibility);
            Assert.Equal(Visibility.Collapsed, strich.Visibility);
        });
    }

    /// <summary>
    /// Der vorlesbare Name nimmt die Schachtnummer ueber dieselbe Regel wie die Seite
    /// (<see cref="SchaechteColumnPolicy.GetSchachtNumber"/>) — auch aus der Spalte "Nr.".
    /// Ohne Nummer bleibt es beim schlichten Namen statt bei einer Luecke im Satz.
    /// </summary>
    [Fact]
    public void Der_Name_nimmt_die_Schachtnummer_wie_die_Seite()
    {
        StaTestRunner.Run(() =>
        {
            var (mitNr, _) = Zelle(("Nr.", "12"), (FieldKeys.PdfPath, @"D:\a.pdf"));
            Assert.Equal("Protokoll 12 öffnen", AutomationProperties.GetName(mitNr));

            var (ohneNr, _) = Zelle((FieldKeys.PdfPath, @"D:\a.pdf"));
            Assert.Equal("Protokoll öffnen", AutomationProperties.GetName(ohneNr));
        });
    }

    /// <summary>Die Spalte ist virtuell: kein Feld, deshalb auch nichts fuer die Layout-Speicherung.</summary>
    [Fact]
    public void Der_Schluessel_der_Spalte_ist_virtuell()
        => Assert.True(NovaStatusSpalten.IstVirtuell(NovaStatusSpalten.Protokoll));

    private static (Button Knopf, TextBlock Strich) Zelle(params (string Feld, string Wert)[] felder)
    {
        var spalte = SchaechteProtokollColumnFactory.Create("PROTOKOLL");
        var wurzel = Assert.IsType<Grid>(spalte.CellTemplate.LoadContent());

        var record = new SchachtRecord();
        foreach (var (feld, wert) in felder)
            record.SetFieldValue(feld, wert, FieldSource.Manual, userEdited: false);

        wurzel.DataContext = record;
        WpfBindungsPumpe.Leeren();

        return (wurzel.Children.OfType<Button>().Single(), wurzel.Children.OfType<TextBlock>().Single());
    }
}

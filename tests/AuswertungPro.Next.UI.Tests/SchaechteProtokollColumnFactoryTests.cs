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
            var (knopf, strich) = Zelle(pdfPfad: @"D:\Projekt\Schaechte\78998.pdf");

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
            var (knopf, strich) = Zelle(pdfPfad: null);

            Assert.Equal(Visibility.Collapsed, knopf.Visibility);
            Assert.Equal(Visibility.Visible, strich.Visibility);
            Assert.Equal("–", strich.Text);
            Assert.Equal("kein Protokoll", strich.ToolTip);
        });
    }

    /// <summary>Die Spalte ist virtuell: kein Feld, deshalb auch nichts fuer die Layout-Speicherung.</summary>
    [Fact]
    public void Der_Schluessel_der_Spalte_ist_virtuell()
        => Assert.True(NovaStatusSpalten.IstVirtuell(NovaStatusSpalten.Protokoll));

    private static (Button Knopf, TextBlock Strich) Zelle(string? pdfPfad)
    {
        var spalte = SchaechteProtokollColumnFactory.Create("PROTOKOLL");
        var wurzel = Assert.IsType<Grid>(spalte.CellTemplate.LoadContent());

        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "78998", FieldSource.Manual, userEdited: false);
        if (!string.IsNullOrWhiteSpace(pdfPfad))
            record.SetFieldValue(FieldKeys.PdfPath, pdfPfad, FieldSource.Manual, userEdited: false);

        wurzel.DataContext = record;
        WpfBindungsPumpe.Leeren();

        return (wurzel.Children.OfType<Button>().Single(), wurzel.Children.OfType<TextBlock>().Single());
    }
}

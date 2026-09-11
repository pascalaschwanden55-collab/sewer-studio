using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class RedesignSiaFeldTests
{
    [Fact]
    public void Bauwerkswechsel_filtert_die_Funktion_ohne_den_bestehenden_Wert_zu_loeschen()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Bauwerksart", "Normschacht", FieldSource.Manual, true);
        record.SetFieldValue("Funktion", "Kontrollschacht", FieldSource.Manual, true);
        var commits = new List<string>();
        var builder = new SchaechteRecordDetailsBuilder(_ => [], _ => null,
            (_, feld, wert) => { commits.Add(feld.AnzeigeName); record.SetFieldValue(feld.AnzeigeName, wert ?? "", FieldSource.Manual, true); });
        var items = builder.Build(["Bauwerksart", "Funktion"], record).SelectMany(g => g.Items).ToArray();
        var funktion = items.Single(i => i.FieldName == "Funktion");
        Assert.DoesNotContain("Regenbecken_Fangbecken", funktion.Options);
        Assert.Contains("Fettabscheider", funktion.Options);
        items.Single(i => i.FieldName == "Bauwerksart").Value = "Spezialbauwerk";
        Assert.Contains("Regenbecken_Fangbecken", funktion.Options);
        Assert.DoesNotContain("Dachwasserschacht", funktion.Options);
        Assert.Equal("Kontrollschacht", record.GetFieldValue("Funktion"));
        Assert.Equal(["Bauwerksart"], commits);
    }

    [Theory]
    [InlineData("Regenbecken_Fangbecken", "Regenbecken Fangbecken")]
    [InlineData("abflussloseGrube", "Abflusslose Grube")]
    [InlineData("Oelabscheider", "Ölabscheider")]
    public void Normbegriffe_werden_lesbar_angezeigt(string norm, string anzeige)
        => Assert.Equal(anzeige, SiaBegriffAnzeige.Klartext(norm));

    [Fact]
    public void Alter_Wert_ausserhalb_der_Liste_bleibt_sichtbar_und_unveraendert()
    {
        var commits = new List<string>();
        var item = new RecordDetailItem("Funktion", "Normschacht", commits.Add, isCombo: true, options: ["Kontrollschacht"]);
        Assert.Contains("Normschacht", item.AuswahlHinweis);
        item.ErsetzeOptionen(["Pumpwerk"]);
        Assert.Equal("Normschacht", item.Value);
        Assert.Empty(commits);
        item.Value = "Pumpwerk";
        Assert.Empty(item.AuswahlHinweis);
    }

    [Fact]
    public void Innenmass_Formular_meldet_den_Bereich_am_Feld()
    {
        var builder = new SchaechteRecordDetailsBuilder(_ => [], _ => null, (_, _, _) => { });
        var item = Assert.Single(builder.Build([FieldKeys.ShaftDimension1Mm], new SchachtRecord()).SelectMany(g => g.Items));
        item.Value = "4500";
        Assert.Contains("4000", item.Error);
        item.Value = "1000";
        Assert.Empty(item.Error);
    }

    [Fact]
    public void Tabelleneditor_filtert_nach_dem_Schacht_der_jeweiligen_Zeile()
    {
        StaTestRunner.Run(() =>
        {
            var s = new SchachtRecord();
            s.SetFieldValue("Bauwerksart", "Normschacht", FieldSource.Manual, true);
            s.SetFieldValue("Funktion", "Kontrollschacht", FieldSource.Manual, true);
            var column = AuswertungPro.Next.UI.Views.Pages.DataGridComboColumnFactory.Create(
                "Funktion", "Funktion", "SchachtFunktionOptions", "Funktion", (_, _) => { }, (_, _) => { },
                false, false, useSelectedItemWhenNotFreeText: false, itemsBinding: SchachtNormoptionen.FuerSpalte("Funktion"));
            column.CellEditingTemplate.Seal();
            var combo = (System.Windows.Controls.ComboBox)column.CellEditingTemplate.LoadContent();
            combo.DataContext = s;
            Assert.DoesNotContain("Regenbecken_Fangbecken", combo.Items.Cast<string>());
            s.SetFieldValue("Bauwerksart", "Spezialbauwerk", FieldSource.Manual, true);
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
            Assert.Contains("Regenbecken_Fangbecken", combo.Items.Cast<string>());
            Assert.Equal("Kontrollschacht", s.GetFieldValue("Funktion"));
        });
    }

    [Fact]
    public void Neue_Haltung_bietet_beide_Schaechte_ohne_Import_an()
    {
        var record = new Project().CreateNewRecord();
        var groups = DataPageRecordDetailsBuilder.Build(record,
            f => new RecordDetailItem(f, record.GetFieldValue(f), _ => { }) { FieldName = f });
        var master = groups.Single(g => g.Title == "Stammdaten");
        Assert.Contains(master.Items, i => i.FieldName == "Schacht_oben");
        Assert.Contains(master.Items, i => i.FieldName == "Schacht_unten");
    }

    [Theory]
    [InlineData("Funktion")]
    [InlineData("Material")]
    [InlineData("Status")]
    [InlineData("Sanierungsbedarf")]
    public void Schacht_Normfelder_sind_auch_im_echten_Formular_Auswahlfelder(string feld)
    {
        var builder = new SchaechteRecordDetailsBuilder(_ => ["", "Test"], _ => null, (_, _, _) => { });
        var items = builder.Build([feld], new SchachtRecord()).SelectMany(g => g.Items);
        var item = Assert.Single(items, i => i.FieldName == feld);
        Assert.True(item.IsCombo);
        Assert.False(item.AllowFreeText);
    }

    [Fact]
    public void Nutzungsart_ist_auch_in_der_Tabelle_eine_feste_Auswahl()
    {
        Assert.True(GridDropdownFieldPolicy.TryResolve(FieldKeys.UsageType, out var spec));
        Assert.False(spec.AllowFreeText);
    }
}

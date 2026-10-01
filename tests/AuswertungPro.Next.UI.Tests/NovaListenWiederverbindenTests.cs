using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class NovaListenWiederverbindenTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Nach_Wiederverbinden_folgt_das_offene_Formular_wieder_dem_Datensatz(bool schacht)
    {
        StaTestRunner.Run(() =>
        {
            var h = new HaltungRecord();
            var s = new SchachtRecord();
            var hl = new HaltungAufklappListe { ItemsSource = new[] { h } };
            var sl = new SchachtAufklappListe { ItemsSource = new[] { s } };
            IReadOnlyList<RecordDetailGroup> Gruppen(string wert) =>
                [new RecordDetailGroup("Stammdaten", "", [new RecordDetailItem("Strasse", wert, _ => { }) { FieldName = "Strasse" }])];
            using var hc = new DataPageAufklappListeController(hl, () => null, r => Gruppen(r.GetFieldValue("Strasse")));
            using var sc = new SchaechteAufklappListeController(sl, () => null, r => Gruppen(r.GetFieldValue("Strasse")));
            if (schacht) { sc.Verdrahte(); sl.KlappeAuf(s); sc.Dispose(); sc.Verdrahte(); }
            else { hc.Verdrahte(); hl.KlappeAuf(h); hc.Dispose(); hc.Verdrahte(); }
            if (schacht) s.SetFieldValue("Strasse", "Nach Rueckkehr", FieldSource.Manual, false);
            else h.SetFieldValue("Strasse", "Nach Rueckkehr", FieldSource.Manual, false);
            var themen = schacht ? sl.Themen : hl.Themen;
            Assert.Equal("Nach Rueckkehr", themen!.Single().EinzelGruppe.Single().Items.Single().Value);
        });
    }
}

using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Auswahllisten zeigen die WebGIS-Begriffe; ein Altwert verschwindet nie aus der Anzeige.</summary>
public sealed class WebGisAuswahlTests
{
    [Fact]
    public void Haltungsauswahl_ist_die_webgis_liste()
    {
        Assert.Equal(["", "Unbekannt", "In Betrieb", "Ausser Betrieb", "Tot/Aufgehoben, verfüllt"],
            FieldCatalog.GetComboItems(FieldKeys.OperatingStatus));
        Assert.Contains("Kreisprofil (K)", FieldCatalog.GetComboItems(FieldKeys.ProfileType));
        Assert.Contains("Regenabwasser", FieldCatalog.GetComboItems(FieldKeys.UsageType));
        Assert.DoesNotContain("Niederschlagsabwasser", FieldCatalog.GetComboItems(FieldKeys.UsageType));
        Assert.Equal(WebGisBegriffe.Fuer(false, FieldKeys.RehabilitationNeed)!.Auswahl, SanierungsbedarfOptionen.Alle);
    }

    [Fact]
    public void Normschacht_funktion_ist_die_webgis_liste_und_altwert_bleibt_sichtbar()
    {
        var liste = SchachtNormoptionen.Funktion("Normschacht", "Absturzbauwerk");
        Assert.Contains("Pumpenschacht", liste);
        Assert.Contains("Absturzbauwerk", liste); // Altwert: sonst waere das Feld leer und beim ersten Klick weg
        Assert.DoesNotContain("Pumpwerk", SchachtNormoptionen.Funktion("Normschacht", "Pumpenschacht"));
    }

    [Fact]
    public void Altwert_einer_haltung_wird_der_auswahl_angehaengt()
    {
        var status = new ObservableCollection<string>(FieldCatalog.GetComboItems(FieldKeys.OperatingStatus));
        var h = new HaltungRecord();
        h.Fields[FieldKeys.OperatingStatus] = "weitere";
        var sets = new DataPageDropdownOptionSets(new(), new(), new(), new(), new())
        {
            WebGisListen = new Dictionary<string, ObservableCollection<string>> { [FieldKeys.OperatingStatus] = status },
        };

        DataPageDropdownOptionSynchronizer.SyncFromRecords([h], sets);

        Assert.Equal("weitere", status[^1]);
    }

    [Fact]
    public void Schachtspalten_nutzungsart_und_lagebestimmung_sind_auswahlfelder()
    {
        Assert.Equal(FieldKeys.UsageType, SchaechteColumnPolicy.ResolveOptionField("Nutzungsart"));
        Assert.Equal(FieldKeys.PositionAccuracy, SchaechteColumnPolicy.ResolveOptionField("Lagebestimmung"));
    }
}

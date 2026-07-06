using System;
using System.Collections.Generic;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// "Sanieren = Nein" blendet die Sanierungs-Folgefelder (Kosten, Massnahmen, …) aus;
/// nur das Feld "Sanieren_JaNein" bleibt sichtbar. Reagiert live auf Umschaltung.
/// </summary>
public sealed class SanierungSichtbarkeitTests
{
    private static RecordDetailItem Item(string label, string value)
        => new(label, value, _ => { });

    private static Dictionary<string, RecordDetailItem> Build(string sanierenWert, out RecordDetailItem kosten, out RecordDetailItem sanieren)
    {
        sanieren = Item("Sanieren", sanierenWert);
        kosten = Item("Kosten", string.Empty);
        return new Dictionary<string, RecordDetailItem>(StringComparer.Ordinal)
        {
            ["Sanieren_JaNein"] = sanieren,
            ["Kosten"] = kosten,
            ["Empfohlene_Sanierungsmassnahmen"] = Item("Massnahmen", string.Empty),
        };
    }

    [Fact]
    public void Nein_blendet_folgefelder_aus_feld_selbst_bleibt()
    {
        var items = Build("Nein", out var kosten, out var sanieren);

        DataPageRecordDetailsBuilder.WireSanierungSichtbarkeit(items);

        Assert.False(kosten.IsVisible);
        Assert.False(items["Empfohlene_Sanierungsmassnahmen"].IsVisible);
        Assert.True(sanieren.IsVisible);
    }

    [Fact]
    public void Ja_zeigt_folgefelder()
    {
        var items = Build("Ja", out var kosten, out _);

        DataPageRecordDetailsBuilder.WireSanierungSichtbarkeit(items);

        Assert.True(kosten.IsVisible);
    }

    [Fact]
    public void Umschalten_auf_Nein_blendet_live_aus()
    {
        var items = Build("Ja", out var kosten, out var sanieren);
        DataPageRecordDetailsBuilder.WireSanierungSichtbarkeit(items);
        Assert.True(kosten.IsVisible);

        sanieren.Value = "Nein";

        Assert.False(kosten.IsVisible);
    }
}

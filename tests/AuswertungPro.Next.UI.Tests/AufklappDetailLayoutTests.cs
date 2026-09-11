using System.Text.Json;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AufklappDetailLayoutTests
{
    [Fact]
    public void Vorschau_und_Abbruch_veraendern_keine_Daten_oder_Originalkarten()
    {
        var commits = 0;
        var original = new RecordDetailItem("A", "Original", _ => commits++)
            { FieldName = "A", IsVisible = false };
        RecordDetailGroup[] standard = [new("Stamm", "", [original])];
        var vorschau = AufklappDetailLayout.Vorschau(standard, RecordDetailLayout.Empty);
        var kopie = vorschau.Single().Items.Single();
        Assert.NotSame(original, kopie);
        Assert.True(kopie.IsReadOnly);
        Assert.True(kopie.IsVisible);
        kopie.Value = "Versuch";
        kopie.IsHiddenByUser = true;
        Assert.Equal("Original", original.Value);
        Assert.False(original.IsHiddenByUser);
        Assert.Equal(0, commits);
    }

    [Fact]
    public void Anordnung_und_Ausblendung_ueberstehen_Speichern_und_neue_Datensaetze()
    {
        var vorschau = AufklappDetailLayout.Vorschau(Standard(), RecordDetailLayout.Empty);
        var verschoben = RecordDetailDragOperations.MoveField(vorschau, "Stamm", 0, "Dokumente", 0)!;
        verschoben.Single(g => g.Title == "Stamm").Items.Single().IsHiddenByUser = true;
        var settings = RecordDetailLayoutSettingsMapper.ToSettings(RecordDetailLayoutApplier.Capture(verschoben));
        var geladen = JsonSerializer.Deserialize<DetailLayoutSettings>(JsonSerializer.Serialize(settings));
        var neuerDatensatz = Standard();
        var themen = AufklappDetailLayout.Themen(neuerDatensatz, geladen);
        Assert.Equal("Dokumente", Assert.Single(themen).Title);
        var gruppe = themen.Single().EinzelGruppe.Single();
        Assert.Equal(new[] { "A", "C" }, gruppe.Items.Select(i => i.FieldName));
        Assert.NotEqual(RecordDetailGroupKind.Documents, gruppe.Kind);
        Assert.Equal(3, neuerDatensatz.Sum(g => g.Items.Count));
        Assert.Same(neuerDatensatz[0].Items[0], gruppe.Items[0]);
    }

    [Fact]
    public void Wieder_einblenden_und_Standard_stellen_alle_Felder_wieder_her()
    {
        var vorschau = AufklappDetailLayout.Vorschau(Standard(), RecordDetailLayout.Empty);
        var item = vorschau[0].Items[0];
        item.IsHiddenByUser = true;
        item.IsHiddenByUser = false;
        Assert.Empty(RecordDetailLayoutApplier.Capture(vorschau).HiddenFields);
        var standard = AufklappDetailLayout.Vorschau(Standard(), RecordDetailLayout.Empty);
        Assert.Equal(new[] { "A", "B", "C" }, standard.SelectMany(g => g.Items).Select(i => i.FieldName));
        Assert.All(standard.SelectMany(g => g.Items), i => Assert.False(i.IsHiddenByUser));
    }

    private static RecordDetailGroup[] Standard() =>
    [
        new("Stamm", "", [Item("A"), Item("B")]),
        new("Dokumente", "", [Item("C")], RecordDetailGroupKind.Documents)
    ];

    private static RecordDetailItem Item(string key) => new(key, key, _ => { }) { FieldName = key };
}

using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Die persoenliche Gestaltung der Detailansicht: Spaltenreihenfolge, Feldreihenfolge,
/// Feld in eine andere Spalte, Feld ausblenden.
///
/// Lehre aus dieser Sitzung: Ein Test, der bei zwei verschiedenen Regeln dasselbe
/// Ergebnis erwartet, beweist keine von beiden. Die Faelle unten sind deshalb so
/// gewaehlt, dass sich die richtige und die naheliegende falsche Regel unterscheiden.
/// </summary>
public sealed class RecordDetailLayoutApplierTests
{
    private static RecordDetailItem Item(string fieldName)
        => new(fieldName, fieldName, _ => { }) { FieldName = fieldName };

    private static RecordDetailGroup Group(string title, RecordDetailGroupKind kind, params string[] fields)
        => new(title, string.Empty, fields.Select(Item).ToList(), kind);

    private static RecordDetailGroup Stammdaten(params string[] fields)
        => Group("Stammdaten", RecordDetailGroupKind.MasterData, fields);

    private static RecordDetailGroup Zustand(params string[] fields)
        => Group("Zustand", RecordDetailGroupKind.Condition, fields);

    private static RecordDetailGroup Weitere(params string[] fields)
        => Group("Weitere", RecordDetailGroupKind.Additional, fields);

    private static string[] Titles(IReadOnlyList<RecordDetailGroup> groups)
        => groups.Select(x => x.Title).ToArray();

    private static string[] Fields(RecordDetailGroup group)
        => group.Items.Select(x => x.FieldName).ToArray();

    private static string[] SichtbareFelder(RecordDetailGroup group)
        => group.Items.Where(x => !x.IsHiddenByUser).Select(x => x.FieldName).ToArray();

    private static RecordDetailLayout Layout(
        (string Title, string[] Fields)[] columns,
        params string[] hidden)
        => new(
            columns.Select(c => new RecordDetailLayoutColumn(c.Title, c.Fields)).ToList(),
            hidden);

    // --- Rueckfallebene -----------------------------------------------------

    [Fact]
    public void Apply_ohne_Layout_laesst_alles_unveraendert()
    {
        var groups = new[] { Stammdaten("A", "B"), Zustand("X") };

        Assert.Same(groups, RecordDetailLayoutApplier.Apply(groups, null));
        Assert.Same(groups, RecordDetailLayoutApplier.Apply(groups, RecordDetailLayout.Empty));
    }

    // --- Spaltenreihenfolge -------------------------------------------------

    [Fact]
    public void Apply_stellt_die_Spalten_in_die_gespeicherte_Reihenfolge()
    {
        var groups = new[] { Stammdaten("A"), Zustand("X"), Weitere("Z") };

        var result = RecordDetailLayoutApplier.Apply(groups, Layout(new[]
        {
            ("Weitere", new[] { "Z" }),
            ("Stammdaten", new[] { "A" }),
            ("Zustand", new[] { "X" })
        }));

        Assert.Equal(new[] { "Weitere", "Stammdaten", "Zustand" }, Titles(result));
    }

    [Fact]
    public void Apply_haelt_eine_unbekannte_Spalte_bei_ihrem_Vorgaenger()
    {
        // NEU steht hinter Stammdaten und kommt im Layout nicht vor. Der Fall ist so
        // gewaehlt, dass "hinter Stammdaten" und "ans Ende" verschiedene Ergebnisse geben.
        var groups = new[]
        {
            Stammdaten("A"),
            Group("NEU", RecordDetailGroupKind.Additional, "N"),
            Zustand("X"),
            Weitere("Z")
        };

        var result = RecordDetailLayoutApplier.Apply(groups, Layout(new[]
        {
            ("Weitere", new[] { "Z" }),
            ("Stammdaten", new[] { "A" }),
            ("Zustand", new[] { "X" })
        }));

        Assert.Equal(new[] { "Weitere", "Stammdaten", "NEU", "Zustand" }, Titles(result));
    }

    // --- Feldreihenfolge ----------------------------------------------------

    [Fact]
    public void Apply_stellt_die_Felder_in_die_gespeicherte_Reihenfolge()
    {
        var groups = new[] { Stammdaten("A", "B", "C") };

        var result = RecordDetailLayoutApplier.Apply(groups, Layout(new[]
        {
            ("Stammdaten", new[] { "C", "A", "B" })
        }));

        Assert.Equal(new[] { "C", "A", "B" }, Fields(result[0]));
    }

    [Fact]
    public void Apply_haelt_ein_neues_Feld_bei_seinem_Vorgaenger_statt_am_Ende()
    {
        var groups = new[] { Stammdaten("A", "NEU", "B", "C") };

        var result = RecordDetailLayoutApplier.Apply(groups, Layout(new[]
        {
            ("Stammdaten", new[] { "C", "A", "B" })
        }));

        Assert.Equal(new[] { "C", "A", "NEU", "B" }, Fields(result[0]));
    }

    // --- Feld in eine andere Spalte -----------------------------------------

    [Fact]
    public void Apply_holt_ein_Feld_in_die_Spalte_die_das_Layout_nennt()
    {
        // Der Builder legt "Bemerkungen" in "Weitere"; der Anwender hat es in die
        // Stammdaten gezogen.
        var groups = new[] { Stammdaten("A"), Weitere("Bemerkungen", "Z") };

        var result = RecordDetailLayoutApplier.Apply(groups, Layout(new[]
        {
            ("Stammdaten", new[] { "A", "Bemerkungen" }),
            ("Weitere", new[] { "Z" })
        }));

        Assert.Equal(new[] { "A", "Bemerkungen" }, Fields(result[0]));
        Assert.Equal(new[] { "Z" }, Fields(result[1]));
    }

    [Fact]
    public void Apply_setzt_das_umgehaengte_Feld_an_die_genannte_Stelle()
    {
        var groups = new[] { Stammdaten("A", "B"), Weitere("Bemerkungen") };

        var result = RecordDetailLayoutApplier.Apply(groups, Layout(new[]
        {
            ("Stammdaten", new[] { "A", "Bemerkungen", "B" }),
            ("Weitere", Array.Empty<string>())
        }));

        Assert.Equal(new[] { "A", "Bemerkungen", "B" }, Fields(result[0]));
    }

    [Fact]
    public void Apply_laesst_ein_Feld_das_zwei_Spalten_beanspruchen_nur_einmal_erscheinen()
    {
        // Ein von Hand verfaelschtes Layout darf keine Karte verdoppeln.
        var groups = new[] { Stammdaten("A"), Weitere("B") };

        var result = RecordDetailLayoutApplier.Apply(groups, Layout(new[]
        {
            ("Stammdaten", new[] { "A", "B" }),
            ("Weitere", new[] { "B" })
        }));

        var alle = result.SelectMany(Fields).ToArray();
        Assert.Equal(new[] { "A", "B" }, alle.OrderBy(x => x).ToArray());
        Assert.Single(alle, x => x == "B");
    }

    [Fact]
    public void Apply_uebergeht_Felder_die_es_nicht_mehr_gibt()
    {
        var groups = new[] { Stammdaten("A", "B") };

        var result = RecordDetailLayoutApplier.Apply(groups, Layout(new[]
        {
            ("Stammdaten", new[] { "WEG", "B", "A" })
        }));

        Assert.Equal(new[] { "B", "A" }, Fields(result[0]));
    }

    [Fact]
    public void Apply_uebergeht_Spalten_die_es_nicht_mehr_gibt()
    {
        var groups = new[] { Stammdaten("A") };

        var result = RecordDetailLayoutApplier.Apply(groups, Layout(new[]
        {
            ("Gibt_es_nicht", new[] { "Q" }),
            ("Stammdaten", new[] { "A" })
        }));

        Assert.Equal(new[] { "Stammdaten" }, Titles(result));
    }

    // --- Ausblenden ---------------------------------------------------------

    [Fact]
    public void Apply_blendet_die_genannten_Felder_aus_ohne_sie_zu_entfernen()
    {
        var groups = new[] { Stammdaten("A", "B", "C") };

        var result = RecordDetailLayoutApplier.Apply(groups, Layout(
            new[] { ("Stammdaten", new[] { "A", "B", "C" }) },
            "B"));

        // Die Karte bleibt in ihrer Gruppe - beim Zurueckholen geht dadurch nichts verloren.
        Assert.Equal(new[] { "A", "B", "C" }, Fields(result[0]));
        Assert.Equal(new[] { "A", "C" }, SichtbareFelder(result[0]));
    }

    [Fact]
    public void Apply_ruehrt_die_fachliche_Sichtbarkeit_nicht_an()
    {
        // IsVisible steuert die Sanierungs-Folgefelder ("Sanieren = Nein"). Das ist eine
        // fachliche Regel und darf von der persoenlichen Einstellung nicht ueberschrieben
        // werden - in keine der beiden Richtungen.
        var groups = new[] { Stammdaten("A", "B") };
        groups[0].Items[1].IsVisible = false;

        var result = RecordDetailLayoutApplier.Apply(groups, Layout(
            new[] { ("Stammdaten", new[] { "A", "B" }) },
            "A"));

        Assert.True(result[0].Items.Single(x => x.FieldName == "B").IsHiddenByUser is false);
        Assert.False(result[0].Items.Single(x => x.FieldName == "B").IsVisible);
        Assert.True(result[0].Items.Single(x => x.FieldName == "A").IsHiddenByUser);
        Assert.True(result[0].Items.Single(x => x.FieldName == "A").IsVisible);
    }

    [Fact]
    public void Apply_hebt_ein_frueher_ausgeblendetes_Feld_wieder_auf()
    {
        var groups = new[] { Stammdaten("A", "B") };
        groups[0].Items[0].IsHiddenByUser = true;

        var result = RecordDetailLayoutApplier.Apply(groups, Layout(
            new[] { ("Stammdaten", new[] { "A", "B" }) },
            "B"));

        Assert.False(result[0].Items.Single(x => x.FieldName == "A").IsHiddenByUser);
        Assert.True(result[0].Items.Single(x => x.FieldName == "B").IsHiddenByUser);
    }

    // --- Erfassen -----------------------------------------------------------

    [Fact]
    public void Capture_liest_Spalten_Felder_und_Ausgeblendete()
    {
        var groups = new[] { Stammdaten("A", "B"), Weitere("Z") };
        groups[0].Items[1].IsHiddenByUser = true;

        var layout = RecordDetailLayoutApplier.Capture(groups);

        Assert.Equal(new[] { "Stammdaten", "Weitere" }, layout.Columns.Select(c => c.Title));
        Assert.Equal(new[] { "A", "B" }, layout.Columns[0].Fields);
        Assert.Equal(new[] { "Z" }, layout.Columns[1].Fields);
        Assert.Equal(new[] { "B" }, layout.HiddenFields);
    }

    [Fact]
    public void Capture_und_Apply_ergeben_denselben_Stand()
    {
        var groups = new[] { Weitere("Z"), Stammdaten("C", "A") };
        groups[1].Items[0].IsHiddenByUser = true;

        var layout = RecordDetailLayoutApplier.Capture(groups);

        // Frisch aus dem Builder in anderer Reihenfolge - das Layout muss ihn zurueckholen.
        var frisch = new[] { Stammdaten("A", "C"), Weitere("Z") };
        var result = RecordDetailLayoutApplier.Apply(frisch, layout);

        Assert.Equal(new[] { "Weitere", "Stammdaten" }, Titles(result));
        Assert.Equal(new[] { "C", "A" }, Fields(result[1]));
        Assert.True(result[1].Items.Single(x => x.FieldName == "C").IsHiddenByUser);
    }

    [Fact]
    public void Capture_ueberspringt_Karten_ohne_Feldschluessel()
    {
        var ohneNamen = new RecordDetailItem("Nur Beschriftung", string.Empty, _ => { });
        var groups = new[]
        {
            new RecordDetailGroup("Stammdaten", string.Empty, new[] { Item("A"), ohneNamen }, RecordDetailGroupKind.MasterData)
        };

        var layout = RecordDetailLayoutApplier.Capture(groups);

        Assert.Equal(new[] { "A" }, layout.Columns[0].Fields);
    }

    [Fact]
    public void Apply_veraendert_die_Eingabelisten_nicht()
    {
        var items = new List<RecordDetailItem> { Item("A"), Item("B") };
        var groups = new List<RecordDetailGroup>
        {
            new("Stammdaten", string.Empty, items, RecordDetailGroupKind.MasterData)
        };

        RecordDetailLayoutApplier.Apply(groups, Layout(new[] { ("Stammdaten", new[] { "B", "A" }) }));

        Assert.Equal(new[] { "A", "B" }, items.Select(x => x.FieldName).ToArray());
        Assert.Single(groups);
    }
}

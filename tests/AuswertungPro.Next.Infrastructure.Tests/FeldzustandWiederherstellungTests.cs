using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Die eine Rueckgaengig-Regel fuer Haltungen und Schaechte (B6, Deepscan 02.10.2026): Wert und
/// Herkunftsdaten zeichengenau zurueck, null heisst "gab es nicht", keine neue Handmarke.
/// </summary>
public sealed class FeldzustandWiederherstellungTests
{
    private static readonly DateTime Frueher = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Fact]
    public void Haltung_und_Schacht_stellen_Wert_und_Herkunft_zeichengenau_wieder_her()
    {
        var haltung = new HaltungRecord();
        haltung.SetFieldValue("Material", "Beton", FieldSource.Manual, userEdited: true);
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Material", "Beton", FieldSource.Manual, userEdited: true);

        var alteMeta = new FieldMetadata
        {
            FieldName = "Material",
            Source = FieldSource.Xtf,
            UserEdited = false,
            LastUpdatedUtc = Frueher
        };

        haltung.StelleFeldzustandWiederHer("Material", "Steinzeug", alteMeta);
        schacht.StelleFeldzustandWiederHer("Material", "Steinzeug", alteMeta);

        foreach (var (fields, meta) in new[] { (haltung.Fields, haltung.FieldMeta), (schacht.Fields, schacht.FieldMeta) })
        {
            Assert.Equal("Steinzeug", fields["Material"]);
            Assert.Equal(FieldSource.Xtf, meta["Material"].Source);
            Assert.False(meta["Material"].UserEdited);
            Assert.Equal(Frueher, meta["Material"].LastUpdatedUtc);
            Assert.NotSame(alteMeta, meta["Material"]); // eine Kopie, kein gemerkter Verweis
        }
    }

    [Fact]
    public void Null_entfernt_Feld_und_Metadaten()
    {
        var fields = new Dictionary<string, string> { ["Material"] = "Beton" };
        var meta = new Dictionary<string, FieldMetadata> { ["Material"] = new() { FieldName = "Material" } };

        FeldzustandWiederherstellung.Anwende(fields, meta, "Material", null, null);

        Assert.False(fields.ContainsKey("Material"));
        Assert.False(meta.ContainsKey("Material"));
    }

    [Fact]
    public void Virtuelle_Spalte_wird_abgewiesen_in_beiden_Records()
    {
        Assert.Throws<ArgumentException>(() => new HaltungRecord().StelleFeldzustandWiederHer("Nova_Protokoll", "x", null));
        Assert.Throws<ArgumentException>(() => new SchachtRecord().StelleFeldzustandWiederHer("Nova_Protokoll", "x", null));
    }
}

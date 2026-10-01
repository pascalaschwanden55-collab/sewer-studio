using System;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Die Belastungsklasse einer Schachtabdeckung (EN 124) ist ein eigenes Feld.
/// Vorher gab es dafuer in der Vorlage nur eine Spalte mit der Ueberschrift "0",
/// die ueberall weggefiltert wurde - der Wert liess sich also nirgends erfassen.
/// </summary>
public sealed class SchaechteBelastungsklasseTests
{
    [Theory]
    [InlineData("Belastungsklasse")]
    [InlineData("Belastungs-\nklasse")]
    [InlineData("belastungsklasse")]
    [InlineData("Belastungsklasse Abdeckung")]
    public void Spalte_wird_als_Belastungsklasse_erkannt(string spaltenname)
    {
        Assert.Equal("Belastungsklasse", SchaechteColumnPolicy.ResolveOptionField(spaltenname));
    }

    [Fact]
    public void Zustandsklasse_bleibt_davon_unberuehrt()
    {
        // Beide Namen enden auf "klasse" - die Faerbung der Zustandsklasse darf
        // die Belastungsklasse nicht mitnehmen.
        Assert.False(SchaechteColumnPolicy.IsZustandsklasseColumn("Belastungsklasse"));
        Assert.True(SchaechteColumnPolicy.IsZustandsklasseColumn("Zustandsklasse"));
    }

    [Fact]
    public void Belastungsklasse_bekommt_eine_feste_Auswahlliste()
    {
        Assert.True(GridDropdownFieldPolicy.TryResolve("Belastungsklasse", out var spec));
        Assert.Equal("BelastungsklasseOptions", spec.ItemsSourcePath);
        Assert.False(spec.AllowFreeText);
    }

    [Fact]
    public void Auswahlliste_fuehrt_die_Klassen_nach_EN_124()
    {
        var werte = FieldCatalog.GetComboItems(FieldKeys.LoadClass);

        Assert.Equal(
            new[] { "", "A15", "B125", "C250", "D400", "E600", "F900" },
            werte.ToArray());
    }

    [Fact]
    public void Ausgelieferte_Vorlage_fuehrt_die_Spalte()
    {
        // Schliesst die Kette: Die Spalte muss in der mitgelieferten Vorlage
        // stehen, sonst erscheint sie zwar in der Schachtseite, faellt aber
        // beim Excel-Export wieder heraus.
        var ergebnis = AuswertungPro.Next.Infrastructure.Export.Excel
            .SchaechteTemplateColumnReader.LoadFromExportDirectory(AppContext.BaseDirectory);

        Assert.True(ergebnis.TemplateFound, "Schacht-Vorlage nicht gefunden.");
        Assert.Contains(
            ergebnis.Columns,
            spalte => SchaechteColumnPolicy.ResolveOptionField(spalte) == FieldKeys.LoadClass);
    }

    [Fact]
    public void Leerer_Eintrag_bleibt_moeglich()
    {
        // Ein Schacht ohne erfasste Belastungsklasse muss leer bleiben duerfen;
        // eine erfundene Klasse waere schlimmer als keine Angabe.
        Assert.Contains(string.Empty, FieldCatalog.GetComboItems(FieldKeys.LoadClass));
    }
}

using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Was im Programm waehlbar ist, muss auch in die SIA405-Datei gelangen koennen.
///
/// Ein Wert, den man auswaehlen kann, der beim Export aber lautlos verschwindet, ist
/// eine Falle: Im Programm steht er da, in der Datei fehlt er, und niemand sieht es.
/// Genau das ist am 2026-09-03 mit "GFK" passiert — Pascal hatte das Rohrmaterial
/// bewusst gesetzt, in der erzeugten XTF fehlte das Feld ganz.
///
/// Dieser Waechter prueft jeden einzelnen Eintrag jeder Auswahlliste, die in die Datei
/// fuehrt. Kommt ein neuer Wert dazu, der dort kein Ziel hat, wird er hier rot — und
/// muss entweder einen Normwert bekommen oder namentlich in
/// <see cref="BewussteAusnahmen"/> stehen.
/// </summary>
public sealed class DropdownExportierbarkeitTests
{
    /// <summary>
    /// Die Felder, deren Auswahl in die SIA405-Datei geht, mit ihrem Ziel dort.
    /// </summary>
    public static TheoryData<string, string> Felder() => new()
    {
        { FieldKeys.PipeMaterial, "Material" },
        { FieldKeys.UsageType, "Nutzungsart_Ist" },
        { FieldKeys.ConditionClass, "BaulicherZustand" },
        { FieldKeys.HierarchicalFunction, "FunktionHierarchisch" },
        { FieldKeys.HydraulicFunction, "FunktionHydraulisch" },
        { FieldKeys.ConnectionType, "Verbindungsart" },
        { FieldKeys.BeddingEncasement, "Bettung_Umhuellung" },
        { FieldKeys.ProfileType, "Profiltyp" },
        { FieldKeys.OperatingStatus, "Status" },
        { FieldKeys.RehabilitationNeed, "Sanierungsbedarf" },
        { FieldKeys.PositionAccuracy, "Lagebestimmung" }
    };

    /// <summary>
    /// Werte, die bewusst waehlbar bleiben, obwohl SIA405 kein Gegenstueck kennt.
    ///
    /// Beide fuehrt das WebGIS von Uri in seiner Materialliste, und beide haben dort
    /// keinen NORM_CODE: "GFK" als Kunststoffart (Code 1001), "Guss" als Gruppe ueber
    /// duktil und Grauguss. Ein Gruppenbegriff sagt nicht, welche Art gemeint ist, und
    /// zu raten waere schlimmer als zu schweigen.
    ///
    /// Waehlbar heisst hier nicht stillschweigend verloren: Der Export nennt solche
    /// Werte namentlich im Bericht.
    ///
    /// Ein neuer Eintrag braucht denselben Beleg: Der Kataster fuehrt den Begriff UND
    /// die Norm kennt ihn wirklich nicht.
    /// </summary>
    private static readonly HashSet<string> BewussteAusnahmen =
        new(StringComparer.Ordinal) { "GFK", "Guss" };

    [Theory]
    [MemberData(nameof(Felder))]
    public void Jeder_waehlbare_Wert_findet_ein_Ziel_in_der_Datei(string feld, string xtfName)
    {
        var werte = FieldCatalog.GetComboItems(feld);
        Assert.NotEmpty(werte);

        var ohneZiel = werte
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Where(w => !BewussteAusnahmen.Contains(w))
            .Where(w => string.IsNullOrEmpty(
                XtfStammdatenPlanBuilder.NachXtfWert(xtfName, w, "SIA405_ABWASSER_2020_LV95")))
            .ToList();

        Assert.True(
            ohneZiel.Count == 0,
            $"{feld} bietet Werte an, die beim Export in \"{xtfName}\" verloren gehen: " +
            string.Join(", ", ohneZiel) +
            ". Entweder einen Normwert ergaenzen oder den Wert aus der Auswahl nehmen.");
    }

    [Fact]
    public void Die_Zustandsklasse_kennt_nur_Z0_bis_Z4()
    {
        // In SIA405 gibt es keine Klasse 5. Bis 2026-09-03 stand sie in der Auswahl.
        var werte = FieldCatalog.GetComboItems(FieldKeys.ConditionClass);

        Assert.Equal(["", "0", "1", "2", "3", "4"], werte);
    }

    [Fact]
    public void GFK_bleibt_waehlbar_weil_das_WebGIS_ihn_fuehrt()
    {
        // Im WebGIS von Uri steht GFK unter der Materialgruppe "Kunststoff".
        Assert.Contains("GFK", FieldCatalog.GetComboItems(FieldKeys.PipeMaterial));
        Assert.Equal("GFK", MaterialVokabular.Normalisieren("gfk"));
        Assert.Equal("GFK", MaterialVokabular.Normalisieren("Glasfaser"));
    }

    [Fact]
    public void Ein_Ausnahmewert_wird_beim_Export_gemeldet_statt_verschluckt()
    {
        // Waehlbar ohne Normwert ist nur vertretbar, solange der Bericht es sagt.
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, "80401-80409", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.PipeMaterial, "GFK", FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([record], []);

        Assert.Contains(plan.Hinweise, h =>
            h.Contains("Material", StringComparison.Ordinal)
            && h.Contains("GFK", StringComparison.Ordinal));
    }

    [Fact]
    public void Die_Ausnahmeliste_bleibt_klein_und_begruendet()
    {
        // Ein Waechter ueber dem Waechter: Waechst diese Liste unbemerkt, ist die
        // eigentliche Regel ausgehoehlt.
        Assert.Equal(2, BewussteAusnahmen.Count);
    }
}

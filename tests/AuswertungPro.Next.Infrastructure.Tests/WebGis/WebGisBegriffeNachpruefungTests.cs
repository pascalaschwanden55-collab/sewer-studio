using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Befunde der Schlusspruefung Schritt A (23.09.2026): Importe schrieben weiter Normbegriffe
/// (erst beim Speichern angehoben -> Scheinkonflikte), das Holen gab einem Spezialbauwerk die
/// Normschacht-Funktion, und die Rundreise Senden/Holen war nicht abgesichert.
/// </summary>
public sealed class WebGisBegriffeNachpruefungTests
{
    // ---------------- Befund 2: jeder Schreibvorgang speichert den WebGIS-Begriff ----------------

    [Fact]
    public void Import_in_eine_haltung_schreibt_sofort_den_webgis_begriff()
    {
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.OperatingStatus, "in_Betrieb", FieldSource.Xtf, false);
        h.SetFieldValue(FieldKeys.UsageType, "Niederschlagsabwasser", FieldSource.Pdf, false);
        h.SetFieldValue(FieldKeys.ProfileType, "Kreisprofil", FieldSource.Xtf, false);
        h.SetFieldValue(FieldKeys.OperatingStatus + "_x", "in_Betrieb", FieldSource.Xtf, false);

        Assert.Equal("In Betrieb", h.GetFieldValue(FieldKeys.OperatingStatus));
        Assert.Equal("Regenabwasser", h.GetFieldValue(FieldKeys.UsageType));
        Assert.Equal("Kreisprofil (K)", h.GetFieldValue(FieldKeys.ProfileType));
        Assert.Equal("in_Betrieb", h.GetFieldValue(FieldKeys.OperatingStatus + "_x")); // fremdes Feld unberuehrt
        Assert.Equal(FieldSource.Xtf, h.FieldMeta[FieldKeys.OperatingStatus].Source);   // Herkunft bleibt
    }

    [Fact]
    public void Gleichbedeutender_import_ist_kein_katasterkonflikt()
    {
        var s = new SchachtRecord();
        s.SetFieldValue(FieldKeys.UsageType, "Regenabwasser", FieldSource.Kataster, false);

        s.SetFieldValue(FieldKeys.UsageType, "Niederschlagsabwasser", FieldSource.Pdf, false);

        Assert.Null(s.FieldMeta[FieldKeys.UsageType].Conflict);
        Assert.Equal("Regenabwasser", s.GetFieldValue(FieldKeys.UsageType));
    }

    [Fact]
    public void Schachtfunktion_wird_nur_beim_normschacht_zum_webgis_begriff()
    {
        var norm = new SchachtRecord();
        norm.SetFieldValue("Funktion", "Pumpwerk", FieldSource.Pdf, false);
        var spezial = new SchachtRecord();
        spezial.SetFieldValue(FieldKeys.ShaftStructureType, "Spezialbauwerk", FieldSource.Xtf, false);
        spezial.SetFieldValue("Funktion", "Pumpwerk", FieldSource.Xtf, false);

        Assert.Equal("Pumpenschacht", norm.GetFieldValue("Funktion"));
        Assert.Equal("Pumpwerk", spezial.GetFieldValue("Funktion"));
    }

    // ---------------- Befund 3: Holen gibt einem Spezialbauwerk keine Normschacht-Funktion ----------------

    private const string SchachtFunktionRef = "0aec3f59-388c-a7e7-e6f5-a0b50b81263a";

    [Fact]
    public void Holen_setzt_die_funktion_nur_beim_normschacht()
    {
        var stand = new WebGisLesestand { GlobalId = "G1", Bezeichnung = "80409" };
        stand.Felder[SchachtFunktionRef] = "173";
        stand.Kataloge[SchachtFunktionRef] = new List<(string, string)> { ("173", "Pumpenschacht") };
        WebGisImportEingabe Eingabe(bool normschacht) => new()
        {
            Objektart = WebGisObjektart.Schacht, Bezeichnung = "80409", GespeicherteGlobalId = "G1",
            Normschacht = normschacht,
            Felder = { ["Funktion"] = new WebGisImportFeld("Pumpwerk", Ersetzbar: true) },
        };

        var spezial = WebGisImportPlanBuilder.Baue(Eingabe(false), stand);
        var norm = WebGisImportPlanBuilder.Baue(Eingabe(true), stand);

        Assert.DoesNotContain(spezial.Aenderungen, a => a.Feld == "Funktion");
        Assert.Contains(spezial.Hinweise, h => h.Contains("kein Normschacht"));
        Assert.Contains(norm.Aenderungen, a => a.Feld == "Funktion" && a.Neu == "Pumpenschacht");
    }

    // ---------------- Befund 4: Rundreise Senden -> Holen je gesendetem Feld ----------------

    public static TheoryData<WebGisObjektart, string, string> GesendeteFelder() => new()
    {
        { WebGisObjektart.Haltung, FieldKeys.OperatingStatus, "haltung-C04" },
        { WebGisObjektart.Haltung, FieldKeys.PositionAccuracy, "haltung-C13" },
        { WebGisObjektart.Haltung, FieldKeys.HydraulicFunction, "haltung-C03" },
        { WebGisObjektart.Haltung, FieldKeys.ConnectionType, "haltung-C18" },
        { WebGisObjektart.Haltung, FieldKeys.ProfileType, "haltung-C07" },
        { WebGisObjektart.Haltung, FieldKeys.UsageType, "haltung-C01" },
        { WebGisObjektart.Schacht, FieldKeys.OperatingStatus, "schacht-C05" },
        { WebGisObjektart.Schacht, FieldKeys.UsageType, "schacht-C02" },
        { WebGisObjektart.Schacht, FieldKeys.PositionAccuracy, "schacht-C11" },
        { WebGisObjektart.Schacht, "Funktion", "schacht-C00" },
    };

    /// <summary>
    /// Gespeicherter WebGIS-Begriff -> Senden findet genau seinen Schluessel -> das WebGIS zeigt dessen
    /// Beschriftung -> Holen speichert wieder denselben Text. «Unbekannt» ist ausgenommen (das Holen
    /// fuellt damit nichts, Entscheid 23.09.2026).
    /// </summary>
    [Theory]
    [MemberData(nameof(GesendeteFelder))]
    public void Rundreise_senden_und_holen_aendert_nichts(WebGisObjektart art, string feld, string katalogId)
    {
        var eintraege = FieldCatalog.Objektfelder.Auswahl(katalogId)!.Eintraege
            .Where(e => !e.Eigen && !string.IsNullOrWhiteSpace(e.Label) && !string.IsNullOrEmpty(e.OriginalCode)).ToList();
        var maske = eintraege.Select(e => (e.OriginalCode!, e.Label)).ToList();
        Assert.NotNull(WebGisHandwertKarte.Finde(art, feld));

        foreach (var e in eintraege.Where(e => WebGisBegriffe.Falte(e.Label) != "unbekannt"))
        {
            Assert.Equal(e.OriginalCode, WebGisHandwertKarte.Schluessel(maske, e.Label));
            Assert.Equal(e.Label, WebGisImportWert.Zuordne(art, feld, e.Label, out var hinweis));
            Assert.Null(hinweis);
        }
    }
}

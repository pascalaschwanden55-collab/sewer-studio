using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Der SIA405-Export (und ueber ihn DSS, VSA, Farben) muss die neu gespeicherten WebGIS-Begriffe
/// verstehen. Ohne Norm sind nur die hier namentlich genannten, reinen WebGIS-Werte.
/// </summary>
public sealed class WebGisBegriffeNormTests
{
    private const string Modell = "SIA405_ABWASSER_2020_LV95";

    public static TheoryData<string, string, string[]> Felder() => new()
    {
        { FieldKeys.OperatingStatus, "Status", [] },
        { FieldKeys.PositionAccuracy, "Lagebestimmung", [] },
        { FieldKeys.HydraulicFunction, "FunktionHydraulisch",
            ["Belagsrinne Wasserschale", "Entwaesserungsgraben befestigt", "Entwaesserungsgraben unbefestigt", "Schlitzrinne", "Wasserrinne mit Rost"] },
        { FieldKeys.ConnectionType, "Verbindungsart",
            ["Stumpfschweissmuffe", "Führungsbolzen", "Manschette einbetoniert", "Schweissmuffen", "Stahlmuffen"] },
        { FieldKeys.BeddingEncasement, "Bettung_Umhuellung", ["Kies", "In Kulisse", "Pressvortrieb"] },
        { FieldKeys.RehabilitationNeed, "Sanierungsbedarf", ["Saniert"] },
        { FieldKeys.UsageType, "Nutzungsart_Ist", ["Bergwasser", "Strassenabwasser"] },
        { FieldKeys.ProfileType, "Profiltyp", [] },
    };

    [Theory]
    [MemberData(nameof(Felder))]
    public void Jeder_webgis_begriff_mit_norm_wird_exportiert(string feld, string xtfName, string[] ohneNorm)
    {
        var fehlend = WebGisBegriffe.Fuer(false, feld)!.Werte
            .Where(w => string.IsNullOrEmpty(XtfStammdatenPlanBuilder.NachXtfWert(xtfName, w, Modell)))
            .ToList();
        Assert.Equal(ohneNorm.OrderBy(x => x, StringComparer.Ordinal), fehlend.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("Tot/Aufgehoben, verfüllt", "tot")]
    [InlineData("In Betrieb", "in_Betrieb")]
    public void Status_aus_dem_webgis_wird_zum_normwert(string webgis, string norm)
        => Assert.Equal(norm, SiaKanalVokabular.Status.NachNorm(webgis));

    [Theory]
    [InlineData("Dükerleitung", "Duekerleitung")]
    [InlineData("Überschiebmuffen", "Ueberschiebmuffen")]
    [InlineData("In Kanal aufgehängt", "in_Kanal_aufgehaengt")]
    public void Umlaute_der_webgis_begriffe_werden_fuer_die_norm_umgeschrieben(string webgis, string norm)
    {
        var treffer = new[] { SiaKanalVokabular.FunktionHydraulisch, SiaKanalVokabular.Verbindungsart, SiaKanalVokabular.BettungUmhuellung }
            .Select(l => l.NachNorm(webgis)).FirstOrDefault(n => n is not null);
        Assert.Equal(norm, treffer);
    }

    [Theory]
    [InlineData("Maulprofil (E)", "Maulprofil")]
    [InlineData("Andere (A)", "Spezialprofil")]
    public void Profil_aus_dem_webgis_hat_eine_norm(string webgis, string norm)
        => Assert.Equal(norm, ProfiltypVokabular.NachNorm(webgis));

    [Fact]
    public void Bachabwasser_zaehlt_wie_bachwasser()
        => Assert.Equal("Bachwasser", NutzungsartVokabular.Normalisieren("Bachabwasser"));
}

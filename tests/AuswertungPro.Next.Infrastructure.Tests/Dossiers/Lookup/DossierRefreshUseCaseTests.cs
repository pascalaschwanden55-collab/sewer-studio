using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

/// <summary>
/// Prueft das Nachfuehren eines bestehenden Dossiers.
///
/// Der Anlass: die Schaechte einer Liegenschaft werden oft erst nach dem
/// Anlegen des Dossiers erfasst. Das Nachfuehren soll genau diese Luecke
/// schliessen — und dabei nichts anfassen, was von Hand geaendert wurde.
/// </summary>
public sealed class DossierRefreshUseCaseTests
{
    private static readonly Guid IdA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid IdB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Dictionary<string, Guid> Projekt()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["439.01-36051"] = IdA,
            ["439.03-36052"] = IdB
        };

    private static DossierDefinition Dossier()
    {
        var d = new DossierDefinition { ParcelNumbers = "439" };
        d.HoldingIds.Add(IdA);
        d.ShaftNumbers.Add("439.01");
        d.ShaftNumbers.Add("36051");
        return d;
    }

    [Fact]
    public void Ein_spaeter_erfasster_Schacht_wird_gefunden()
    {
        // Genau der Fall: das Dossier steht, der Schacht kommt danach dazu.
        var vorschlag = DossierRefreshUseCase.Propose(
            Dossier(),
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["439.01-36051"] = IdA },
            new[] { "439.01", "36051", "439.02" });

        Assert.Equal(new[] { "439.02" }, vorschlag.NewShafts);
        Assert.Empty(vorschlag.NewHoldings);
    }

    [Fact]
    public void Eine_spaeter_erfasste_Leitung_bringt_ihre_Schaechte_gleich_mit()
    {
        // Sonst muesste man zweimal nachfuehren, um beides zu bekommen.
        var vorschlag = DossierRefreshUseCase.Propose(
            Dossier(), Projekt(), new[] { "439.01", "36051", "439.03", "36052" });

        Assert.Equal("439.03-36052", Assert.Single(vorschlag.NewHoldings).Designation);
        Assert.Equal(new[] { "439.03", "36052" }, vorschlag.NewShafts);
    }

    [Fact]
    public void Was_schon_im_Dossier_steht_wird_nicht_erneut_angeboten()
    {
        var vorschlag = DossierRefreshUseCase.Propose(
            Dossier(),
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["439.01-36051"] = IdA },
            new[] { "439.01", "36051" });

        Assert.False(vorschlag.HasAnything);
    }

    [Fact]
    public void Ein_abgelehnter_Schacht_kommt_nie_wieder()
    {
        var dossier = Dossier();
        dossier.DismissedShaftNumbers.Add("439.02");

        var vorschlag = DossierRefreshUseCase.Propose(
            dossier,
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["439.01-36051"] = IdA },
            new[] { "439.01", "36051", "439.02" });

        Assert.Empty(vorschlag.NewShafts);
    }

    [Fact]
    public void Eine_abgelehnte_Leitung_kommt_nie_wieder()
    {
        var dossier = Dossier();
        dossier.DismissedHoldingIds.Add(IdB);

        var vorschlag = DossierRefreshUseCase.Propose(
            dossier, Projekt(), new[] { "439.01", "36051" });

        Assert.Empty(vorschlag.NewHoldings);
    }

    [Fact]
    public void Abhaken_merkt_die_Ablehnung_fuer_das_naechste_Mal()
    {
        var dossier = Dossier();
        var vorschlag = DossierRefreshUseCase.Propose(
            dossier, Projekt(), new[] { "439.01", "36051", "439.03", "36052" });

        // Der Mensch nimmt die Leitung, aber nur einen der beiden Schaechte.
        DossierRefreshUseCase.Apply(
            dossier,
            vorschlag.NewHoldings,
            new[] { "439.03" },
            vorschlag);

        Assert.Contains(IdB, dossier.HoldingIds);
        Assert.Contains("439.03", dossier.ShaftNumbers);

        Assert.Equal(new[] { "36052" }, dossier.DismissedShaftNumbers);

        // Und beim naechsten Lauf ist er wirklich weg.
        var zweiter = DossierRefreshUseCase.Propose(
            dossier, Projekt(), new[] { "439.01", "36051", "439.03", "36052" });

        Assert.False(zweiter.HasAnything);
    }

    [Fact]
    public void Nachfuehren_entfernt_nichts()
    {
        // Auch nicht, wenn die Leitung inzwischen aus dem Projekt verschwunden
        // ist: ein Dossier, das der Empfaenger schon hat, soll sich nicht
        // hinter seinem Ruecken leeren.
        var dossier = Dossier();
        var vorher = dossier.HoldingIds.ToList();
        var schaechteVorher = dossier.ShaftNumbers.ToList();

        var vorschlag = DossierRefreshUseCase.Propose(
            dossier,
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        DossierRefreshUseCase.Apply(
            dossier, Array.Empty<RefreshableHolding>(), Array.Empty<string>(), vorschlag);

        Assert.Equal(vorher, dossier.HoldingIds);
        Assert.Equal(schaechteVorher, dossier.ShaftNumbers);
    }

    [Fact]
    public void Texte_und_Themen_bleiben_unberuehrt()
    {
        var dossier = Dossier();
        dossier.Topics.Add(new DossierTopicRow { Title = "Schäden", Text = "Leitung undicht" });
        dossier.OwnerName = "Martin Muster";
        dossier.OverviewPlanPath = @"C:\Plan\uebersicht.png";

        var vorschlag = DossierRefreshUseCase.Propose(
            dossier, Projekt(), new[] { "439.03", "36052" });

        DossierRefreshUseCase.Apply(
            dossier, vorschlag.NewHoldings, vorschlag.NewShafts, vorschlag);

        Assert.Equal("Leitung undicht", Assert.Single(dossier.Topics).Text);
        Assert.Equal("Martin Muster", dossier.OwnerName);
        Assert.Equal(@"C:\Plan\uebersicht.png", dossier.OverviewPlanPath);
    }

    [Fact]
    public void Ohne_Parzellennummer_wird_nichts_vorgeschlagen()
    {
        // Ohne Parzelle gibt es keine Regel, die etwas zuordnen koennte.
        // Dann lieber nichts anbieten als irgendetwas.
        var dossier = new DossierDefinition();

        var vorschlag = DossierRefreshUseCase.Propose(
            dossier, Projekt(), new[] { "439.01", "439.02" });

        Assert.False(vorschlag.HasAnything);
    }

    [Fact]
    public void Mehrere_Parzellen_im_selben_Feld_werden_alle_geprueft()
    {
        var dossier = new DossierDefinition { ParcelNumbers = "439, 512" };

        var vorschlag = DossierRefreshUseCase.Propose(
            dossier,
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase),
            new[] { "439.01", "512.01", "700.01" });

        Assert.Equal(new[] { "439.01", "512.01" }, vorschlag.NewShafts);
    }

    [Fact]
    public void Ein_fremder_Schacht_wird_nicht_angeboten()
    {
        var vorschlag = DossierRefreshUseCase.Propose(
            Dossier(),
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["439.01-36051"] = IdA },
            new[] { "439.01", "36051", "512.01" });

        Assert.Empty(vorschlag.NewShafts);
    }
}

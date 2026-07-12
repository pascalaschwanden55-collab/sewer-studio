using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.KnowledgeBase;

/// <summary>
/// Tests fuer den KnowledgeRootGuard (AP-06): erkennt beim Start, ob die App
/// unbemerkt mit einer anderen/leeren Wissensdatenbank laeuft.
/// </summary>
public sealed class KnowledgeRootGuardTests
{
    private const string Root = @"C:\KI_BRAIN";

    [Fact]
    public void Erststart_OhneGemerktenRoot_KeineWarnung()
    {
        var r = KnowledgeRootGuard.Evaluate(
            currentRoot: Root, lastKnownRoot: null,
            dbExisted: false, currentSampleCount: 0, lastKnownSampleCount: null);

        Assert.False(r.HatWarnung);
        Assert.Equal(KnowledgeRootWarnungArt.Keine, r.Art);
    }

    [Fact]
    public void GleicherRoot_DbVorhanden_KeineWarnung()
    {
        var r = KnowledgeRootGuard.Evaluate(
            currentRoot: Root, lastKnownRoot: Root,
            dbExisted: true, currentSampleCount: 21000, lastKnownSampleCount: 21000);

        Assert.False(r.HatWarnung);
    }

    [Fact]
    public void RootGewechselt_MeldetSplitBrainWarnung()
    {
        var r = KnowledgeRootGuard.Evaluate(
            currentRoot: @"C:\Users\Besitzer\AppData\Local\SewerStudio\Knowledge",
            lastKnownRoot: Root,
            dbExisted: true, currentSampleCount: 30000, lastKnownSampleCount: 21000);

        Assert.True(r.HatWarnung);
        Assert.Equal(KnowledgeRootWarnungArt.RootGewechselt, r.Art);
        Assert.Contains(Root, r.Meldung);
    }

    [Fact]
    public void RootMitAbschliessendemTrenner_GiltAlsGleich()
    {
        var r = KnowledgeRootGuard.Evaluate(
            currentRoot: @"C:\KI_BRAIN\", lastKnownRoot: @"C:\KI_BRAIN",
            dbExisted: true, currentSampleCount: 21000, lastKnownSampleCount: 21000);

        Assert.False(r.HatWarnung);
    }

    [Fact]
    public void GleicherRoot_DbWeg_MeldetLeereOderNeueDb()
    {
        var r = KnowledgeRootGuard.Evaluate(
            currentRoot: Root, lastKnownRoot: Root,
            dbExisted: false, currentSampleCount: 0, lastKnownSampleCount: 21000);

        Assert.True(r.HatWarnung);
        Assert.Equal(KnowledgeRootWarnungArt.LeereOderNeueDb, r.Art);
    }

    [Fact]
    public void SampleEinbruchUeber90Prozent_MeldetDatenverlust()
    {
        var r = KnowledgeRootGuard.Evaluate(
            currentRoot: Root, lastKnownRoot: Root,
            dbExisted: true, currentSampleCount: 5, lastKnownSampleCount: 21000);

        Assert.True(r.HatWarnung);
        Assert.Equal(KnowledgeRootWarnungArt.SampleEinbruch, r.Art);
    }

    [Fact]
    public void LeichterSampleRueckgang_KeineWarnung()
    {
        var r = KnowledgeRootGuard.Evaluate(
            currentRoot: Root, lastKnownRoot: Root,
            dbExisted: true, currentSampleCount: 20500, lastKnownSampleCount: 21000);

        Assert.False(r.HatWarnung);
    }

    [Fact]
    public void SampleEinbruch_AberVorherNurWenige_KeineWarnung()
    {
        // Kleiner Bestand (unter Relevanzschwelle) -> kein Fehlalarm beim Aufbau der KB.
        var r = KnowledgeRootGuard.Evaluate(
            currentRoot: Root, lastKnownRoot: Root,
            dbExisted: true, currentSampleCount: 0, lastKnownSampleCount: 10);

        Assert.False(r.HatWarnung);
    }
}

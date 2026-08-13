using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Abnahme am echten Kundenbestand: Kommt der Videozaehlerstand aus einer
/// gelieferten VSA-KEK-XTF wirklich bis zum Protokolleintrag?
///
/// Vor der Reparatur vom 2026-08-13 war die Zeit dort immer null — der Parser
/// hat das Feld nie eingelesen. Die Kette dahinter stand vollstaendig.
///
/// Maschinengebunden: ohne die Datei wird uebersprungen. Sie wird ausschliesslich
/// gelesen.
/// </summary>
public sealed class XtfVideoCounterLiveAcceptanceTests
{
    internal const string XtfPfad =
        @"D:\Videoprojekte\2.26.161 Wassen UR_KF_KR 2026\2.26.161 Wassen UR_KF_KR 2026_INTERLIS_2020.xtf";

    [KundenbestandFact]
    [Trait("Category", "Integration")]
    public void Rohranfang_und_Rohrende_tragen_ihre_videozeit()
    {
        var project = new Project();
        new LegacyXtfImportService().ImportXtfFiles([XtfPfad], project);

        var eintraege = project.Data
            .SelectMany(h => h.Protocol?.Current?.Entries ?? [])
            .ToList();
        Assert.NotEmpty(eintraege);

        var anfang = eintraege.Where(e => Ist(e, "BCD")).ToList();
        var ende = eintraege.Where(e => Ist(e, "BCE")).ToList();
        Assert.NotEmpty(anfang);
        Assert.NotEmpty(ende);

        Assert.All(anfang, e => Assert.NotNull(e.Zeit));
        Assert.All(ende, e => Assert.NotNull(e.Zeit));

        // "00:06:57:00" sind 6 Minuten 57, nicht 6 Stunden 57. .NET liest den
        // vierteiligen Wert von sich aus als d:hh:mm:ss.
        Assert.All(ende, e => Assert.True(
            e.Zeit!.Value < TimeSpan.FromHours(2),
            $"Unplausible Videozeit {e.Zeit} — als Tage gelesen?"));
    }

    private static bool Ist(ProtocolEntry entry, string code)
        => (entry.Code ?? "").Trim().StartsWith(code, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Laeuft nur, wenn der Kundenbestand auf diesem Rechner liegt. Gleiches Muster
/// wie <c>MachineIntegrationFactAttribute</c> im Pipeline-Testprojekt.
/// </summary>
public sealed class KundenbestandFactAttribute : FactAttribute
{
    public KundenbestandFactAttribute()
    {
        if (!File.Exists(XtfVideoCounterLiveAcceptanceTests.XtfPfad))
            Skip = $"Kundenbestand nicht vorhanden: {XtfVideoCounterLiveAcceptanceTests.XtfPfad}";
    }
}

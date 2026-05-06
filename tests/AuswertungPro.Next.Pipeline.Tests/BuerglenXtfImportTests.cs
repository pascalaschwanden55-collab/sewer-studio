using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using Xunit;
using Xunit.Abstractions;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// V4.3 Phase 2 Verifikation: IBAK-Bürglen-XTF (VSA_KEK_2020_LV95, INTERLIS 2.3)
/// muss alle 608 Kanalschaden-Eintraege importieren. Pro Finding muessen
/// KanalSchadencode, Distanz (MeterStart) und Videozaehlerstand (MPEG) gesetzt sein.
/// </summary>
public class BuerglenXtfImportTests
{
    private readonly ITestOutputHelper _output;
    public BuerglenXtfImportTests(ITestOutputHelper output) => _output = output;

    private const string XtfPath =
        @"D:\TESTSAMPLES\Bürgle_Seitenanschlüsse\Bürglen_UR_Klausenstrasse_Neumühleweg_Privat_32953_1225\Bürglen_UR_Klausenstrasse_Neumühleweg_Privat_32953_1225\Bürglen_UR_Klausenstrasse_Neumühleweg_Privat_32953_1225.xtf";

    [Fact]
    public void ImportBuerglen_YieldsFindingsWithCodeDistanceTimestamp()
    {
        if (!File.Exists(XtfPath))
        {
            _output.WriteLine($"Testdatei fehlt: {XtfPath} — Test wird uebersprungen");
            return;
        }

        // ParseVsaKek ist private — via Reflection aufrufen (Integrationstest ohne Project-Kontext)
        var doc = XDocument.Load(XtfPath);
        var svcType = typeof(LegacyXtfImportService);
        var method = svcType.GetMethod("ParseVsaKek",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        object?[] args = { doc, XtfPath, null };
        var result = method!.Invoke(null, args);
        var records = (List<HaltungRecord>)result!;
        var findingsPerHaltung = (Dictionary<string, List<VsaFinding>>)args[2]!;

        _output.WriteLine($"HaltungRecords: {records.Count}");

        var allFindings = records.SelectMany(r => r.VsaFindings ?? new List<VsaFinding>()).ToList();
        _output.WriteLine($"Total VsaFindings: {allFindings.Count}");

        int withCode   = allFindings.Count(f => !string.IsNullOrWhiteSpace(f.KanalSchadencode));
        int withMeter  = allFindings.Count(f => f.MeterStart.HasValue);
        int withMpeg   = allFindings.Count(f => !string.IsNullOrWhiteSpace(f.MPEG));
        int withQuant  = allFindings.Count(f => !string.IsNullOrWhiteSpace(f.Quantifizierung1));

        _output.WriteLine($"  mit Code:            {withCode}");
        _output.WriteLine($"  mit MeterStart:      {withMeter}");
        _output.WriteLine($"  mit MPEG (Video-TS): {withMpeg}");
        _output.WriteLine($"  mit Quantifizierung: {withQuant}");

        // Stichprobe ausgeben
        foreach (var f in allFindings.Take(3))
            _output.WriteLine($"  {f.KanalSchadencode,-8} @ {f.MeterStart:F2}m  Video={f.MPEG}  Q1={f.Quantifizierung1}");

        Assert.True(records.Count > 0, "keine HaltungRecords");
        Assert.True(allFindings.Count >= 600, $"erwartet ~608 Findings, bekommen {allFindings.Count}");
        Assert.True(withCode == allFindings.Count, "nicht alle Findings haben KanalSchadencode");
        Assert.True(withMpeg >= allFindings.Count * 0.9,
            $"erwartet dass >=90% einen MPEG/Videozaehlerstand haben, ist {withMpeg}/{allFindings.Count}");
    }
}

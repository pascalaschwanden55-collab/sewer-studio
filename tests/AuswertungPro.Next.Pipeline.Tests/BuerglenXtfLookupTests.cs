using System.IO;
using System.Text.RegularExpressions;
using AuswertungPro.Next.UI.Services.CodeCatalog;
using Xunit;
using Xunit.Abstractions;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// V4.3 Phase 1 Verifikation: Alle 53 VSA-Codes aus der Bürglen-XTF (IBAK IKAS, VSA_KEK_2020)
/// müssen vom VsaCodeTree.LookupLabel aufgelöst werden können — sonst hat die UI
/// "unbekannte Codes" in der KB.
/// </summary>
public class BuerglenXtfLookupTests
{
    private readonly ITestOutputHelper _output;
    public BuerglenXtfLookupTests(ITestOutputHelper output) => _output = output;

    private const string XtfPath =
        @"D:\TESTSAMPLES\Bürgle_Seitenanschlüsse\Bürglen_UR_Klausenstrasse_Neumühleweg_Privat_32953_1225\Bürglen_UR_Klausenstrasse_Neumühleweg_Privat_32953_1225\Bürglen_UR_Klausenstrasse_Neumühleweg_Privat_32953_1225.xtf";

    [Fact]
    public void AllBuerglenCodes_MustResolveInVsaCodeTree()
    {
        if (!File.Exists(XtfPath))
        {
            _output.WriteLine($"Testdatei fehlt: {XtfPath} — Test wird uebersprungen");
            return;
        }

        var xtf = File.ReadAllText(XtfPath);
        var codes = new System.Collections.Generic.HashSet<string>();
        foreach (Match m in Regex.Matches(xtf, @"<KanalSchadencode>([^<]+)</KanalSchadencode>"))
            codes.Add(m.Groups[1].Value);

        _output.WriteLine($"Bürglen XTF enthält {codes.Count} verschiedene Codes");

        var unresolved = new System.Collections.Generic.List<string>();
        foreach (var code in codes)
        {
            var label = VsaCodeTree.LookupLabel(code);
            if (label is null) unresolved.Add(code);
        }

        _output.WriteLine($"Aufgeloest: {codes.Count - unresolved.Count} / {codes.Count}");
        foreach (var u in unresolved)
            _output.WriteLine($"  FEHLT: {u}");

        Assert.Empty(unresolved);
    }
}

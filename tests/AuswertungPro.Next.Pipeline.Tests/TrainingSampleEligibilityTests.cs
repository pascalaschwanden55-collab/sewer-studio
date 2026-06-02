using System;
using System.Globalization;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingSampleEligibilityTests
{
    [Theory]
    [InlineData("31.12.2021", false, TrainingSampleEligibility.LegacyBeforeCutoffReason)]
    [InlineData("01.01.2022", true, null)]
    [InlineData("2022-01-01", true, null)]
    [InlineData("Aufnahmen: 04.12.14 - 05.12.14", false, TrainingSampleEligibility.LegacyBeforeCutoffReason)]
    [InlineData("GEP Aufnahmen Altdorf 2025", true, null)]
    public void Evaluate_nutzt_2022_als_harten_Stichtag(string rawDate, bool expectedEligible, string? expectedReason)
    {
        var parsed = TrainingSampleEligibility.TryParseInspectionDate(rawDate);

        Assert.NotNull(parsed);
        var result = TrainingSampleEligibility.Evaluate(parsed);

        Assert.Equal(expectedEligible, result.IsEligible);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Fact]
    public void Evaluate_sperrt_unbekanntes_Datum()
    {
        var result = TrainingSampleEligibility.Evaluate((DateTime?)null);

        Assert.False(result.IsEligible);
        Assert.Equal(TrainingSampleEligibility.MissingInspectionDateReason, result.Reason);
    }

    [Fact]
    public void Evaluate_mit_Katalog_sperrt_unbekannte_und_nicht_klickbare_Codes()
    {
        var catalog = new InMemoryCodeCatalogProvider(
        [
            new CodeDefinition { Code = "BBAA", IsSelectable = true },
            new CodeDefinition { Code = "BCCYY", IsSelectable = false, IsObservedExtension = true }
        ]);

        var valid = MakeSample("BBAA");
        var unknown = MakeSample("BZZZ");
        var observed = MakeSample("BCCYY");

        Assert.True(TrainingSampleEligibility.Evaluate(valid, catalog).IsEligible);

        var unknownResult = TrainingSampleEligibility.Evaluate(unknown, catalog);
        Assert.False(unknownResult.IsEligible);
        Assert.Equal(TrainingSampleEligibility.InvalidCatalogCodeReason, unknownResult.Reason);

        var observedResult = TrainingSampleEligibility.Evaluate(observed, catalog);
        Assert.False(observedResult.IsEligible);
        Assert.Equal(TrainingSampleEligibility.InvalidCatalogCodeReason, observedResult.Reason);
    }

    [Theory]
    // Fix: yyyyMMdd-Praefix im Datei-/Ordnernamen wird als Inspektionsdatum erkannt.
    [InlineData("20251110_9866-9327.pdf", "2025-11-10")]   // kanonischer Protokollname
    [InlineData("20251110_9866-9327.mpg", "2025-11-10")]   // kanonischer Videoname
    [InlineData("9866-9327_20251110", "2025-11-10")]       // Datum nach der Projekt-ID
    [InlineData("20251110.pdf", "2025-11-10")]             // Datum direkt vor der Extension
    [InlineData("20230915_2835-2828_BAFZA_17.9m_1.png", "2023-09-15")] // realer self_training_frames-Name
    [InlineData(@"D:\Haltungen\20251110_9866-9327", "2025-11-10")]      // ganzer Pfad
    public void TryParseInspectionDate_erkennt_eingebettetes_yyyyMMdd_Praefix(string raw, string expectedIso)
    {
        var parsed = TrainingSampleEligibility.TryParseInspectionDate(raw);

        Assert.Equal(DateTime.ParseExact(expectedIso, "yyyy-MM-dd", CultureInfo.InvariantCulture), parsed);
    }

    [Theory]
    // Guards: keine falschen Treffer aus IDs, zu langen Ziffernfolgen oder ungueltigen Kalenderdaten.
    [InlineData("9866-9327")]            // reine Projekt-ID, kein Datum
    [InlineData("report_30250101.pdf")]  // eingebettetes Jahr ausserhalb [1990,2099]
    [InlineData("12345678")]             // 8-Lauf, aber Monat 56 ungueltig
    [InlineData("99999999")]             // Fueller-ID
    [InlineData("123420251110")]         // 8-Lauf nur innerhalb laengerer Ziffernfolge -> nicht isoliert
    [InlineData("202511109999")]         // Datum direkt von weiteren Ziffern gefolgt
    [InlineData("report_20230229.pdf")]  // 29. Feb in Nicht-Schaltjahr
    public void TryParseInspectionDate_ignoriert_Nicht_Datums_Ziffern(string raw)
    {
        Assert.Null(TrainingSampleEligibility.TryParseInspectionDate(raw));
    }

    [Theory]
    // Praezedenz/Regression: ungueltiger erster Lauf wird uebersprungen; bestehende Formate unveraendert.
    [InlineData("20251301_20231110.pdf", "2023-11-10")] // Monat 13 -> skip, dann gueltiger zweiter Lauf
    [InlineData("20251110", "2025-11-10")]              // reines yyyyMMdd (bestehender Exact-Pfad)
    [InlineData("2022/03/04", "2022-03-04")]            // bestehendes Trennzeichen-Format unveraendert
    public void TryParseInspectionDate_behaelt_Praezedenz_und_bestehende_Formate(string raw, string expectedIso)
    {
        var parsed = TrainingSampleEligibility.TryParseInspectionDate(raw);

        Assert.Equal(DateTime.ParseExact(expectedIso, "yyyy-MM-dd", CultureInfo.InvariantCulture), parsed);
    }

    private static TrainingSample MakeSample(string code)
        => new()
        {
            Code = code,
            InspectionDate = new DateTime(2022, 1, 1),
            TrainingEligible = true
        };

    private sealed class InMemoryCodeCatalogProvider : ICodeCatalogProvider
    {
        private readonly IReadOnlyList<CodeDefinition> _codes;

        public InMemoryCodeCatalogProvider(IReadOnlyList<CodeDefinition> codes)
            => _codes = codes;

        public IReadOnlyList<CodeDefinition> GetAll()
            => _codes;

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = _codes.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new InvalidOperationException("Test catalog is read-only.");

        public IReadOnlyList<string> AllowedCodes()
            => _codes.Where(c => c.IsSelectable && !c.IsObservedExtension).Select(c => c.Code).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }
}

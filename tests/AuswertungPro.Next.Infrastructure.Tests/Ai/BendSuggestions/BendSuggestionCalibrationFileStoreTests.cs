using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Infrastructure.Ai.BendSuggestions;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.BendSuggestions;

/// <summary>
/// Liest den gemessenen Arbeitspunkt neben dem Kandidaten. Er liegt bewusst nicht
/// im candidate_manifest.json: Der Sidecar-Vertrag liefert feste Felder, ein neues
/// Feld kaeme in C# nicht an.
/// </summary>
public sealed class BendSuggestionCalibrationFileStoreTests : IDisposable
{
    private const string Id = "bcc_nc15_seed46_20260808";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "sewerstudio-workpoint-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Ein_hinterlegter_Arbeitspunkt_wird_vollstaendig_gelesen()
    {
        Schreibe(Id, """
            {
              "schema_version": 1,
              "candidate_id": "bcc_nc15_seed46_20260808",
              "weight_sha256": "8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114",
              "min_confidence": 0.5,
              "strong_confidence": 0.8,
              "source": "Videomessung 2026-08-08"
            }
            """);

        var kalibrierung = Store().TryRead(Id);

        Assert.NotNull(kalibrierung);
        Assert.Equal(Id, kalibrierung!.CandidateId);
        Assert.Equal(0.5, kalibrierung.MinConfidence, 3);
        Assert.Equal(0.8, kalibrierung.StrongConfidence, 3);
        Assert.Equal("Videomessung 2026-08-08", kalibrierung.Source);
    }

    [Fact]
    public void Ohne_Datei_gibt_es_keine_Kalibrierung()
    {
        Directory.CreateDirectory(Path.Combine(_root, Id));

        Assert.Null(Store().TryRead(Id));
    }

    [Fact]
    public void Der_gelesene_Arbeitspunkt_passt_durch_die_Regel()
    {
        const string sha = "8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114";
        Schreibe(Id, $$"""
            {
              "candidate_id": "{{Id}}",
              "weight_sha256": "{{sha}}",
              "min_confidence": 0.5,
              "strong_confidence": 0.8,
              "source": "Videomessung 2026-08-08"
            }
            """);

        var ergebnis = BendSuggestionCalibrationPolicy.Resolve(Store().TryRead(Id), Id, sha);

        Assert.True(ergebnis.IsUsable);
        Assert.Equal(0.8, ergebnis.Options!.StrongConfidence, 3);
    }

    [Fact]
    public void Eine_beschaedigte_Datei_wird_gemeldet_statt_stillschweigend_uebergangen()
    {
        // Ein Tippfehler darf nicht wie "kein Arbeitspunkt hinterlegt" aussehen —
        // sonst sucht niemand nach der Ursache.
        Schreibe(Id, "{ das ist kein json");

        Assert.Throws<InvalidDataException>(() => Store().TryRead(Id));
    }

    [Theory]
    [InlineData("candidate_id")]
    [InlineData("weight_sha256")]
    [InlineData("min_confidence")]
    [InlineData("strong_confidence")]
    [InlineData("source")]
    public void Ein_fehlendes_Pflichtfeld_wird_namentlich_gemeldet(string weggelassen)
    {
        // Jedes Feld einzeln weglassen — sonst meldet die Pruefung nur das erste
        // fehlende und die uebrigen bleiben ungeprueft.
        var felder = new Dictionary<string, string>
        {
            ["candidate_id"] = $"\"candidate_id\": \"{Id}\"",
            ["weight_sha256"] = $"\"weight_sha256\": \"{new string('a', 64)}\"",
            ["min_confidence"] = "\"min_confidence\": 0.5",
            ["strong_confidence"] = "\"strong_confidence\": 0.8",
            ["source"] = "\"source\": \"Videomessung\""
        };
        felder.Remove(weggelassen);
        Schreibe(Id, "{ " + string.Join(", ", felder.Values) + " }");

        var fehler = Assert.Throws<InvalidDataException>(() => Store().TryRead(Id));

        Assert.Contains(weggelassen, fehler.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ein_leerer_Herkunftsbeleg_zaehlt_als_fehlend()
    {
        // Ein Arbeitspunkt ohne Beleg ist geraten.
        Schreibe(Id, $$"""
            {
              "candidate_id": "{{Id}}",
              "weight_sha256": "{{new string('a', 64)}}",
              "min_confidence": 0.5,
              "strong_confidence": 0.8,
              "source": "   "
            }
            """);

        Assert.Throws<InvalidDataException>(() => Store().TryRead(Id));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../fremd")]
    [InlineData("a/b")]
    [InlineData("  ")]
    public void Eine_unsichere_Kandidaten_ID_wird_abgewiesen(string id)
    {
        Assert.Throws<ArgumentException>(() => Store().TryRead(id));
    }

    private BendSuggestionCalibrationFileStore Store() => new(_root);

    private void Schreibe(string id, string inhalt)
    {
        var ordner = Path.Combine(_root, id);
        Directory.CreateDirectory(ordner);
        File.WriteAllText(Path.Combine(ordner, "workpoint.json"), inhalt);
    }
}

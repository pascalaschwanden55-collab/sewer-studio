using System;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Phase 5.3 Sub-B: Sicherstellt dass die Migration des frueheren MVVM-
/// `TrainingCase` (UI) zum neuen POCO `TrainingCase` (Application) den
/// JSON-Roundtrip nicht bricht. Bestehende `training_center.json`-Dateien
/// muessen unveraendert lesbar bleiben.
/// </summary>
public class TrainingCaseJsonRoundtripTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void TrainingCenterState_RoundTrip_PreservesAllProperties()
    {
        var original = new TrainingCenterState
        {
            UpdatedUtc = new DateTime(2026, 5, 7, 10, 0, 0, DateTimeKind.Utc),
            RootFolders = { "D:\\Videoprojekte\\Auftrag-A", "E:\\Archiv" },
            Cases =
            {
                new TrainingCase
                {
                    CaseId = "047261-Goeschenen/Film",
                    FolderPath = "D:\\Videoprojekte\\047261-Goeschenen\\Film",
                    VideoPath = "D:\\Videoprojekte\\047261-Goeschenen\\Film\\H1.mpg",
                    ProtocolPath = "D:\\Videoprojekte\\047261-Goeschenen\\Film\\H1.pdf",
                    Status = TrainingCaseStatus.BatchImported,
                    CreatedUtc = new DateTime(2026, 5, 1, 12, 34, 51, DateTimeKind.Utc),
                    Rohrmaterial = "Polyethylen",
                    NennweiteMm = 300,
                    Profil = "Kreisprofil 300mm",
                },
                new TrainingCase
                {
                    CaseId = "Empty-Case",
                    FolderPath = "D:\\X",
                    Status = TrainingCaseStatus.New,
                    CreatedUtc = new DateTime(2026, 5, 5, 0, 0, 0, DateTimeKind.Utc),
                    // Rohrmaterial / NennweiteMm / Profil bleiben null
                },
            }
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var roundtripped = JsonSerializer.Deserialize<TrainingCenterState>(json, JsonOptions);

        Assert.NotNull(roundtripped);
        Assert.Equal(original.UpdatedUtc, roundtripped!.UpdatedUtc);
        Assert.Equal(original.RootFolders, roundtripped.RootFolders);
        Assert.Equal(2, roundtripped.Cases.Count);

        var c1 = roundtripped.Cases[0];
        Assert.Equal("047261-Goeschenen/Film", c1.CaseId);
        Assert.Equal("D:\\Videoprojekte\\047261-Goeschenen\\Film", c1.FolderPath);
        Assert.Equal("D:\\Videoprojekte\\047261-Goeschenen\\Film\\H1.mpg", c1.VideoPath);
        Assert.Equal("D:\\Videoprojekte\\047261-Goeschenen\\Film\\H1.pdf", c1.ProtocolPath);
        Assert.Equal(TrainingCaseStatus.BatchImported, c1.Status);
        Assert.Equal(new DateTime(2026, 5, 1, 12, 34, 51, DateTimeKind.Utc), c1.CreatedUtc);
        Assert.Equal("Polyethylen", c1.Rohrmaterial);
        Assert.Equal(300, c1.NennweiteMm);
        Assert.Equal("Kreisprofil 300mm", c1.Profil);

        var c2 = roundtripped.Cases[1];
        Assert.Equal("Empty-Case", c2.CaseId);
        Assert.Equal(TrainingCaseStatus.New, c2.Status);
        Assert.Null(c2.Rohrmaterial);
        Assert.Null(c2.NennweiteMm);
        Assert.Null(c2.Profil);
    }

    /// <summary>
    /// Liest eine Datei im exakten Schema der frueheren MVVM-Klasse (mit
    /// Status als Integer = Enum-Wert) und stellt sicher dass der POCO sie
    /// fehlerfrei akzeptiert. Schuetzt vor Schema-Drift bei zukuenftigen
    /// Aenderungen.
    /// </summary>
    [Fact]
    public void LegacyJsonShape_DeserializesIntoPoco()
    {
        const string legacyJson = """
        {
          "Cases": [
            {
              "Rohrmaterial": null,
              "NennweiteMm": null,
              "Profil": null,
              "CaseId": "047261-Göschenen Unterdorfstrasse_IO",
              "FolderPath": "D:\\Videoprojekte\\047261-Göschenen Unterdorfstrasse_IO",
              "VideoPath": "",
              "ProtocolPath": "",
              "Status": 0,
              "CreatedUtc": "2026-05-01T12:34:51.7968829Z"
            },
            {
              "Rohrmaterial": "Polyethylen",
              "NennweiteMm": 250,
              "Profil": "Kreis",
              "CaseId": "Test/Sub",
              "FolderPath": "D:\\X\\Sub",
              "VideoPath": "D:\\X\\Sub\\v.mpg",
              "ProtocolPath": "D:\\X\\Sub\\p.pdf",
              "Status": 4,
              "CreatedUtc": "2026-05-01T13:00:00.0000000Z"
            }
          ],
          "RootFolders": ["D:\\Videoprojekte"],
          "UpdatedUtc": "2026-05-06T09:15:30.0000000Z"
        }
        """;

        var state = JsonSerializer.Deserialize<TrainingCenterState>(legacyJson, JsonOptions);

        Assert.NotNull(state);
        Assert.Equal(2, state!.Cases.Count);
        Assert.Equal("047261-Göschenen Unterdorfstrasse_IO", state.Cases[0].CaseId);
        Assert.Equal(TrainingCaseStatus.New, state.Cases[0].Status);
        Assert.Null(state.Cases[0].Rohrmaterial);

        Assert.Equal(TrainingCaseStatus.BatchImported, state.Cases[1].Status);
        Assert.Equal("Polyethylen", state.Cases[1].Rohrmaterial);
        Assert.Equal(250, state.Cases[1].NennweiteMm);

        Assert.Single(state.RootFolders);
        Assert.Equal("D:\\Videoprojekte", state.RootFolders[0]);
    }
}

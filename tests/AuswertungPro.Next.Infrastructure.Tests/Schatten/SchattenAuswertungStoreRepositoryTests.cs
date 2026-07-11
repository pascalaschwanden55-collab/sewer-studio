using System;
using System.IO;
using AuswertungPro.Next.Application.Schatten;
using AuswertungPro.Next.Infrastructure.Schatten;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Schatten;

public sealed class SchattenAuswertungStoreRepositoryTests
{
    [Fact]
    public void SaveLoad_Roundtrip_MitCaseInsensitivenKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), "schatten-store", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var projectPath = Path.Combine(root, "projekt.json");
        try
        {
            var repo = new SchattenAuswertungStoreRepository();
            var store = new SchattenAuswertungStore { LetzterLaufUtc = DateTime.UtcNow, KiModell = "qwen3-vl:8b-q8" };
            store.ByHaltung["10081-8993"] = new SchattenHaltungErgebnis
            {
                Haltung = "10081-8993",
                Status = SchattenStatus.MitKi,
                Zustandsklasse = "3",
                KiMassnahme = "Schlauchliner",
                KostenErwartet = 12500m
            };

            Assert.True(repo.Save(projectPath, store, out var error), error);
            var geladen = repo.Load(projectPath, out var loadError);

            Assert.Null(loadError);
            Assert.Equal("qwen3-vl:8b-q8", geladen.KiModell);
            var e = geladen.ByHaltung["10081-8993"]; // exakter Key
            Assert.True(geladen.ByHaltung.ContainsKey("10081-8993".ToUpperInvariant())); // case-insensitive
            Assert.Equal(SchattenStatus.MitKi, e.Status);
            Assert.Equal(12500m, e.KostenErwartet);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DefektesJson_LiefertLoadErrorUndLeerenStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "schatten-store", Guid.NewGuid().ToString("N"));
        var dateiOrdner = Path.Combine(root, "schatten");
        Directory.CreateDirectory(dateiOrdner);
        var projectPath = Path.Combine(root, "projekt.json");
        File.WriteAllText(Path.Combine(dateiOrdner, "schatten_auswertung.json"), "{ kaputt !!");
        try
        {
            var repo = new SchattenAuswertungStoreRepository();
            var geladen = repo.Load(projectPath, out var loadError);

            Assert.NotNull(loadError); // Aufrufer darf jetzt NICHT speichern (K3-Regel)
            Assert.Empty(geladen.ByHaltung);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

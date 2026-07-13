using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

/// <summary>
/// Tests fuer die Save-Serialisierung (AP-50 Save-Schutz): Sobald Speichern in den
/// Hintergrund wandert, koennen manuelles Speichern und AutoSave gleichzeitig dieselbe
/// projekt.json schreiben. JsonProjectRepository.Save muss parallele Aufrufe serialisieren,
/// damit File.Replace/.bak nicht kollidieren und die Datei ladbar bleibt.
/// </summary>
public sealed class JsonProjectRepositorySaveSerializationTests
{
    [Fact]
    public async Task Save_VieleParalleleAufrufe_AlleErfolgreich_UndDateiBleibtLadbar()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"repo-savepar-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "projekt.json");
        // Zieldatei existiert -> Save nutzt den File.Replace-Pfad inkl. gemeinsamer .bak,
        // genau dort kollidieren parallele Schreiber ohne Serialisierung.
        File.WriteAllText(path, "{}");
        var repo = new JsonProjectRepository();

        try
        {
            var tasks = Enumerable.Range(0, 40)
                .Select(i => Task.Run(() => repo.Save(new Project { Name = $"P{i}" }, path)))
                .ToArray();
            var results = await Task.WhenAll(tasks);

            Assert.All(results, r => Assert.True(r.Ok, r.ErrorMessage));
            Assert.True(repo.Load(path).Ok, "Datei muss nach parallelen Saves ladbar bleiben");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}

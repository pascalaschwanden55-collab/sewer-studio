using System.Diagnostics;
using System.Text.Json;
using Xunit;
using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Haelt die Zaehlregel der Produktabdeckung an der handpruefbaren Beispieldatei fest
/// (tests/Fixtures/Coverage/ProduktAbdeckung/README.md): nur src/, ohne erzeugten Code,
/// jede Zeile einmal ueber alle Berichte.
/// </summary>
public sealed class ProductCoverageScriptTests
{
    [Fact]
    public void Beispiel_ergibt_zwei_von_fuenf_Produktzeilen()
    {
        var json = Path.Combine(Path.GetTempPath(), $"produktabdeckung_{Guid.NewGuid():N}.json");
        try
        {
            var (exit, output) = Run(
                "-ResultsDirectory", Path.GetDirectoryName(
                    RepoFile("tests", "Fixtures", "Coverage", "ProduktAbdeckung", "README.md"))!,
                "-RepoRoot", "C:/repo",
                "-Bereiche", "src/Lib/B.cs,src/Lib/A.cs",
                "-JsonAusgabe", json);

            Assert.True(exit == 0, output);
            using var doc = JsonDocument.Parse(File.ReadAllText(json));
            var gesamt = doc.RootElement.GetProperty("gesamt");
            Assert.Equal(2, gesamt.GetProperty("Abgedeckt").GetInt32());
            Assert.Equal(5, gesamt.GetProperty("Gesamt").GetInt32());
            Assert.Equal(40.0, gesamt.GetProperty("Prozent").GetDouble());

            var bereiche = doc.RootElement.GetProperty("bereiche").EnumerateArray().ToArray();
            Assert.Equal((0, 2), (bereiche[0].GetProperty("Abgedeckt").GetInt32(), bereiche[0].GetProperty("Gesamt").GetInt32()));
            Assert.Equal((2, 3), (bereiche[1].GetProperty("Abgedeckt").GetInt32(), bereiche[1].GetProperty("Gesamt").GetInt32()));
        }
        finally
        {
            File.Delete(json);
        }
    }

    [Fact]
    public void Ordner_ohne_Bericht_ist_ein_technischer_Fehler()
    {
        var leer = Directory.CreateTempSubdirectory("produktabdeckung_leer_");
        try
        {
            var (exit, output) = Run("-ResultsDirectory", leer.FullName);
            Assert.True(exit == 2, output);
        }
        finally
        {
            leer.Delete(recursive: true);
        }
    }

    private static (int Exit, string Output) Run(params string[] args)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File",
                     RepoFile(".github", "scripts", "measure-product-coverage.ps1") }.Concat(args))
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(60)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Messskript antwortet nicht.");
        }
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}

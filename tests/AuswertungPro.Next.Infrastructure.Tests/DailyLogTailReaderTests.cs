using AuswertungPro.Next.Infrastructure.Diagnostics;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class DailyLogTailReaderTests
{
    [Fact]
    public void ReadToday_meldet_fehlende_tagesdatei_ohne_fehler()
    {
        using var directory = new TempDirectory();
        var reader = new DailyLogTailReader(directory.Path, () => new DateTime(2026, 7, 13));

        var result = reader.ReadToday();

        Assert.False(result.FileExists);
        Assert.Empty(result.Lines);
        Assert.Null(result.UserMessage);
    }

    [Fact]
    public void ReadToday_liefert_nur_die_letzten_angeforderten_zeilen()
    {
        using var directory = new TempDirectory();
        File.WriteAllLines(
            Path.Combine(directory.Path, "app-20260713.log"),
            ["Zeile 1", "Zeile 2", "Zeile 3", "Zeile 4"]);
        var reader = new DailyLogTailReader(directory.Path, () => new DateTime(2026, 7, 13));

        var result = reader.ReadToday(maximumLines: 2);

        Assert.True(result.FileExists);
        Assert.Equal(["Zeile 3", "Zeile 4"], result.Lines);
        Assert.Null(result.UserMessage);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "daily_log_tail_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Test-Aufräumen darf das Testergebnis nicht verdecken.
            }
        }
    }
}

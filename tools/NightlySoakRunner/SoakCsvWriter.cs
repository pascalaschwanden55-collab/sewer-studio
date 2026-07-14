using System.Globalization;
using System.Text;

namespace NightlySoakRunner;

public sealed class SoakCsvWriter : IAsyncDisposable
{
    private const string Header = "round;started_utc;video;success;elapsed_ms;pid;private_memory_mb;handles;health_vram_mb;nvidia_vram_mb;error";
    private readonly StreamWriter _writer;

    private SoakCsvWriter(StreamWriter writer)
    {
        _writer = writer;
    }

    public static async Task<SoakCsvWriter> CreateAsync(string path, CancellationToken ct)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await writer.WriteLineAsync(Header.AsMemory(), ct);
        await writer.FlushAsync(ct);
        return new SoakCsvWriter(writer);
    }

    public async Task WriteAsync(SoakRoundRecord row, CancellationToken ct)
    {
        var fields = new[]
        {
            row.Round.ToString(CultureInfo.InvariantCulture),
            row.StartedUtc.ToString("O", CultureInfo.InvariantCulture),
            Escape(row.VideoPath),
            row.Success ? "true" : "false",
            Format(row.ElapsedMilliseconds),
            row.ProcessId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            Format(row.PrivateMemoryMb),
            row.HandleCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            Format(row.HealthVramMb),
            Format(row.NvidiaVramMb),
            Escape(row.Error),
        };
        await _writer.WriteLineAsync(string.Join(';', fields).AsMemory(), ct);
        await _writer.FlushAsync(ct);
    }

    public async ValueTask DisposeAsync() => await _writer.DisposeAsync();

    private static string Format(double? value)
        => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var normalized = value.Replace("\r", " ").Replace("\n", " ");
        return normalized.IndexOfAny([';', '"']) < 0
            ? normalized
            : $"\"{normalized.Replace("\"", "\"\"")}\"";
    }
}

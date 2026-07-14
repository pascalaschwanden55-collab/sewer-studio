using System.Diagnostics;
using System.Globalization;

namespace NightlySoakRunner;

public sealed class ProcessResourceSampler : IResourceSampler
{
    public async Task<ResourceSnapshot> CaptureAsync(
        int processId,
        double? healthVramMb,
        NightlySoakOptions options,
        CancellationToken ct)
    {
        using var process = Process.GetProcessById(processId);
        process.Refresh();
        var nvidiaVramMb = await TryReadNvidiaVramMbAsync(
            options.NvidiaSmiPath,
            processId,
            ct);

        if (options.RequireNvidiaSmi && nvidiaVramMb is null)
        {
            throw new InvalidOperationException(
                $"nvidia-smi lieferte fuer Prozess {processId} keinen GPU-Speicherwert.");
        }

        return new ResourceSnapshot(
            processId,
            process.PrivateMemorySize64 / 1024d / 1024d,
            process.HandleCount,
            healthVramMb,
            nvidiaVramMb);
    }

    private static async Task<double?> TryReadNvidiaVramMbAsync(
        string executable,
        int processId,
        CancellationToken ct)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("--query-compute-apps=pid,used_memory");
            startInfo.ArgumentList.Add("--format=csv,noheader,nounits");

            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            var outputTask = process.StandardOutput.ReadToEndAsync(ct);
            var errorTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(10), ct);
            var output = await outputTask;
            _ = await errorTask;
            if (process.ExitCode != 0)
                return null;

            double total = 0;
            var found = false;
            foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 2
                    || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)
                    || pid != processId
                    || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var memory))
                {
                    continue;
                }

                total += memory;
                found = true;
            }

            return found ? total : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}

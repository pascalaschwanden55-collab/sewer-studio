using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerLibVlcArguments
{
    public static string[] Build(PlayerWindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var args = new List<string>();

        if (!string.Equals(options.VideoOutput, "any", StringComparison.OrdinalIgnoreCase))
            args.Add($"--vout={options.VideoOutput}");

        args.Add(options.EnableHardwareDecoding ? "--avcodec-hw=dxva2" : "--avcodec-hw=none");
        args.Add($"--avcodec-threads={options.CodecThreads}");
        args.Add($"--file-caching={options.FileCachingMs}");
        args.Add($"--network-caching={options.NetworkCachingMs}");

        if (options.DropLateFrames)
            args.Add("--drop-late-frames");
        if (options.SkipFrames)
            args.Add("--skip-frames");

        args.Add("--clock-jitter=0");
        args.Add("--clock-synchro=0");
        args.Add("--no-snapshot-preview");

        return args.ToArray();
    }
}

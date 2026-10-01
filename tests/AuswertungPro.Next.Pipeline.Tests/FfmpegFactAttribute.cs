using System.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Fuehrt den Test nur aus, wenn ffmpeg auffindbar und ausfuehrbar ist.</summary>
internal sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute()
    {
        if (!FfmpegProbe.Verfuegbar)
            Skip = "ffmpeg nicht auffindbar (SEWERSTUDIO_FFMPEG, PATH oder Standardorte).";
    }
}

internal static class FfmpegProbe
{
    private static readonly Lazy<bool> VerfuegbarLazy = new(Pruefe);

    public static bool Verfuegbar => VerfuegbarLazy.Value;

    /// <summary>
    /// Startet ffmpeg wirklich. Ein blosser Name wie "ffmpeg" galt frueher schon als
    /// vorhanden, weil er kein absoluter Pfad ist — auf einem Rechner ohne ffmpeg lief
    /// der Test dann trotzdem los und scheiterte mit Win32Exception, statt uebersprungen
    /// zu werden. Genau das war in der CI der Fall.
    /// </summary>
    private static bool Pruefe()
    {
        try
        {
            var pfad = new FfmpegFileLocator().ResolveFfmpeg();
            if (string.IsNullOrWhiteSpace(pfad))
                return false;

            using var prozess = Process.Start(new ProcessStartInfo(pfad, "-version")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (prozess is null)
                return false;

            // Zeitlimit: Ein haengendes ffmpeg darf den Testlauf nicht blockieren.
            if (!prozess.WaitForExit(10_000))
            {
                try { prozess.Kill(entireProcessTree: true); } catch { /* Aufraeumen ist Nebensache */ }
                return false;
            }

            return prozess.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

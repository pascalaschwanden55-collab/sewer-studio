using System;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

public static class TechniqueFrameAnalyzer
{
    private const double OsdGoodDelta = 0.5;
    private const double OsdMediumDelta = 1.0;
    private const double LuminanceDark = 40.0;
    private const double LuminanceBright = 200.0;
    private const double SharpnessThreshold = 50.0;

    public static TechniqueAssessment AssessFrame(
        BgraImageFrame frame,
        double? osdMeterReading,
        double protocolMeter,
        string? centering = null)
    {
        var osdReadable = osdMeterReading.HasValue;
        var osdDelta = osdReadable ? Math.Abs(osdMeterReading!.Value - protocolMeter) : (double?)null;

        var meanLum = ComputeMeanLuminance(frame);
        var lapVar = ComputeLaplacianVariance(frame);

        var lighting = EvaluateLighting(meanLum);
        var sharpness = EvaluateSharpness(lapVar);
        var grade = ComputeGrade(osdReadable, osdDelta, lighting, sharpness, centering);

        return new TechniqueAssessment(
            OsdReadable: osdReadable,
            OsdDeltaMeters: osdDelta,
            LightingQuality: lighting,
            SharpnessQuality: sharpness,
            CenteringQuality: centering,
            OverallGrade: grade,
            MeanLuminance: meanLum,
            LaplacianVariance: lapVar);
    }

    public static string ComputeGrade(
        bool osdReadable,
        double? osdDelta,
        string lighting,
        string sharpness,
        string? centering)
    {
        var score = 0;
        var maxScore = 0;

        maxScore += 2;
        if (osdReadable && osdDelta.HasValue)
        {
            if (osdDelta.Value < OsdGoodDelta) score += 2;
            else if (osdDelta.Value < OsdMediumDelta) score += 1;
        }

        maxScore += 2;
        score += lighting switch { "Gut" => 2, "Mittel" => 1, _ => 0 };

        maxScore += 2;
        score += sharpness switch { "Gut" => 2, "Mittel" => 1, _ => 0 };

        if (centering is not null)
        {
            maxScore += 2;
            score += centering switch { "Gut" => 2, "Mittel" => 1, _ => 0 };
        }

        var pct = maxScore > 0 ? (double)score / maxScore : 0;
        if (pct >= 0.75) return "A";
        if (pct >= 0.45) return "B";
        return "C";
    }

    private static double ComputeMeanLuminance(BgraImageFrame frame)
    {
        if (!HasUsablePixels(frame))
            return 100;

        long sum = 0;
        var count = 0;
        for (var i = 0; i < frame.Pixels.Length - 3; i += 32)
        {
            var b = frame.Pixels[i];
            var g = frame.Pixels[i + 1];
            var r = frame.Pixels[i + 2];
            sum += (int)(0.299 * r + 0.587 * g + 0.114 * b);
            count++;
        }

        return count > 0 ? (double)sum / count : 100;
    }

    private static double ComputeLaplacianVariance(BgraImageFrame frame)
    {
        if (!HasUsablePixels(frame))
            return 100;

        var w = frame.Width;
        var h = frame.Height;
        var stride = w * 4;

        var sampleStep = Math.Max(1, Math.Min(w, h) / 200);
        var sw = (w - 2) / sampleStep;
        var sh = (h - 2) / sampleStep;
        if (sw < 3 || sh < 3)
            return 100;

        double sum = 0;
        double sumSq = 0;
        var count = 0;

        for (var y = 1; y < h - 1; y += sampleStep)
        {
            for (var x = 1; x < w - 1; x += sampleStep)
            {
                var center = GetLuminance(frame.Pixels, x, y, stride);
                var top = GetLuminance(frame.Pixels, x, y - 1, stride);
                var bottom = GetLuminance(frame.Pixels, x, y + 1, stride);
                var left = GetLuminance(frame.Pixels, x - 1, y, stride);
                var right = GetLuminance(frame.Pixels, x + 1, y, stride);

                var lap = top + bottom + left + right - 4 * center;
                sum += lap;
                sumSq += lap * lap;
                count++;
            }
        }

        if (count == 0)
            return 100;

        var mean = sum / count;
        return (sumSq / count) - (mean * mean);
    }

    private static bool HasUsablePixels(BgraImageFrame frame)
        => frame.Width > 0
           && frame.Height > 0
           && frame.Pixels.Length >= frame.Width * frame.Height * 4;

    private static double GetLuminance(byte[] pixels, int x, int y, int stride)
    {
        var idx = y * stride + x * 4;
        return 0.299 * pixels[idx + 2] + 0.587 * pixels[idx + 1] + 0.114 * pixels[idx];
    }

    private static string EvaluateLighting(double meanLuminance)
    {
        if (meanLuminance < LuminanceDark) return "Schlecht";
        if (meanLuminance > LuminanceBright) return "Schlecht";
        if (meanLuminance < 60 || meanLuminance > 180) return "Mittel";
        return "Gut";
    }

    private static string EvaluateSharpness(double laplacianVariance)
    {
        if (laplacianVariance < 30) return "Schlecht";
        if (laplacianVariance < SharpnessThreshold) return "Mittel";
        return "Gut";
    }
}

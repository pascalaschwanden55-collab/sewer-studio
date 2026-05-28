using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Ai.Calibration;

public static class AutoCalibrationService
{
    private const int MinGradientStrength = 25;
    private const double MinDiameterRatio = 0.25;
    private const double MaxDiameterRatio = 0.92;

    private static readonly double[] ScanLines = { 0.45, 0.475, 0.50, 0.525, 0.55 };

    public static PipeCalibration? TryAutoCalibrate(GrayscaleImageFrame frame, int nominalDiameterMm)
    {
        if (nominalDiameterMm <= 0)
            return null;

        var width = frame.Width;
        var height = frame.Height;
        if (width < 100 || height < 100)
            return null;

        if (frame.Pixels.Length < width * height)
            return null;

        var measurements = new List<(int left, int right)>();

        foreach (var scanY in ScanLines)
        {
            var y = (int)(scanY * height);
            if (y < 0 || y >= height)
                continue;

            var edges = FindPipeEdgesInRow(frame.Pixels, width, y);
            if (edges.HasValue)
                measurements.Add(edges.Value);
        }

        if (measurements.Count < 3)
            return null;

        var diameters = measurements
            .Select(m => m.right - m.left)
            .OrderBy(d => d)
            .ToArray();
        var medianDiameter = diameters[diameters.Length / 2];

        var centers = measurements
            .Select(m => (m.left + m.right) / 2.0)
            .OrderBy(c => c)
            .ToArray();
        var medianCenterX = centers[centers.Length / 2];

        var diameterRatio = (double)medianDiameter / width;
        if (diameterRatio < MinDiameterRatio || diameterRatio > MaxDiameterRatio)
            return null;

        return new PipeCalibration
        {
            NominalDiameterMm = nominalDiameterMm,
            NormalizedDiameter = diameterRatio,
            PipePixelDiameter = medianDiameter,
            PipeCenter = new NormalizedPoint(medianCenterX / width, 0.50),
            WasManuallyCalibrated = true
        };
    }

    private static (int left, int right)? FindPipeEdgesInRow(byte[] gray, int width, int y)
    {
        var rowStart = y * width;

        var leftEdge = -1;
        var maxLeftGrad = 0;
        for (var x = 10; x < width / 2; x++)
        {
            var grad = gray[rowStart + x + 1] - gray[rowStart + x - 1];
            if (grad > maxLeftGrad && grad > MinGradientStrength)
            {
                maxLeftGrad = grad;
                leftEdge = x;
            }
        }

        var rightEdge = -1;
        var maxRightGrad = 0;
        for (var x = width - 11; x > width / 2; x--)
        {
            var grad = gray[rowStart + x - 1] - gray[rowStart + x + 1];
            if (grad > maxRightGrad && grad > MinGradientStrength)
            {
                maxRightGrad = grad;
                rightEdge = x;
            }
        }

        if (leftEdge < 0 || rightEdge < 0)
            return null;

        if (rightEdge <= leftEdge + 50)
            return null;

        return (leftEdge, rightEdge);
    }
}

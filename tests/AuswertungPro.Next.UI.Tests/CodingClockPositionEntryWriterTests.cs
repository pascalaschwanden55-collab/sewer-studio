using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingClockPositionEntryWriterTests
{
    private static readonly QuantificationGate.ManifestQuantRule ClockAllowed = new(true, true, true);
    private static readonly QuantificationGate.ManifestQuantRule ClockBlocked = new(true, true, false);

    [Fact]
    public void ApplyToEntry_ignores_missing_bbox_or_invalid_image_size()
    {
        var entry = new ProtocolEntry();

        CodingClockPositionEntryWriter.ApplyToEntry(
            entry,
            "BCAEB",
            Finding([]),
            imageWidth: 100,
            imageHeight: 100,
            calibration: CalibratedPipe(),
            manifestRule: ClockAllowed);

        CodingClockPositionEntryWriter.ApplyToEntry(
            entry,
            "BCAEB",
            Finding([70, 40, 90, 60]),
            imageWidth: 0,
            imageHeight: 100,
            calibration: CalibratedPipe(),
            manifestRule: ClockAllowed);

        Assert.Null(entry.CodeMeta);
    }

    [Fact]
    public void ApplyToEntry_respects_manifest_clock_block()
    {
        var entry = new ProtocolEntry();

        CodingClockPositionEntryWriter.ApplyToEntry(
            entry,
            "BDD",
            Finding([70, 40, 90, 60]),
            imageWidth: 100,
            imageHeight: 100,
            calibration: CalibratedPipe(),
            manifestRule: ClockBlocked);

        Assert.Null(entry.CodeMeta);
    }

    [Fact]
    public void ApplyToEntry_writes_point_clock_and_removes_stale_to_value()
    {
        var entry = new ProtocolEntry
        {
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "BCAEB",
                Parameters =
                {
                    ["vsa.uhr.von"] = "12:00",
                    ["vsa.uhr.bis"] = "6:00"
                }
            }
        };

        CodingClockPositionEntryWriter.ApplyToEntry(
            entry,
            "BCAEB",
            Finding([70, 40, 90, 60]),
            imageWidth: 100,
            imageHeight: 100,
            calibration: CalibratedPipe(),
            manifestRule: ClockAllowed);

        Assert.Equal("3:00", entry.CodeMeta!.Parameters["vsa.uhr.von"]);
        Assert.Equal(["vsa.uhr.von"], entry.CodeMeta.Parameters.Keys);
    }

    [Fact]
    public void ApplyToEntry_removes_stale_clock_when_position_is_unknown()
    {
        var entry = new ProtocolEntry
        {
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "BCAEB",
                Parameters =
                {
                    ["vsa.uhr.von"] = "3:00",
                    ["vsa.uhr.bis"] = "5:00"
                }
            }
        };

        CodingClockPositionEntryWriter.ApplyToEntry(
            entry,
            "BCAEB",
            Finding([48, 48, 52, 52]),
            imageWidth: 100,
            imageHeight: 100,
            calibration: CalibratedPipe(),
            manifestRule: ClockAllowed);

        Assert.Empty(entry.CodeMeta!.Parameters);
    }

    private static PipeCalibration CalibratedPipe()
        => new()
        {
            NominalDiameterMm = 300,
            NormalizedDiameter = 0.7,
            Source = CalibrationSource.Auto
        };

    private static SegmentedFinding Finding(IReadOnlyList<double> bbox)
    {
        var mask = new SamMaskResult(
            Label: "connection",
            Confidence: 0.9,
            Bbox: bbox,
            MaskRle: "0",
            MaskAreaPixels: 100,
            ImageAreaPixels: 10_000,
            HeightPixels: 100,
            WidthPixels: 100,
            CentroidX: 50,
            CentroidY: 50);
        var quant = new MaskQuantificationService.QuantifiedMask(
            "connection",
            0.9,
            null,
            null,
            null,
            null,
            null,
            null);
        var proximity = new MetrierungProximityResult(
            MetrierungProximity.Codierbar,
            "",
            0,
            0,
            0,
            false,
            false);
        return new SegmentedFinding(null, mask, quant, proximity);
    }
}

using System;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterDisplayFormatterTests
{
    [Fact]
    public void FormatRootFolders_returns_empty_for_no_folders()
    {
        Assert.Equal("", TrainingCenterDisplayFormatter.FormatRootFolders(Array.Empty<string>()));
    }

    [Fact]
    public void FormatRootFolders_returns_full_path_for_single_folder()
    {
        Assert.Equal(
            @"C:\Training\ProjektA",
            TrainingCenterDisplayFormatter.FormatRootFolders(new[] { @"C:\Training\ProjektA" }));
    }

    [Fact]
    public void FormatRootFolders_returns_count_and_folder_names_for_multiple_folders()
    {
        var text = TrainingCenterDisplayFormatter.FormatRootFolders(new[]
        {
            @"C:\Training\ProjektA",
            @"D:\Daten\ProjektB\"
        });

        Assert.Equal("2 Ordner: ProjektA; ProjektB", text);
    }

    [Fact]
    public void FormatScanSummary_includes_pdf_only_and_missing_protocol_counts()
    {
        var text = TrainingCenterDisplayFormatter.FormatScanSummary(
            total: 8,
            withProtocol: 5,
            pdfOnly: 2);

        Assert.Equal("Gefunden: 8 Fälle, 2 nur PDF, 3 ohne Protokoll", text);
    }

    [Fact]
    public void FormatScanSummary_omits_zero_detail_counts()
    {
        var text = TrainingCenterDisplayFormatter.FormatScanSummary(
            total: 4,
            withProtocol: 4,
            pdfOnly: 0);

        Assert.Equal("Gefunden: 4 Fälle", text);
    }
}

using System;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPagePrintArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_awu_haltungsprotokoll_druck_an_print_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        var method = ExtractMethod(source, "private void PrintAwuHaltungsprotokollPdf(HaltungRecord? record)");

        Assert.Contains("_printController.PrintAwuHaltungsprotokollPdf(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildHaltungsprotokollPdf(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllBytes(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("_sp.Dialogs.SaveFile(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Haltungsprotokoll_AWU_", method, StringComparison.Ordinal);
    }

    [Fact]
    public void DataPageViewModel_delegiert_hydraulik_pdf_druck_an_print_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        var method = ExtractMethod(source, "private async void PrintHydraulikPdf(HaltungRecord? record)");

        Assert.Contains("_printController.PrintHydraulikPdfAsync(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new HydraulikPrintDialog(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("HydraulikPdfBuilder.Build(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllBytes(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("_sp.Dialogs.SaveFile(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Hydraulik_", method, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string marker)
    {
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new InvalidOperationException($"Method marker not found: {marker}");

        var openBrace = source.IndexOf('{', markerIndex);
        if (openBrace < 0)
            throw new InvalidOperationException($"Method has no body: {marker}");

        var depth = 0;
        for (var i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
                continue;
            }

            if (source[i] != '}')
                continue;

            depth--;
            if (depth == 0)
                return source.Substring(markerIndex, i - markerIndex + 1);
        }

        throw new InvalidOperationException($"Method body is incomplete: {marker}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AuswertungPro.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}

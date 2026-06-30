using System;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageCommandArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_dropdown_command_erzeugung_an_factory()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "DataPageViewModel.cs"));

        var constructorBody = ExtractMethodBody(
            source,
            "public DataPageViewModel(ShellViewModel shell, ServiceProvider services)");

        Assert.Contains("DataPageDropdownCommandFactory.Create(", constructorBody, StringComparison.Ordinal);
        Assert.DoesNotContain("new RelayCommand(EditSanierenOptions)", constructorBody, StringComparison.Ordinal);
        Assert.DoesNotContain("new RelayCommand(PreviewSanierenOptions)", constructorBody, StringComparison.Ordinal);
        Assert.DoesNotContain("new RelayCommand(ResetSanierenOptions)", constructorBody, StringComparison.Ordinal);
        Assert.DoesNotContain("new RelayCommand<object?>(AddSanierenOption)", constructorBody, StringComparison.Ordinal);
        Assert.DoesNotContain("new RelayCommand<object?>(RemoveSanierenOption)", constructorBody, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository-Root mit AuswertungPro.sln wurde nicht gefunden.");
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Signatur nicht gefunden: {signature}");

        var braceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(braceIndex >= 0, $"Methodenrumpf nicht gefunden: {signature}");

        var depth = 0;
        for (var i = braceIndex; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source[braceIndex..(i + 1)];
            }
        }

        throw new InvalidOperationException($"Methodenrumpf nicht abgeschlossen: {signature}");
    }
}

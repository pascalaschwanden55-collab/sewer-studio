using System;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

internal static class SourceTextTestHelpers
{
    public static string FindRepositoryRoot()
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

    public static string FindRepoRoot()
        => FindRepositoryRoot();

    public static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Signatur nicht gefunden: {signature}");

        var arrowIndex = source.IndexOf("=>", signatureIndex, StringComparison.Ordinal);
        var nextBraceIndex = source.IndexOf('{', signatureIndex);
        if (arrowIndex >= 0 && (nextBraceIndex < 0 || arrowIndex < nextBraceIndex))
        {
            var semicolonIndex = source.IndexOf(';', arrowIndex);
            Assert.True(semicolonIndex >= 0, $"Expression-Body nicht abgeschlossen: {signature}");
            return source[signatureIndex..(semicolonIndex + 1)];
        }

        var braceIndex = nextBraceIndex;
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

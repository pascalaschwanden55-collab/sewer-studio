namespace AuswertungPro.Next.Infrastructure.Tests;

using System.Text.Json;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using static TestRepoPaths;

public sealed class M150MdbImportHelperProcessSafetyTests
{
    [Fact]
    public void M150MdbRowReaderUsesSharedTimeoutProcessRunner()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.Infrastructure",
            "Import",
            "Xtf",
            "M150MdbRowReader.cs"));

        Assert.Contains("ExternalProcessRunner.RunAsync", source);
        AssertNoForbiddenTokens(source, "WaitForExit(");
    }

    [Fact]
    public void M150MdbImportHelper_uebernimmt_Tabellenname_und_Zeilenwerte_aus_Json()
    {
        using var document = JsonDocument.Parse(
            """{"table":"S_T","row":{"S_ID":"42","S_StartNode":"865"}}""");
        var rows = new List<Dictionary<string, string>>();
        PowerShellM150MdbRowReader.TryAppendJsonRow(document.RootElement, rows);

        var row = Assert.Single(rows);
        Assert.Equal("S_T", row["__table"]);
        Assert.Equal("42", row["S_ID"]);
        Assert.Equal("865", row["S_StartNode"]);
    }

    [Fact]
    public void TryReadRows_liest_Prozessausgabe_und_entfernt_Tempdateien()
    {
        string? startedFile = null;
        IReadOnlyList<string>? startedArguments = null;
        TimeSpan startedTimeout = default;
        var reader = new PowerShellM150MdbRowReader((fileName, arguments, timeout) =>
        {
            startedFile = fileName;
            startedArguments = arguments;
            startedTimeout = timeout;
            var outputPath = ValueAfter(arguments, "-OutPath");
            File.WriteAllText(
                outputPath,
                """[{"table":"S_T","row":{"S_ID":"42","S_EndNode":"864"}}]""");
            return new ExternalProcessRunResult(true, 0, false, string.Empty, string.Empty, null);
        });

        var success = reader.TryReadRows(
            @"C:\Import\wincan.mdb",
            out var rows,
            out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("powershell", startedFile);
        Assert.Equal(TimeSpan.FromSeconds(120), startedTimeout);
        Assert.NotNull(startedArguments);
        Assert.Equal(@"C:\Import\wincan.mdb", ValueAfter(startedArguments!, "-MdbPath"));
        Assert.False(File.Exists(ValueAfter(startedArguments!, "-File")));
        Assert.False(File.Exists(ValueAfter(startedArguments!, "-OutPath")));
        var row = Assert.Single(rows);
        Assert.Equal("S_T", row["__table"]);
        Assert.Equal("864", row["S_EndNode"]);
    }

    [Fact]
    public void TryReadRows_gibt_Prozessfehler_unveraendert_zurueck()
    {
        var reader = new PowerShellM150MdbRowReader((_, _, _) =>
            new ExternalProcessRunResult(
                false,
                1,
                false,
                string.Empty,
                "Access-Treiber fehlt",
                "ExitCode 1"));

        var success = reader.TryReadRows("defekt.mdb", out var rows, out var error);

        Assert.False(success);
        Assert.Empty(rows);
        Assert.Equal("Access-Treiber fehlt", error);
    }

    private static string ValueAfter(IReadOnlyList<string> arguments, string name)
    {
        var index = arguments.ToList().IndexOf(name);
        return index >= 0 && index + 1 < arguments.Count
            ? arguments[index + 1]
            : string.Empty;
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene blockierende Prozess-APIs gefunden: " + string.Join(", ", hits));
    }
}

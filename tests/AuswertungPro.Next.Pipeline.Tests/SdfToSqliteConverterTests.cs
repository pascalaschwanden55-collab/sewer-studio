using System.IO;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using Microsoft.Data.Sqlite;
using Xunit;
using Xunit.Abstractions;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Integrationstest: SDF → SQLite via PowerShell+Python-Pipeline.
/// Braucht SSCE 4.0 Runtime + powershell.exe + python im PATH.
/// </summary>
public class SdfToSqliteConverterTests
{
    private readonly ITestOutputHelper _output;
    public SdfToSqliteConverterTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Convert_AndermattZone211_YieldsExpectedTables()
    {
        const string sdf = @"G:\Zone 2.11\Projects\Gep_Andermatt_Zone_2.11_6490 Andermatt_202639\db\Gep_Andermatt_Zone_2.11_6490 Andermatt_202639.sdf";
        if (!File.Exists(sdf))
        {
            _output.WriteLine($"Testdatei fehlt: {sdf} — Test uebersprungen");
            return;
        }
        if (!SdfToSqliteConverter.IsSsceAvailable())
        {
            _output.WriteLine("SSCE 4.0 Runtime fehlt — Test uebersprungen");
            return;
        }

        var outPath = Path.Combine(Path.GetTempPath(),
            $"sdftest_{System.Guid.NewGuid():N}.db3");
        try
        {
            var result = SdfToSqliteConverter.Convert(sdf, outPath);
            _output.WriteLine($"Konvertiert nach: {result}");
            Assert.True(File.Exists(result));

            using var con = new SqliteConnection($"Data Source={result}");
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM SECOBS";
            var obs = System.Convert.ToInt32(cmd.ExecuteScalar());
            _output.WriteLine($"SECOBS rows: {obs}");
            Assert.True(obs >= 200, $"Erwartet >=200 SECOBS-Zeilen, bekommen {obs}");
        }
        finally
        {
            try { File.Delete(outPath); } catch { }
        }
    }
}

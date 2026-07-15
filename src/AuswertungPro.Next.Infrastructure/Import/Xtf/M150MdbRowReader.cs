using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>Liest alle Tabellenzeilen einer M150-/WinCan-MDB als Textwerte.</summary>
public interface IM150MdbRowReader
{
    bool TryReadRows(
        string mdbPath,
        out List<Dictionary<string, string>> rows,
        out string? error);
}

/// <summary>
/// Liest MDB-Dateien über den vorhandenen Windows-OLEDB-Treiber in einem begrenzten
/// PowerShell-Hilfsprozess. Fehler einzelner Tabellen stoppen die übrigen Tabellen nicht.
/// </summary>
public sealed class PowerShellM150MdbRowReader : IM150MdbRowReader
{
    private readonly Func<string, IReadOnlyList<string>, TimeSpan, ExternalProcessRunResult> _runProcess;

    public PowerShellM150MdbRowReader()
        : this(RunProcess)
    {
    }

    internal PowerShellM150MdbRowReader(
        Func<string, IReadOnlyList<string>, TimeSpan, ExternalProcessRunResult> runProcess)
    {
        _runProcess = runProcess ?? throw new ArgumentNullException(nameof(runProcess));
    }

    public bool TryReadRows(
        string mdbPath,
        out List<Dictionary<string, string>> rows,
        out string? error)
    {
        rows = [];
        error = null;

        var tempScript = Path.Combine(Path.GetTempPath(), $"mdb_dump_{Guid.NewGuid():N}.ps1");
        var tempJson = Path.Combine(Path.GetTempPath(), $"mdb_dump_{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(tempScript, PowerShellScript, new UTF8Encoding(false));
            var arguments = new[]
            {
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                tempScript,
                "-MdbPath",
                mdbPath,
                "-OutPath",
                tempJson
            };
            var result = _runProcess("powershell", arguments, TimeSpan.FromSeconds(120));

            if (!result.Success)
            {
                error = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr;
                if (string.IsNullOrWhiteSpace(error))
                    error = result.Message ?? "MDB-Import fehlgeschlagen.";
                return false;
            }

            if (!File.Exists(tempJson))
            {
                error = "MDB-Ausgabe konnte nicht erstellt werden.";
                return false;
            }

            var json = File.ReadAllText(tempJson);
            if (string.IsNullOrWhiteSpace(json))
                return true;

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in document.RootElement.EnumerateArray())
                    TryAppendJsonRow(item, rows);
            }
            else if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                TryAppendJsonRow(document.RootElement, rows);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            BestEffort.Try(
                () =>
                {
                    if (File.Exists(tempScript))
                        File.Delete(tempScript);
                },
                "M150-Import: Temp-Skript loeschen");
            BestEffort.Try(
                () =>
                {
                    if (File.Exists(tempJson))
                        File.Delete(tempJson);
                },
                "M150-Import: Temp-JSON loeschen");
        }
    }

    internal static void TryAppendJsonRow(
        JsonElement item,
        List<Dictionary<string, string>> rows)
    {
        if (!item.TryGetProperty("row", out var rowElement)
            || rowElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (item.TryGetProperty("table", out var table))
            row["__table"] = table.GetString() ?? string.Empty;

        foreach (var property in rowElement.EnumerateObject())
            row[property.Name] = property.Value.GetString() ?? string.Empty;

        rows.Add(row);
    }

    private static ExternalProcessRunResult RunProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout)
        => ExternalProcessRunner.RunAsync(
                fileName,
                arguments,
                timeout,
                Encoding.UTF8,
                Encoding.UTF8)
            .GetAwaiter()
            .GetResult();

    private const string PowerShellScript = """
param(
    [Parameter(Mandatory=$true)][string]$MdbPath,
    [Parameter(Mandatory=$true)][string]$OutPath
)
$ErrorActionPreference = "Stop"

function Open-Db([string]$provider, [string]$path) {
    $cs = "Provider=$provider;Data Source=$path;Persist Security Info=False;"
    $conn = New-Object System.Data.OleDb.OleDbConnection($cs)
    $conn.Open()
    return $conn
}

$conn = $null
try {
    try {
        $conn = Open-Db -provider "Microsoft.ACE.OLEDB.12.0" -path $MdbPath
    } catch {
        $conn = Open-Db -provider "Microsoft.Jet.OLEDB.4.0" -path $MdbPath
    }

    $schema = $conn.GetOleDbSchemaTable([System.Data.OleDb.OleDbSchemaGuid]::Tables, $null)
    $tables = @($schema | Where-Object { $_.TABLE_TYPE -eq "TABLE" } | ForEach-Object { [string]$_.TABLE_NAME })

    $result = New-Object System.Collections.Generic.List[object]

    foreach ($table in $tables) {
        try {
            $cmd = $conn.CreateCommand()
            $cmd.CommandText = "SELECT * FROM [$table]"
            $adapter = New-Object System.Data.OleDb.OleDbDataAdapter($cmd)
            $dt = New-Object System.Data.DataTable
            [void]$adapter.Fill($dt)

            foreach ($row in $dt.Rows) {
                $values = @{}
                foreach ($col in $dt.Columns) {
                    $name = [string]$col.ColumnName
                    $val = $row[$name]
                    if ($null -eq $val -or $val -is [System.DBNull]) {
                        $values[$name] = ""
                    } else {
                        $values[$name] = [string]$val
                    }
                }

                $result.Add([PSCustomObject]@{
                    table = $table
                    row = $values
                })
            }
        } catch {
            # Eine defekte Tabelle darf den restlichen Import nicht stoppen.
        }
    }

    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutPath -Encoding UTF8
}
finally {
    if ($conn -ne $null) { $conn.Close() }
}
""";
}

/// <summary>Kompatible Fassade für bestehende statische Importwege.</summary>
public static class M150MdbRowReader
{
    private static IM150MdbRowReader _current = new PowerShellM150MdbRowReader();

    public static IM150MdbRowReader Current => Volatile.Read(ref _current);

    public static void Use(IM150MdbRowReader reader) =>
        Volatile.Write(
            ref _current,
            reader ?? throw new ArgumentNullException(nameof(reader)));
}

using System.Text.Json;
using AuswertungPro.Next.Application.Protocol;

const string defaultArchivePath =
    @"D:\Videoprojekte\Erstfeld_Jagdmatt_38454_0426\Erstfeld_Jagdmatt_38454_0426_Export\Bin\Bin.7z";

const string defaultXtfPath =
    @"D:\Videoprojekte\Erstfeld_Jagdmatt_38454_0426\Erstfeld_Jagdmatt_38454_0426_Export\Erstfeld_Jagdmatt_38454_0426.xtf";

var options = CliOptions.Parse(args);
if (options.ShowHelp)
{
    PrintHelp();
    return 0;
}

try
{
    var archivePath = options.ArchivePath ?? defaultArchivePath;
    var outputPath = options.OutputPath;
    if (string.IsNullOrWhiteSpace(outputPath))
    {
        Console.Error.WriteLine("--output fehlt.");
        PrintHelp();
        return 2;
    }

    var ili = VsaKekCatalogArchiveReader.ReadTextEntry(archivePath, VsaKekCatalogArchiveReader.IliEntryName);
    var sectionIcm = VsaKekCatalogArchiveReader.ReadTextEntry(archivePath, VsaKekCatalogArchiveReader.SectionIcmEntryName);
    var manholeIcm = VsaKekCatalogArchiveReader.ReadTextEntry(archivePath, VsaKekCatalogArchiveReader.ManholeIcmEntryName);

    var xtfTexts = new List<string>();
    var xtfPaths = options.XtfPaths.Count > 0 ? options.XtfPaths : [defaultXtfPath];
    foreach (var xtfPath in xtfPaths)
    {
        if (!File.Exists(xtfPath))
            continue;
        xtfTexts.Add(File.ReadAllText(xtfPath));
    }

    var manifest = VsaKekCatalogBuilder.Build(ili, sectionIcm, manholeIcm, xtfTexts);
    var directory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(directory))
        Directory.CreateDirectory(directory);

    var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
    {
        WriteIndented = true
    });
    File.WriteAllText(outputPath, json);

    var channelIliCount = manifest.Codes.Count(c =>
        string.Equals(c.Source, VsaKekCatalogSources.Ili, StringComparison.OrdinalIgnoreCase)
        && c.CategoryPath.Contains("Kanal", StringComparer.OrdinalIgnoreCase));
    var observedCount = manifest.Codes.Count(c => c.IsObservedExtension);
    Console.WriteLine($"Manifest geschrieben: {outputPath}");
    Console.WriteLine($"VSA-KEK-2020-ILI Kanalcodes: {channelIliCount}");
    Console.WriteLine($"Observed XTF Extensions: {observedCount}");
    Console.WriteLine($"Gesamtcodes: {manifest.Codes.Count}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("""
        IliCatalogReader
        Baut das neutrale Sewer-Studio VSA-Code-Manifest aus VSA-KEK 2020 ILI + ICM + XTF.

        Optionen:
          --archive <Bin.7z>   VSA-KEK-2020 Bin.7z. Default ist der Erstfeld/Jagdmatt-Export.
          --xtf <file>         XTF mit beobachteten Codes. Mehrfach erlaubt.
          --output <file>      Zielpfad fuer das JSON-Manifest.
          --help               Hilfe anzeigen.
        """);
}

internal sealed record CliOptions
{
    public string? ArchivePath { get; private init; }
    public string? OutputPath { get; private init; }
    public List<string> XtfPaths { get; } = new();
    public bool ShowHelp { get; private init; }

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--help" or "-h" or "/?")
                return options with { ShowHelp = true };

            if (arg is "--archive" or "-a")
            {
                options = options with { ArchivePath = RequireValue(args, ref i, arg) };
                continue;
            }

            if (arg is "--output" or "-o")
            {
                options = options with { OutputPath = RequireValue(args, ref i, arg) };
                continue;
            }

            if (arg is "--xtf" or "-x")
            {
                options.XtfPaths.Add(RequireValue(args, ref i, arg));
                continue;
            }

            throw new ArgumentException($"Unbekannte Option: {arg}");
        }

        return options;
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"{option} braucht einen Wert.");
        index++;
        return args[index];
    }
}

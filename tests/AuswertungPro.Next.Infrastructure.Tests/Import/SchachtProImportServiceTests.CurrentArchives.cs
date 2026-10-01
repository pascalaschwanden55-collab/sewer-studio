using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.SchachtPro;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed partial class SchachtProImportServiceTests
{
    [Theory]
    [InlineData(1, 21, false)]
    [InlineData(1, 21, true)]
    [InlineData(2, 22, true)]
    [InlineData(3, 23, true)]
    public void AktuellesArchiv_Importiert_Daten_und_Fotos_AlteArchiveBleibenLesbar(int format, int schema, bool integrity)
    {
        using var temp = new TempDir();
        var files = AktuelleDateien(format, schema);
        var path = SchreibeAktuellesArchiv(temp, files, integrity ? Integritaet(files) : null);
        var originalHash = SHA256.HashData(File.ReadAllBytes(path));
        var project = new Project();
        using var staging = BeginStaging(temp);

        var result = new SchachtProImportService().ImportSchachtProArchive(path, project, Ctx(staging));

        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal(0, result.Value!.Errors);
        var shaft = Assert.Single(project.SchaechteData);
        Assert.Equal("S-NEU", shaft.GetFieldValue("Schachtnummer"));
        Assert.Equal("2683947.125", shaft.GetFieldValue("Koordinate_East"));
        Assert.NotNull(shaft.Anschluesse);
        var connection = Assert.Single(shaft.Anschluesse);
        Assert.Equal("4", connection.Uhr);
        Assert.Contains(shaft.Protocol!.Current.Entries, entry => entry.Code == "Anschluss");
        staging.Publish();
        staging.Accept();
        Assert.Equal(files["photos/XP1/0_0.jpg"], File.ReadAllBytes(Path.Combine(temp.ProjectRoot,
            shaft.GetFieldValue("Fotos").Replace('/', Path.DirectorySeparatorChar))));
        Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(path)));
    }

    [Theory]
    [InlineData("missing", "INTEGRITY_MISSING")]
    [InlineData("photo", "INTEGRITY_MISMATCH")]
    [InlineData("project", "INTEGRITY_MISMATCH")]
    [InlineData("manifest", "INTEGRITY_MISMATCH")]
    [InlineData("extra", "INTEGRITY_MISMATCH")]
    [InlineData("removed", "INTEGRITY_MISMATCH")]
    [InlineData("json", "INTEGRITY_INVALID")]
    [InlineData("algorithm", "INTEGRITY_INVALID")]
    [InlineData("hash", "INTEGRITY_INVALID")]
    [InlineData("duplicate", "INTEGRITY_INVALID")]
    [InlineData("duplicate-normalized", "INTEGRITY_INVALID")]
    [InlineData("duplicate-separator", "INTEGRITY_INVALID")]
    [InlineData("duplicate-algorithm", "INTEGRITY_INVALID")]
    [InlineData("null", "INTEGRITY_INVALID")]
    [InlineData("self", "INTEGRITY_INVALID")]
    [InlineData("traversal", "INTEGRITY_INVALID")]
    public void AktuellesArchiv_DefektePruefsummen_StoppenVorJederUebernahme(string damage, string expectedCode)
    {
        using var temp = new TempDir();
        var files = AktuelleDateien(3, 23);
        string? integrity = Integritaet(files);
        switch (damage)
        {
            case "missing": integrity = null; break;
            case "photo": files["photos/XP1/0_0.jpg"][0] ^= 1; break;
            case "project": files["projects/XP1.json"] = Encoding.UTF8.GetBytes("{}"); break;
            case "manifest": files["manifest.json"] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(files["manifest.json"]).Replace("Gemeinde", "Andere")); break;
            case "extra": files["photos/XP1/fremd.jpg"] = [1]; break;
            case "removed": files.Remove("photos/XP1/0_0.jpg"); break;
            case "json": integrity = "{"; break;
            case "algorithm": integrity = integrity.Replace("SHA-256", "MD5"); break;
            case "hash": integrity = """{"algorithm":"SHA-256","entries":{"manifest.json":"keine-Pruefsumme"}}"""; break;
            case "duplicate": integrity = IntegritaetMitPfaden("manifest.json", "manifest.json"); break;
            case "duplicate-normalized": integrity = IntegritaetMitPfaden("photos/XP1/0_0.jpg", "photos\\XP1\\0_0.jpg"); break;
            case "duplicate-separator": integrity = IntegritaetMitPfaden("photos/XP1/0_0.jpg", "photos//XP1/0_0.jpg"); break;
            case "duplicate-algorithm": integrity = integrity.Replace("\"algorithm\":", "\"algorithm\":\"SHA-256\",\"algorithm\":"); break;
            case "null": integrity = """{"algorithm":"SHA-256","entries":null}"""; break;
            case "self": integrity = IntegritaetMitPfaden("integrity.json"); break;
            case "traversal": integrity = IntegritaetMitPfaden("photos/../fremd.jpg"); break;
        }
        var path = SchreibeAktuellesArchiv(temp, files, integrity);
        var original = SHA256.HashData(File.ReadAllBytes(path));
        var project = new Project();
        var before = JsonSerializer.Serialize(project);
        using var staging = BeginStaging(temp);

        var result = new SchachtProImportService().ImportSchachtProArchive(path, project, Ctx(staging));

        Assert.False(result.Ok);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(before, JsonSerializer.Serialize(project));
        staging.Publish();
        staging.Accept();
        Assert.Empty(Directory.EnumerateFiles(temp.ProjectRoot, "*.jpg", SearchOption.AllDirectories));
        Assert.Equal(original, SHA256.HashData(File.ReadAllBytes(path)));
    }

    [Fact]
    public void AktuellesArchiv_DoppelterZipEintrag_IstNichtEindeutig()
    {
        using var temp = new TempDir();
        var files = AktuelleDateien(3, 23);
        var path = SchreibeAktuellesArchiv(temp, files, Integritaet(files));
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
            SchreibeTextEintrag(zip, "photos\\XP1\\0_0.jpg", "anderer Inhalt");
        var project = new Project();
        var result = new SchachtProImportService().ImportSchachtProArchive(path, project);
        Assert.False(result.Ok);
        Assert.Equal("DUPLICATE_ENTRY", result.ErrorCode);
        Assert.Empty(project.SchaechteData);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AktuellesArchiv_SeparatesAnschlussfoto_WirdAlsOriginalMitHinweisUebernommen(bool alsoNormalPhoto)
    {
        using var temp = new TempDir();
        var files = AktuelleDateien(3, 23);
        var protocol = """{"schachtNr":"S-NEU","connectionPhoto":{"photoPath":"photos/XP1/0_connection.jpg","photoRotation":90,"sourceRotation":0,"sourceEdit":{},"scale":1,"offsetX":0,"offsetY":0,"opacity":0.85,"visible":true,"includeInPdf":false},"photos":[]}""";
        if (alsoNormalPhoto)
            protocol = protocol.Replace("\"photos\":[]", "\"photos\":[{\"archivePath\":\"photos/XP1/0_0.jpg\"}]");
        else
            files.Remove("photos/XP1/0_0.jpg");
        files["projects/XP1.json"] = Encoding.UTF8.GetBytes(Snapshot("XP1", "Gemeinde", protocol));
        files["photos/XP1/0_connection.jpg"] = [8, 7, 6, 5];
        var path = SchreibeAktuellesArchiv(temp, files, Integritaet(files));
        using var staging = BeginStaging(temp);
        var project = new Project();
        var result = new SchachtProImportService().ImportSchachtProArchive(path, project, Ctx(staging));
        Assert.True(result.Ok, result.ErrorMessage);
        staging.Publish();
        staging.Accept();
        var photos = Assert.Single(project.SchaechteData).GetFieldValue("Fotos").Split(';');
        Assert.Equal(alsoNormalPhoto ? 2 : 1, photos.Length);
        Assert.EndsWith("0_connection.jpg", photos[^1]);
        Assert.Equal(new byte[] { 8, 7, 6, 5 }, File.ReadAllBytes(Path.Combine(temp.ProjectRoot, photos[^1])));
        if (alsoNormalPhoto)
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(Path.Combine(temp.ProjectRoot, photos[0])));
        Assert.Contains(result.Value!.Messages, message => message.Contains("Anschlussfoto") && message.Contains("Original"));
    }

    [Fact]
    public void AktuellesArchiv_ZuGrosserNachweis_WirdBegrenzt()
    {
        using var temp = new TempDir();
        var path = SchreibeAktuellesArchiv(temp, AktuelleDateien(3, 23), new string(' ', 5 * 1024 * 1024 + 1));
        var project = new Project();
        var result = new SchachtProImportService().ImportSchachtProArchive(path, project);
        Assert.False(result.Ok);
        Assert.Equal("INTEGRITY_INVALID", result.ErrorCode);
        Assert.Empty(project.SchaechteData);
    }

    [Fact]
    public void AktuellesArchiv_Abbruch_BleibtAbbruchUndUebernimmtNichts()
    {
        using var temp = new TempDir();
        var files = AktuelleDateien(3, 23);
        var path = SchreibeAktuellesArchiv(temp, files, Integritaet(files));
        var project = new Project();
        var before = JsonSerializer.Serialize(project);
        var context = new ImportRunContext(new CancellationToken(canceled: true), null, new ImportRunLog());
        Assert.Throws<OperationCanceledException>(() =>
            new SchachtProImportService().ImportSchachtProArchive(path, project, context));
        Assert.Equal(before, JsonSerializer.Serialize(project));
    }

    [Theory]
    [InlineData(0, 23)]
    [InlineData(3, 0)]
    public void AktuellesArchiv_UngueltigeVersion_IstKeinAltformat(int format, int schema)
    {
        using var temp = new TempDir();
        var files = AktuelleDateien(format, schema);
        var path = SchreibeAktuellesArchiv(temp, files, Integritaet(files));
        var result = new SchachtProImportService().ImportSchachtProArchive(path, new Project());
        Assert.False(result.Ok);
        Assert.Equal("MANIFEST_INVALID", result.ErrorCode);
    }

    private static Dictionary<string, byte[]> AktuelleDateien(int format, int schema)
    {
        var manifest = Manifest("XP1", "Gemeinde").Replace("\"formatVersion\":1", $"\"formatVersion\":{format}")
            .Replace("\"dbSchemaVersion\":21", $"\"dbSchemaVersion\":{schema}").Replace("\"photoCount\":0", "\"photoCount\":1");
        var snapshot = Snapshot("XP1", "Gemeinde", """
            {"schachtNr":"S-NEU","lv95East":2683947.125,"lv95North":1192844.5,
             "anschluesse":[{"nr":1,"typ":"Einlauf","dn":"150","tiefe":"1.80","uhr":"4","material":"PVC","zustand":{"gerissen":true}}],
             "photos":[{"archivePath":"photos/XP1/0_0.jpg","rotation":0,"edit":{},"description":"Originalfoto"}]}
            """);
        return new() { ["manifest.json"] = Encoding.UTF8.GetBytes(manifest),
            ["projects/XP1.json"] = Encoding.UTF8.GetBytes(snapshot), ["photos/XP1/0_0.jpg"] = [1, 2, 3, 4] };
    }

    private static string Integritaet(Dictionary<string, byte[]> files) => JsonSerializer.Serialize(new
    {
        algorithm = "SHA-256", entries = files.ToDictionary(x => x.Key, x => Convert.ToHexString(SHA256.HashData(x.Value)).ToLowerInvariant())
    });

    private static string IntegritaetMitPfaden(params string[] paths) => "{\"algorithm\":\"SHA-256\",\"entries\":{" +
        string.Join(",", paths.Select(path => JsonSerializer.Serialize(path) + ":\"" + new string('a', 64) + "\"")) + "}}";

    private static string SchreibeAktuellesArchiv(TempDir temp, Dictionary<string, byte[]> files, string? integrity)
    {
        var path = Path.Combine(temp.Root, "aktuell.spro");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var pair in files)
        {
            using var stream = zip.CreateEntry(pair.Key).Open();
            stream.Write(pair.Value);
        }
        if (integrity is not null) SchreibeTextEintrag(zip, "integrity.json", integrity);
        return path;
    }
}

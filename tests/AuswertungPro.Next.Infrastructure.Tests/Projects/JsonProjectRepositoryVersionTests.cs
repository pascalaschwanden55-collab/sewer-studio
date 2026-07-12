using System.Text.Json;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

public sealed class JsonProjectRepositoryVersionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "project-version-" + Guid.NewGuid().ToString("N"));

    public JsonProjectRepositoryVersionTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Load_NeuereVersion_WirdAbgelehntOhneDateiaenderung()
    {
        var path = Path.Combine(_root, "projekt.json");
        var original = """
                       { "Version": 99, "Name": "Zukunft" }
                       """;
        File.WriteAllText(path, original);

        var result = new JsonProjectRepository().Load(path);

        Assert.False(result.Ok);
        Assert.Equal("APP-VERSION", result.ErrorCode);
        Assert.Contains("neueren Programmversion", result.ErrorMessage);
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public void Save_UnbekannteProjektUndHaltungsfelder_BleibenErhalten()
    {
        var path = Path.Combine(_root, "projekt.json");
        File.WriteAllText(path, """
            {
              "Version": 2,
              "Name": "Erweiterungstest",
              "NeuesProjektFeld": { "wert": 17 },
              "Data": [
                {
                  "Id": "11111111-1111-1111-1111-111111111111",
                  "NeuesHaltungsFeld": "bleibt"
                }
              ]
            }
            """);

        var repository = new JsonProjectRepository();
        var loaded = repository.Load(path);
        Assert.True(loaded.Ok, loaded.ErrorMessage);
        Assert.True(repository.Save(loaded.Value!, path).Ok);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(17, document.RootElement.GetProperty("NeuesProjektFeld").GetProperty("wert").GetInt32());
        Assert.Equal("bleibt", document.RootElement.GetProperty("Data")[0].GetProperty("NeuesHaltungsFeld").GetString());
    }

    [Fact]
    public void Load_VersionEins_WirdAufVersionZweiMigriertUndAlsGeaendertMarkiert()
    {
        var path = Path.Combine(_root, "projekt.json");
        File.WriteAllText(path, "{ \"Version\": 1, \"Name\": \"Altprojekt\" }");

        var result = new JsonProjectRepository().Load(path);

        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal(JsonProjectRepository.CurrentVersion, result.Value!.Version);
        Assert.True(result.Value.Dirty);
    }
}

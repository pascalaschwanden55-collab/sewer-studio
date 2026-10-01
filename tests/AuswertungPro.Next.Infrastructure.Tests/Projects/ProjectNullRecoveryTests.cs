using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

public sealed class ProjectNullRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "project-null-recovery", Guid.NewGuid().ToString("N"));
    public ProjectNullRecoveryTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("null")]
    [InlineData(" \r\n null \t")]
    public void Load_JsonNull_IstKeinProjektUndBleibtUnveraendert(string json)
    {
        var path = Path.Combine(_root, "projekt.json");
        File.WriteAllText(path, json);
        var result = new JsonProjectRepository().Load(path);
        Assert.False(result.Ok);
        Assert.Equal("APP-LOAD", result.ErrorCode);
        Assert.Null(result.Value);
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void Recovery_NurNullSicherung_QuarantaeniertDieHauptdateiNicht()
    {
        var path = Path.Combine(_root, "projekt.json");
        File.WriteAllText(path, "{defekt");
        File.WriteAllText(path + ".bak", "null");
        var result = new ProjectRecoveryService().TryRecover(path, new JsonProjectRepository());
        Assert.False(result.Recovered);
        Assert.Null(result.QuarantinedPath);
        Assert.Equal("{defekt", File.ReadAllText(path));
        Assert.Equal("null", File.ReadAllText(path + ".bak"));
    }

    [Fact]
    public void Recovery_NeuesteSicherungIstNull_VerwendetAelterenGueltigenStand()
    {
        var path = Path.Combine(_root, "projekt.json");
        var backup = Path.Combine(_root, ProjectStructure.RestorePoints, "projekt", "20200101_120000", "projekt.json");
        var repository = new JsonProjectRepository();
        Assert.True(repository.Save(new Project { Name = "Gueltiger Altstand" }, backup).Ok);
        File.WriteAllText(path, "{defekt");
        File.WriteAllText(path + ".bak", "null");
        File.SetLastWriteTimeUtc(path + ".bak", new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc));

        var result = new ProjectRecoveryService().TryRecover(path, repository);

        Assert.True(result.Recovered);
        Assert.Equal("Gueltiger Altstand", result.Project?.Name);
        Assert.Equal(backup, result.RecoveredFromPath);
        Assert.Equal("{defekt", File.ReadAllText(result.QuarantinedPath!));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Version\":1,\"Name\":\"Altprojekt\"}")]
    public void Load_GueltigesLeeresAltprojekt_BleibtLesbar(string json)
    {
        var path = Path.Combine(_root, "projekt.json");
        File.WriteAllText(path, json);
        var result = new JsonProjectRepository().Load(path);
        Assert.True(result.Ok, result.ErrorMessage);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Data);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}

using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class BackupAdditionalFoldersTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "backup-folders-" + Guid.NewGuid());
    [Fact]
    public void Zusatzordner_ueberleben_Neustart_und_Dubletten_werden_entfernt()
    {
        var source = Path.Combine(_root, "GeoShop");
        new BackupAdditionalFoldersStore(_root).Save([source, source]);
        Assert.Equal([source], new BackupAdditionalFoldersStore(_root).Load());
    }
    [Fact]
    public void Relative_Pfade_ueberschreiben_keine_gueltige_Konfiguration()
    {
        var store = new BackupAdditionalFoldersStore(_root);
        var source = Path.Combine(_root, "GeoShop");
        store.Save([source]);
        Assert.Throws<ArgumentException>(() => store.Save(["relativer-ordner"]));
        Assert.Equal([source], store.Load());
    }
    [Fact]
    public void Defekte_Konfiguration_wird_nicht_still_als_leer_verwendet()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "backup-additional-folders.json"), "kaputt");
        Assert.Throws<System.Text.Json.JsonException>(() => new BackupAdditionalFoldersStore(_root).Load());
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}

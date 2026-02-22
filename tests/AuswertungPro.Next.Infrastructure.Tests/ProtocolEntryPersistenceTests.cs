using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProtocolEntryPersistenceTests
{
    [Fact]
    public void SavesAndLoads_ProtocolEntryCodeMeta_OnHaltungRecord()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungProTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "project.json");

        try
        {
            Directory.CreateDirectory(root);

            var project = new Project();
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", "H-01", FieldSource.Manual, userEdited: false);
            record.ProtocolEntry = new ProtocolEntry
            {
                Code = "BAB",
                Beschreibung = "Riss bei Anschluss",
                MeterStart = 12.4,
                MeterEnd = 12.9,
                CodeMeta = new ProtocolEntryCodeMeta
                {
                    Code = "BAB",
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["auspraegung"] = "stark"
                    },
                    Severity = "high",
                    Count = 2,
                    Notes = "Nachkontrolle noetig",
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            };
            project.Data.Add(record);

            var repo = new JsonProjectRepository();
            var save = repo.Save(project, path);
            Assert.True(save.Ok, save.ErrorMessage);

            var load = repo.Load(path);
            Assert.True(load.Ok, load.ErrorMessage);
            var loaded = Assert.Single(load.Value!.Data);

            // Legacy ProtocolEntry may be migrated to Protocol.Current during load.
            var entry = loaded.ProtocolEntry
                        ?? loaded.Protocol?.Current?.Entries.FirstOrDefault()
                        ?? loaded.Protocol?.Original?.Entries.FirstOrDefault();

            Assert.NotNull(entry);
            Assert.Equal("BAB", entry!.Code);
            Assert.NotNull(entry.CodeMeta);
            Assert.Equal("high", entry.CodeMeta!.Severity);
            Assert.Equal(2, entry.CodeMeta.Count);
            Assert.Equal("stark", entry.CodeMeta.Parameters["auspraegung"]);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}

using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageSelectedProtocolControllerTests
{
    [Fact]
    public void Refresh_does_not_merge_original_photos_into_current_entries()
    {
        var controller = new DataPageSelectedProtocolController();
        var current = new ProtocolEntry
        {
            Code = "BCCYB",
            MeterStart = 3.57,
            Beschreibung = "Bogen nach unten"
        };
        var original = new ProtocolEntry
        {
            Code = "BCCYB",
            MeterStart = 3.57,
            Beschreibung = "Bogen nach unten"
        };
        original.FotoPaths.Add("Fotos/Haltungen/H1/foto.jpg");
        var record = new HaltungRecord
        {
            Protocol = new ProtocolDocument
            {
                Original = new ProtocolRevision
                {
                    Entries = [original]
                },
                Current = new ProtocolRevision
                {
                    Entries = [current]
                }
            }
        };

        controller.Refresh(record, new InMemoryCatalog());

        var entry = Assert.Single(controller.Entries);
        Assert.Same(current, entry);
        Assert.Empty(entry.FotoPaths);
        Assert.Empty(current.FotoPaths);
    }

    [Fact]
    public void Refresh_shows_only_current_non_deleted_entries_and_enriches_short_descriptions()
    {
        var controller = new DataPageSelectedProtocolController();
        var catalog = new InMemoryCatalog(("BAB", "Rissbildung"));
        var visible = new ProtocolEntry { Code = "BAB", Beschreibung = "BAB" };
        var deleted = new ProtocolEntry { Code = "BAC", Beschreibung = "nicht sichtbar", IsDeleted = true };
        var record = new HaltungRecord
        {
            Protocol = new ProtocolDocument
            {
                Current = new ProtocolRevision
                {
                    Entries = [visible, deleted]
                }
            }
        };

        controller.Refresh(record, catalog);

        Assert.Single(controller.Entries);
        Assert.Same(visible, controller.Entries[0]);
        Assert.Equal("Rissbildung", visible.Beschreibung);
    }

    [Fact]
    public void SyncFromFindings_creates_protocol_once_and_refreshes_entries_for_selected_record()
    {
        var controller = new DataPageSelectedProtocolController();
        var catalog = new InMemoryCatalog(("BAB", "Rissbildung"));
        var record = new HaltungRecord
        {
            VsaFindings =
            [
                new VsaFinding
                {
                    KanalSchadencode = "BAB",
                    Raw = "",
                    SchadenlageAnfang = 1.25
                }
            ]
        };
        record.SetFieldValue("Haltungsname", "06.24341-35625", FieldSource.Xtf, userEdited: false);

        var refreshCount = 0;
        controller.SyncFromFindings(
            record,
            new ProtocolService(),
            code => catalog.TryGet(code, out var def) ? def.Title : null,
            refreshRecord: _ => refreshCount++,
            refreshEntries: true,
            catalog);

        Assert.NotNull(record.Protocol);
        Assert.Equal(1, refreshCount);
        Assert.Single(controller.Entries);
        Assert.Equal("BAB", controller.Entries[0].Code);
        Assert.Equal("Rissbildung", controller.Entries[0].Beschreibung);

        controller.SyncFromFindings(
            record,
            new ProtocolService(),
            code => catalog.TryGet(code, out var def) ? def.Title : null,
            refreshRecord: _ => refreshCount++,
            refreshEntries: true,
            catalog);

        Assert.Equal(1, refreshCount);
        Assert.Single(controller.Entries);
    }

    private sealed class InMemoryCatalog(params (string Code, string Title)[] entries) : ICodeCatalogProvider
    {
        private readonly Dictionary<string, CodeDefinition> _codes = entries.ToDictionary(
            item => item.Code,
            item => new CodeDefinition { Code = item.Code, Title = item.Title },
            StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<CodeDefinition> GetAll() => _codes.Values.ToList();

        public bool TryGet(string code, out CodeDefinition def)
        {
            var ok = _codes.TryGetValue(code, out var found);
            def = found ?? new CodeDefinition();
            return ok;
        }

        public void Save(IReadOnlyList<CodeDefinition> codes) { }

        public IReadOnlyList<string> AllowedCodes() => _codes.Keys.ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => [];
    }
}

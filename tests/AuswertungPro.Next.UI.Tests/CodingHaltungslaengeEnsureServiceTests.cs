using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingHaltungslaengeEnsureServiceTests
{
    [Fact]
    public void Ensure_returns_when_known_sources_resolve_length()
    {
        var record = new HaltungRecord();
        var service = new CodingHaltungslaengeEnsureService(
            tryEnsureFromKnownSources: (actualRecord, pipeLength) =>
            {
                Assert.Same(record, actualRecord);
                Assert.Equal(12.5, pipeLength);
                return true;
            },
            askForLength: () => throw new InvalidOperationException("Prompt must not open."));

        service.Ensure(record, 12.5);

        Assert.Equal("", record.GetFieldValue("Haltungslaenge_m"));
    }

    [Fact]
    public void Ensure_accepts_manual_comma_input_and_writes_invariant_length()
    {
        var record = new HaltungRecord();
        var service = new CodingHaltungslaengeEnsureService(
            tryEnsureFromKnownSources: (_, _) => false,
            askForLength: () => "45,3");

        service.Ensure(record, null);

        Assert.Equal("45.30", record.GetFieldValue("Haltungslaenge_m"));
        Assert.True(record.FieldMeta.TryGetValue("Haltungslaenge_m", out var meta));
        Assert.Equal(FieldSource.Manual, meta.Source);
        Assert.True(meta.UserEdited);
    }

    [Fact]
    public void Ensure_ignores_empty_invalid_and_non_positive_manual_input()
    {
        foreach (var input in new[] { "", "abc", "0", "-1" })
        {
            var record = new HaltungRecord();
            var service = new CodingHaltungslaengeEnsureService(
                tryEnsureFromKnownSources: (_, _) => false,
                askForLength: () => input);

            service.Ensure(record, null);

            Assert.Equal("", record.GetFieldValue("Haltungslaenge_m"));
        }
    }

    [Fact]
    public void Factory_creates_service()
    {
        var service = CodingHaltungslaengeEnsureServiceFactory.Create();

        Assert.NotNull(service);
    }
}

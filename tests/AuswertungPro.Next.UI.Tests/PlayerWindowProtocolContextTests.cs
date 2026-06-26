using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowProtocolContextTests
{
    [Fact]
    public void From_stores_dependencies_haltung_and_callback()
    {
        var record = new HaltungRecord();
        var context = PlayerWindowProtocolContext.From(
            serviceProvider: null,
            haltungId: "H-42",
            onEntryCreated: _ => { },
            haltungRecord: record);

        Assert.NotNull(context.Dependencies);
        Assert.Equal("H-42", context.HaltungId);
        Assert.Same(record, context.HaltungRecord);
        Assert.True(context.HasHaltungRecord);
    }

    [Fact]
    public void NotifyEntryCreated_invokes_optional_callback()
    {
        var entry = new ProtocolEntry();
        ProtocolEntry? received = null;
        var context = PlayerWindowProtocolContext.From(
            serviceProvider: null,
            haltungId: null,
            onEntryCreated: created => received = created,
            haltungRecord: null);

        context.NotifyEntryCreated(entry);

        Assert.Same(entry, received);
        Assert.False(context.HasHaltungRecord);
    }

    [Fact]
    public void From_exposes_dependency_facade_for_player_partials()
    {
        var context = PlayerWindowProtocolContext.From(
            serviceProvider: null,
            haltungId: null,
            onEntryCreated: null,
            haltungRecord: null);

        Assert.Null(context.Settings);
        Assert.Null(context.CodeCatalog);
        Assert.Null(context.CodeSelectionCatalog);
        Assert.Null(context.PipelineConfig);
        Assert.Null(context.ProtocolPdfExporter);
        Assert.Null(context.LoggerFactory);
        Assert.Null(context.LastProjectPath);
        Assert.Null(context.LegacyServiceProvider);
        Assert.False(context.HasCodeCatalog);
    }
}

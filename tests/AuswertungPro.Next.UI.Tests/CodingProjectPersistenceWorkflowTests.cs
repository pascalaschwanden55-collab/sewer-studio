using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProjectPersistenceWorkflowTests
{
    [Fact]
    public void MarkProjectDirty_offers_default_service_wiring()
    {
        var overload = typeof(CodingProjectPersistenceWorkflow)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(CodingProjectPersistenceWorkflow.MarkProjectDirty) &&
                method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual([typeof(HaltungRecord)]));

        Assert.NotNull(overload);
    }

    [Fact]
    public void TrySaveProjectIfReady_offers_default_service_wiring()
    {
        var overload = typeof(CodingProjectPersistenceWorkflow)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(CodingProjectPersistenceWorkflow.TrySaveProjectIfReady) &&
                method.GetParameters().Length == 0);

        Assert.NotNull(overload);
    }

    [Fact]
    public void MarkProjectDirty_creates_service_and_marks_record()
    {
        var calls = new List<string>();
        var record = new HaltungRecord();
        var service = new CodingProjectPersistenceService(
            markProjectDirty: actualRecord =>
            {
                Assert.Same(record, actualRecord);
                calls.Add("dirty");
                return true;
            },
            trySaveProjectIfReady: () => throw new InvalidOperationException("Save must not run."),
            utcNow: () => throw new InvalidOperationException("Clock must not be read."));

        CodingProjectPersistenceWorkflow.MarkProjectDirty(
            record,
            new CodingProjectPersistenceWorkflowActions(
                CreateService: () =>
                {
                    calls.Add("service");
                    return service;
                }));

        Assert.Equal(["service", "dirty"], calls);
    }

    [Fact]
    public void TrySaveProjectIfReady_creates_service_and_saves_ready_project()
    {
        var calls = new List<string>();
        var service = new CodingProjectPersistenceService(
            markProjectDirty: _ => throw new InvalidOperationException("Dirty must not run."),
            trySaveProjectIfReady: () => calls.Add("save"),
            utcNow: () => throw new InvalidOperationException("Clock must not be read."));

        CodingProjectPersistenceWorkflow.TrySaveProjectIfReady(
            new CodingProjectPersistenceWorkflowActions(
                CreateService: () =>
                {
                    calls.Add("service");
                    return service;
                }));

        Assert.Equal(["service", "save"], calls);
    }
}

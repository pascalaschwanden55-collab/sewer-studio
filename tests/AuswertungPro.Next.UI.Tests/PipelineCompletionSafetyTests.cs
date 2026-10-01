using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PipelineCompletionSafetyTests
{
    [Theory]
    [InlineData(251, false)]
    [InlineData(1001, false)]
    [InlineData(251, true)]
    [InlineData(1001, true)]
    public void Alle_Befunde_bleiben_auswaehlbar_und_der_letzte_ist_uebernehmbar(int count, bool onlyLast)
    {
        var mapped = Enumerable.Range(0, count).Select(index => new MappedProtocolEntry(
            new RawVideoDetection($"Befund {index}", index, index, "mid"),
            "BAB", 0.9, "Test", [],
            Freigabe: new AiDecision(AiDecisionOutcome.AutoAccept, "Test"), EntryId: Guid.NewGuid())).ToArray();
        var document = new ProtocolDocument { Original = new ProtocolRevision(), Current = new ProtocolRevision() };
        foreach (var entry in mapped)
            foreach (var revision in new[] { document.Original, document.Current })
                revision.Entries.Add(new ProtocolEntry
                {
                    EntryId = entry.EntryId, Code = "BAB", Source = ProtocolEntrySource.Ai,
                    Ai = new ProtocolEntryAiMeta { SuggestedCode = "BAB" }
                });
        var result = new PipelineResult(document, mapped.Select(x => x.Detection).ToArray(), mapped, null, [], null);

        var presentation = PipelineResultPresenter.ApplySuccessful(new VideoAnalysisPipelineViewModel(), result);

        Assert.Equal(count, presentation.VisibleDetections.Count);
        Assert.Equal(mapped[^1].EntryId, presentation.VisibleDetections[^1].EntryId);
        Assert.Equal(count, document.Current.Entries.Count);
        foreach (var item in presentation.VisibleDetections)
            item.IsSelected = !onlyLast || item.EntryId == mapped[^1].EntryId;
        var selected = presentation.VisibleDetections.Where(x => x.IsSelected).Select(x => x.EntryId).ToHashSet();
        AiProtocolAcceptancePolicy.Apply(document, selected);

        foreach (var revision in new[] { document.Original, document.Current })
        {
            Assert.Equal(onlyLast ? 1 : count, revision.Entries.Count);
            Assert.Contains(revision.Entries, x => x.EntryId == mapped[^1].EntryId && x.Ai!.Accepted);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Warnungen_oder_Unvollstaendigkeit_erhalten_einen_eindeutigen_Abschluss(bool incomplete)
    {
        var vm = new VideoAnalysisPipelineViewModel();
        var result = new PipelineResult(null, [], [], null,
            incomplete ? [] : ["SAM: Masken gingen verloren."], null, Incomplete: incomplete);

        PipelineResultPresenter.ApplySuccessful(vm, result);

        Assert.Contains(incomplete ? "Unvollständig" : "Hinweise", vm.StatusText);
        Assert.DoesNotContain("Du kannst jetzt übertragen", vm.StatusText);
        Assert.Equal(incomplete ? "Unvollständig" : "Fertig mit Hinweisen", vm.PhaseLabel);
        Assert.True(vm.HasResultWarnings);
        Assert.Contains(incomplete ? "unvollständig" : "SAM: Masken gingen verloren.", vm.ResultWarningText);
    }

    [Fact]
    public void Spaete_Fortschrittsmeldung_verdeckt_den_Abschluss_nicht()
    {
        var vm = new VideoAnalysisPipelineViewModel();
        var mapper = new PipelineProgressMapper(vm, [], _ => throw new InvalidOperationException());
        PipelineResultPresenter.ApplySuccessful(vm,
            new PipelineResult(null, [], [], null, ["Qwen war ausgefallen."], null, Incomplete: true));
        vm.IsDone = true;
        var completion = vm.StatusText;

        mapper.Apply(new PipelineProgress(PipelinePhase.Done, 100, "Fertig"));

        Assert.Equal(completion, vm.StatusText);
        Assert.Equal("Unvollständig", vm.PhaseLabel);
        Assert.Contains("Qwen war ausgefallen.", vm.ResultWarningText);
    }

    [Fact]
    public void Neuer_Lauf_und_gesunder_Abschluss_entfernen_alte_Warnungen()
    {
        var vm = new VideoAnalysisPipelineViewModel();
        var warning = new PipelineResult(null, [], [], null, ["Warnung", " Warnung ", " "], null);
        PipelineResultPresenter.ApplySuccessful(vm, warning);
        Assert.Equal("Warnung", vm.ResultWarningText);
        vm.Reset();
        Assert.False(vm.HasResultWarnings);
        Assert.Empty(vm.ResultWarningText);
        PipelineResultPresenter.ApplySuccessful(vm, warning);
        PipelineResultPresenter.ApplySuccessful(vm, warning with { Warnings = [] });
        Assert.False(vm.HasResultWarnings);
        Assert.Equal("Fertig", vm.PhaseLabel);
    }
}

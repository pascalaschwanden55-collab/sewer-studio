using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingStepWorkflowRequestFactoryTests
{
    [Fact]
    public void Create_verdrahtet_self_training_step_request()
    {
        var calls = new List<string>();
        var step = new SelfTrainingStep(
            1,
            2,
            "BBA",
            3.4,
            SelfTrainingStage.ExtractingFrame,
            null,
            null,
            "frame.jpg");
        var tracker = new SelfTrainingMatchRateTracker();
        var results = new List<SelfTrainingEntryResult>();

        var request = SelfTrainingStepWorkflowRequestFactory.Create(
            new SelfTrainingStepWorkflowRequestFactoryRequest(
                Step: step,
                ActiveVisionModel: "vision",
                OnUi: action =>
                {
                    calls.Add("ui");
                    action();
                },
                SetPipelineActiveStep: value => calls.Add($"active-step:{value}"),
                SetCurrentEntryCode: value => calls.Add($"code:{value}"),
                SetCurrentEntryMeter: value => calls.Add($"meter:{value}"),
                SetProgressValue: value => calls.Add($"progress:{value}"),
                SetProgressMax: value => calls.Add($"max:{value}"),
                SetActiveModelName: value => calls.Add($"model:{value}"),
                SetIsModelActive: value => calls.Add($"model-active:{value}"),
                SetCurrentTechniqueGrade: value => calls.Add($"grade:{value}"),
                SetCurrentTechniqueDetails: value => calls.Add($"details:{value}"),
                SetCurrentComparisonText: value => calls.Add($"comparison:{value}"),
                Log: value => calls.Add($"log:{value}"),
                SetLiveFrame: value => calls.Add($"frame:{value}"),
                MatchRateTracker: tracker,
                RefreshMatchRatePercents: () => calls.Add("refresh-match-rate"),
                Results: results,
                UpdateCodeDistribution: (code, level) => calls.Add($"distribution:{code}:{level}")));

        Assert.Same(step, request.Step);
        Assert.Equal("vision", request.ActiveVisionModel);
        Assert.Same(tracker, request.MatchRateTracker);
        Assert.Same(results, request.Results);

        request.OnUi(() =>
        {
            request.Ui.SetPipelineActiveStep(7);
            request.Ui.SetCurrentEntryCode("BBB");
            request.Ui.SetCurrentEntryMeter(8.9);
            request.Ui.SetProgressValue(2);
            request.Ui.SetProgressMax(4);
            request.Ui.SetActiveModelName("model");
            request.Ui.SetIsModelActive(true);
            request.Ui.SetCurrentTechniqueGrade("A");
            request.Ui.SetCurrentTechniqueDetails("gut");
            request.Ui.SetCurrentComparisonText("match");
            request.Ui.Log("log");
            request.Ui.SetLiveFrame("next.jpg");
            request.RefreshMatchRatePercents();
            request.UpdateCodeDistribution("BBB", MatchLevel.ExactMatch);
        });

        Assert.Equal(
            [
                "ui",
                "active-step:7",
                "code:BBB",
                "meter:8.9",
                "progress:2",
                "max:4",
                "model:model",
                "model-active:True",
                "grade:A",
                "details:gut",
                "comparison:match",
                "log:log",
                "frame:next.jpg",
                "refresh-match-rate",
                "distribution:BBB:ExactMatch"
            ],
            calls);
    }
}

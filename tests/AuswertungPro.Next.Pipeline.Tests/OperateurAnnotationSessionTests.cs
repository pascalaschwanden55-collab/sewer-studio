using AuswertungPro.Next.Application.Ai.Annotation;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class OperateurAnnotationSessionTests
{
    [Fact]
    public void Session_NewlyConstructed_HasEmptyTasks()
    {
        var session = new OperateurAnnotationSession
        {
            CaseId = "case-1",
            VideoPath = "video.mp4",
            PdfPath = "protocol.pdf"
        };
        Assert.Empty(session.Tasks);
        Assert.Null(session.Active);
    }

    [Fact]
    public void CodeTask_DefaultState_IsPending()
    {
        var task = new CodeTask { Code = "BAB B", Meterstand = 12.3 };
        Assert.Equal(CodeTaskState.Pending, task.State);
    }

    [Fact]
    public void CodeTask_TransitionToActive_ClearsBoxAndPreview()
    {
        var task = new CodeTask
        {
            Code = "BAB B",
            Meterstand = 12.3,
            State = CodeTaskState.PreviewReady,
            Box = new BoundingBoxNormalized(0.5, 0.5, 0.1, 0.1)
        };

        task.State = CodeTaskState.Active;
        task.Box = null;
        task.Preview = null;

        Assert.Equal(CodeTaskState.Active, task.State);
        Assert.Null(task.Box);
        Assert.Null(task.Preview);
    }

    [Fact]
    public void CodeTask_Committed_SetsSampleIdAndUtc()
    {
        var task = new CodeTask
        {
            Code = "BAB B",
            Meterstand = 12.3,
            State = CodeTaskState.Committed,
            CommittedSampleId = "sample-xyz",
            CommittedUtc = System.DateTime.UtcNow
        };

        Assert.Equal("sample-xyz", task.CommittedSampleId);
        Assert.NotNull(task.CommittedUtc);
    }

    [Fact]
    public void CodeTask_Skipped_StoresUserReason()
    {
        var task = new CodeTask
        {
            Code = "BBB Z",
            Meterstand = 24.1,
            State = CodeTaskState.Skipped,
            UserReason = "Frame komplett unscharf"
        };

        Assert.Equal(CodeTaskState.Skipped, task.State);
        Assert.Equal("Frame komplett unscharf", task.UserReason);
    }

    [Fact]
    public void CodeTaskState_AllValuesDefined()
    {
        var states = new[]
        {
            CodeTaskState.Pending,
            CodeTaskState.Active,
            CodeTaskState.PreviewReady,
            CodeTaskState.Committed,
            CodeTaskState.Skipped,
            CodeTaskState.Rejected,
            CodeTaskState.Error
        };
        Assert.Equal(7, states.Length);
    }

    [Fact]
    public void Session_Active_PointsToTaskInTasks()
    {
        var task = new CodeTask { Code = "BAB B", Meterstand = 12.3 };
        var session = new OperateurAnnotationSession { CaseId = "c1" };
        session.Tasks.Add(task);
        session.Active = task;

        Assert.Same(task, session.Active);
        Assert.Single(session.Tasks);
    }
}

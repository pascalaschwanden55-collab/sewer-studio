using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionConfirmationTrainingWorkflowTests
{
    [Fact]
    public async Task SaveAcceptedAsync_uses_pre_captured_frame_and_saves_all_findings()
    {
        var findings = new[]
        {
            new LiveFrameFinding("Riss", 3, "3", 20, VsaCodeHint: "BAB"),
            new LiveFrameFinding("Wurzel", 2, "6", 10, VsaCodeHint: "BBA")
        };
        var frameBytes = new byte[] { 1, 2, 3 };
        var saved = new List<string>();
        var method = FindSaveAcceptedMethod();
        Assert.NotNull(method);

        var result = await InvokeAsync(method, [
            findings,
            12d,
            frameBytes,
            new Func<Task<byte[]?>>(() => throw new InvalidOperationException("Pre-captured frame should be used.")),
            new Func<byte[], LiveFrameFinding, TimeSpan, Task<TeacherAnnotation>>((bytes, finding, time) =>
            {
                Assert.Same(frameBytes, bytes);
                Assert.Equal(TimeSpan.FromSeconds(12), time);
                saved.Add(finding.VsaCodeHint!);
                return Task.FromResult(new TeacherAnnotation());
            })
        ]);

        AssertResult(result, saved: true, savedCount: 2, code: null);
        Assert.Equal(["BAB", "BBA"], saved);
    }

    [Fact]
    public async Task SaveAcceptedAsync_captures_frame_when_pending_frame_is_missing()
    {
        var captured = new byte[] { 9 };
        var captureCalls = 0;
        var method = FindSaveAcceptedMethod();
        Assert.NotNull(method);

        var result = await InvokeAsync(method, [
            new[] { new LiveFrameFinding("Riss", 3, "3", 20, VsaCodeHint: "BAB") },
            3d,
            null,
            new Func<Task<byte[]?>>(() =>
            {
                captureCalls++;
                return Task.FromResult<byte[]?>(captured);
            }),
            new Func<byte[], LiveFrameFinding, TimeSpan, Task<TeacherAnnotation>>((bytes, _, _) =>
            {
                Assert.Same(captured, bytes);
                return Task.FromResult(new TeacherAnnotation());
            })
        ]);

        AssertResult(result, saved: true, savedCount: 1, code: null);
        Assert.Equal(1, captureCalls);
    }

    [Fact]
    public async Task SaveAcceptedAsync_returns_false_without_findings_or_frame()
    {
        var writerCalls = 0;
        var method = FindSaveAcceptedMethod();
        Assert.NotNull(method);
        var save = new Func<byte[], LiveFrameFinding, TimeSpan, Task<TeacherAnnotation>>((_, _, _) =>
        {
            writerCalls++;
            return Task.FromResult(new TeacherAnnotation());
        });

        var noFindings = await InvokeAsync(method, [
            Array.Empty<LiveFrameFinding>(),
            3d,
            new byte[] { 1 },
            new Func<Task<byte[]?>>(() => Task.FromResult<byte[]?>([1])),
            save
        ]);
        var noFrame = await InvokeAsync(method, [
            new[] { new LiveFrameFinding("Riss", 3, "3", 20, VsaCodeHint: "BAB") },
            3d,
            null,
            new Func<Task<byte[]?>>(() => Task.FromResult<byte[]?>(null)),
            save
        ]);

        AssertResult(noFindings, saved: false, savedCount: 0, code: null);
        AssertResult(noFrame, saved: false, savedCount: 0, code: null);
        Assert.Equal(0, writerCalls);
    }

    [Fact]
    public async Task SaveCorrectedAsync_saves_primary_finding_with_selected_entry()
    {
        var findings = new[]
        {
            new LiveFrameFinding("KI", 3, "3", 20, VsaCodeHint: "BAB"),
            new LiveFrameFinding("Ignored", 2, "6", 20, VsaCodeHint: "BBA")
        };
        var selectedEntry = new ProtocolEntry { Code = "BCA", Beschreibung = "Anschluss" };
        var frameBytes = new byte[] { 4, 5 };
        var saved = new List<string>();
        var method = FindSaveCorrectedMethod();
        Assert.NotNull(method);

        var result = await InvokeAsync(method, [
            findings,
            selectedEntry,
            8d,
            frameBytes,
            new Func<Task<byte[]?>>(() => throw new InvalidOperationException("Pre-captured frame should be used.")),
            new Func<byte[], LiveFrameFinding, ProtocolEntry, TimeSpan, Task<TeacherAnnotation>>(
                (bytes, finding, entry, time) =>
                {
                    Assert.Same(frameBytes, bytes);
                    Assert.Same(findings[0], finding);
                    Assert.Same(selectedEntry, entry);
                    Assert.Equal(TimeSpan.FromSeconds(8), time);
                    saved.Add(entry.Code!);
                    return Task.FromResult(new TeacherAnnotation());
                })
        ]);

        AssertResult(result, saved: true, savedCount: 1, code: "BCA");
        Assert.Equal(["BCA"], saved);
    }

    [Fact]
    public async Task SaveCorrectedAsync_returns_false_without_selection_findings_or_frame()
    {
        var method = FindSaveCorrectedMethod();
        Assert.NotNull(method);
        var save = new Func<byte[], LiveFrameFinding, ProtocolEntry, TimeSpan, Task<TeacherAnnotation>>(
            (_, _, _, _) => throw new InvalidOperationException("Writer must not be called."));

        var noSelection = await InvokeAsync(method, [
            new[] { new LiveFrameFinding("KI", 3, "3", 20, VsaCodeHint: "BAB") },
            null,
            8d,
            new byte[] { 1 },
            new Func<Task<byte[]?>>(() => Task.FromResult<byte[]?>([1])),
            save
        ]);
        var noFindings = await InvokeAsync(method, [
            Array.Empty<LiveFrameFinding>(),
            new ProtocolEntry { Code = "BCA" },
            8d,
            new byte[] { 1 },
            new Func<Task<byte[]?>>(() => Task.FromResult<byte[]?>([1])),
            save
        ]);
        var noFrame = await InvokeAsync(method, [
            new[] { new LiveFrameFinding("KI", 3, "3", 20, VsaCodeHint: "BAB") },
            new ProtocolEntry { Code = "BCA" },
            8d,
            null,
            new Func<Task<byte[]?>>(() => Task.FromResult<byte[]?>(null)),
            save
        ]);

        AssertResult(noSelection, saved: false, savedCount: 0, code: null);
        AssertResult(noFindings, saved: false, savedCount: 0, code: null);
        AssertResult(noFrame, saved: false, savedCount: 0, code: null);
    }

    private static MethodInfo? FindSaveAcceptedMethod()
        => WorkflowType?.GetMethod(
            "SaveAcceptedAsync",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types:
            [
                typeof(IReadOnlyList<LiveFrameFinding>),
                typeof(double),
                typeof(byte[]),
                typeof(Func<Task<byte[]>>),
                typeof(Func<byte[], LiveFrameFinding, TimeSpan, Task<TeacherAnnotation>>)
            ],
            modifiers: null);

    private static MethodInfo? FindSaveCorrectedMethod()
        => WorkflowType?.GetMethod(
            "SaveCorrectedAsync",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types:
            [
                typeof(IReadOnlyList<LiveFrameFinding>),
                typeof(ProtocolEntry),
                typeof(double),
                typeof(byte[]),
                typeof(Func<Task<byte[]>>),
                typeof(Func<byte[], LiveFrameFinding, ProtocolEntry, TimeSpan, Task<TeacherAnnotation>>)
            ],
            modifiers: null);

    private static Type? WorkflowType
        => typeof(LiveDetectionTrainingAnnotationWriter).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.LiveDetectionConfirmationTrainingWorkflow");

    private static async Task<object?> InvokeAsync(MethodInfo method, object?[] args)
    {
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(null, args));
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task);
    }

    private static void AssertResult(object? result, bool saved, int savedCount, string? code)
    {
        Assert.NotNull(result);
        var type = result.GetType();
        Assert.Equal(saved, type.GetProperty("Saved")?.GetValue(result));
        Assert.Equal(savedCount, type.GetProperty("SavedCount")?.GetValue(result));
        Assert.Equal(code, type.GetProperty("Code")?.GetValue(result));
    }
}

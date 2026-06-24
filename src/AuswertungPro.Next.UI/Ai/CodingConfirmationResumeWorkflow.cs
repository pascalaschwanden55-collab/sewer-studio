using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public readonly record struct CodingConfirmationResumeResult(
    bool ResumedSession,
    bool IsLiveAiEnabled,
    CodingLiveAiStatusState Status);

public static class CodingConfirmationResumeWorkflow
{
    public static CodingConfirmationResumeResult Apply(
        ICodingSessionService? codingSessionService,
        bool isLiveAiEnabled,
        string modelName,
        Action<bool> setPause)
    {
        ArgumentNullException.ThrowIfNull(setPause);

        var resumedSession = false;
        if (codingSessionService?.ActiveSession?.State == CodingSessionState.WaitingForUserInput)
        {
            codingSessionService.ResumeSession();
            resumedSession = true;
        }

        PlayerConfirmationPlayback.ResumeCodingLiveAi(isLiveAiEnabled, setPause);

        var status = CodingLiveAiButtonDisplayPolicy.BuildStatus(
            isLiveAiEnabled,
            LiveDetectionDisplayPolicy.CompactModelName(modelName));

        return new CodingConfirmationResumeResult(resumedSession, isLiveAiEnabled, status);
    }
}

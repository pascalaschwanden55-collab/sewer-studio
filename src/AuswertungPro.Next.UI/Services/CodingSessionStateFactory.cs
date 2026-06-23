using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Services;

public sealed record CodingSessionStateComponents(
    ICodingSessionService SessionService,
    IOverlayToolService OverlayService,
    CodingSessionViewModel ViewModel);

public static class CodingSessionStateFactory
{
    public static CodingSessionStateComponents Create(string videoPath, AppSettings? settings = null)
        => Create(
            CodingSessionServiceFactory.Create(settings),
            new OverlayToolService(),
            new CodingFeedbackRecorder(),
            videoPath);

    public static CodingSessionStateComponents Create(
        ICodingSessionService sessionService,
        IOverlayToolService overlayService,
        ICodingFeedbackRecorder? feedbackRecorder,
        string videoPath)
    {
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(overlayService);

        var viewModel = new CodingSessionViewModel(sessionService, overlayService, feedbackRecorder)
        {
            VideoPath = videoPath
        };

        return new CodingSessionStateComponents(sessionService, overlayService, viewModel);
    }
}

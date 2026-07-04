using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingApprovedProtocolExportRequestFactoryRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    IEnumerable<TrainingSample> Samples,
    Func<TrainingSample, bool> IsExportEligible,
    Func<Task> PersistSamplesAsync,
    Func<DateTime> UtcNow,
    Action<string> Log,
    Action<string> SetStatusText);

public sealed record TrainingApprovedProtocolExportDefaultRequestFactoryRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    IEnumerable<TrainingSample> Samples,
    Func<TrainingSample, bool> IsExportEligible,
    Func<Task> PersistSamplesAsync,
    Action<string> Log,
    Action<string> SetStatusText);

public sealed record TrainingApprovedProtocolExportRequestFactoryDefaults(
    Action<ProtocolEntry, string?> AddProtocolTrainingSample,
    string TargetPath);

public static class TrainingApprovedProtocolExportRequestFactory
{
    public static TrainingApprovedProtocolExportWorkflowRequest CreateWithDefaults(
        TrainingApprovedProtocolExportDefaultRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return CreateWithDefaults(new TrainingApprovedProtocolExportRequestFactoryRequest(
            request.GetIsBusy,
            request.SetIsBusy,
            request.Samples,
            request.IsExportEligible,
            request.PersistSamplesAsync,
            UtcNow: () => DateTime.UtcNow,
            request.Log,
            request.SetStatusText));
    }

    public static TrainingApprovedProtocolExportWorkflowRequest CreateWithDefaults(
        TrainingApprovedProtocolExportRequestFactoryRequest request)
        => Create(
            request,
            new TrainingApprovedProtocolExportRequestFactoryDefaults(
                ProtocolTrainingStore.AddSample,
                ProtocolTrainingStore.DefaultPath));

    public static TrainingApprovedProtocolExportWorkflowRequest Create(
        TrainingApprovedProtocolExportRequestFactoryRequest request,
        TrainingApprovedProtocolExportRequestFactoryDefaults defaults)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.GetIsBusy);
        ArgumentNullException.ThrowIfNull(request.SetIsBusy);
        ArgumentNullException.ThrowIfNull(request.Samples);
        ArgumentNullException.ThrowIfNull(request.IsExportEligible);
        ArgumentNullException.ThrowIfNull(request.PersistSamplesAsync);
        ArgumentNullException.ThrowIfNull(request.UtcNow);
        ArgumentNullException.ThrowIfNull(request.Log);
        ArgumentNullException.ThrowIfNull(request.SetStatusText);
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(defaults.AddProtocolTrainingSample);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaults.TargetPath);

        return new TrainingApprovedProtocolExportWorkflowRequest(
            request.GetIsBusy,
            request.SetIsBusy,
            request.Samples,
            request.IsExportEligible,
            defaults.AddProtocolTrainingSample,
            request.PersistSamplesAsync,
            request.UtcNow,
            defaults.TargetPath,
            request.Log,
            request.SetStatusText);
    }
}

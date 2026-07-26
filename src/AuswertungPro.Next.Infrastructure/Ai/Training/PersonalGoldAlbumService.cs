using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Baut den rein lesenden Album-Schnappschuss aus dem aktuellen TrainingSample-Bestand.
/// Fremde, automatische und nicht bestaetigte Daten bleiben draussen.
/// </summary>
public sealed class PersonalGoldAlbumService(
    ITrainingSampleStore sampleStore,
    Func<string, bool>? fileExists = null) : IPersonalGoldAlbumService
{
    private readonly ITrainingSampleStore _sampleStore =
        sampleStore ?? throw new ArgumentNullException(nameof(sampleStore));
    private readonly Func<string, bool> _fileExists = fileExists ?? File.Exists;

    public async Task<PersonalGoldAlbumSnapshot> LoadAsync(
        string confirmedByUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmedByUser);
        cancellationToken.ThrowIfCancellationRequested();
        var samples = await _sampleStore.LoadAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var items = samples
            // Eigene Entwuerfe (Draft) bleiben im Album als "unvollstaendig" sichtbar.
            .Where(sample =>
                ManualGoldTrainingPolicy.IsManuallyConfirmed(sample, confirmedByUser)
                || GoldDraftMatcher.IsOwnDraft(sample, confirmedByUser))
            .Select(ToAlbumItem)
            .OrderBy(item => item.MainCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(item => item.ConfirmedAtUtc ?? DateTime.MinValue)
            .ThenBy(item => item.SampleId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var groups = items
            .GroupBy(item => item.MainCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PersonalGoldAlbumGroup(
                group.Key,
                group.Count(item => item.IsFullGold),
                group.ToArray()))
            .OrderBy(group => group.MainCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new PersonalGoldAlbumSnapshot(
            groups,
            items.Length,
            items.Count(item => item.IsFullGold),
            items.Count(item => !item.IsFullGold),
            items.Count(item => !item.FileExists));
    }

    private PersonalGoldAlbumItem ToAlbumItem(TrainingSample sample)
        => new(
            sample.SampleId,
            NormalizeMainCode(sample.Code),
            (sample.Code ?? string.Empty).Trim().ToUpperInvariant(),
            sample.Beschreibung?.Trim() ?? string.Empty,
            sample.FramePath ?? string.Empty,
            sample.ConfirmedAtUtc,
            ManualGoldTrainingPolicy.HasValidGoldBox(sample),
            ManualGoldTrainingPolicy.HasValidGoldSegmentation(sample),
            !string.IsNullOrWhiteSpace(sample.FramePath) && _fileExists(sample.FramePath));

    private static string NormalizeMainCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "OHNE CODE";

        var normalized = code.Trim().Replace(".", string.Empty).ToUpperInvariant();
        return normalized.Length >= 3 ? normalized[..3] : normalized;
    }
}

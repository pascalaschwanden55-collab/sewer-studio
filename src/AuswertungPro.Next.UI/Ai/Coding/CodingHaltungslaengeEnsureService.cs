using System;
using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingHaltungslaengeEnsureService
{
    private readonly Func<HaltungRecord, double?, bool> _tryEnsureFromKnownSources;
    private readonly Func<string> _askForLength;

    public CodingHaltungslaengeEnsureService(
        Func<HaltungRecord, double?, bool> tryEnsureFromKnownSources,
        Func<string> askForLength)
    {
        _tryEnsureFromKnownSources = tryEnsureFromKnownSources ?? throw new ArgumentNullException(nameof(tryEnsureFromKnownSources));
        _askForLength = askForLength ?? throw new ArgumentNullException(nameof(askForLength));
    }

    public void Ensure(HaltungRecord record, double? overlayPipeLengthMeters)
    {
        if (_tryEnsureFromKnownSources(record, overlayPipeLengthMeters))
            return;

        var input = _askForLength();
        if (string.IsNullOrWhiteSpace(input))
            return;

        var normalized = input.Trim().Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value <= 0)
            return;

        record.SetFieldValue(
            "Haltungslaenge_m",
            value.ToString("F2", CultureInfo.InvariantCulture),
            FieldSource.Manual,
            userEdited: true);
    }
}

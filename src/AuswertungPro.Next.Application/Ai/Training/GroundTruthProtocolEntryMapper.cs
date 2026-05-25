using System;
using System.Collections.Generic;
using System.Globalization;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai.Training;

public static class GroundTruthProtocolEntryMapper
{
    public static ProtocolEntry ToProtocolEntry(GroundTruthEntry source)
        => new()
        {
            Code = source.VsaCode,
            Beschreibung = source.Text,
            MeterStart = source.MeterStart,
            MeterEnd = source.MeterEnd,
            IsStreckenschaden = source.IsStreckenschaden,
            Zeit = source.Zeit,
            Source = ProtocolEntrySource.Imported,
            FotoPaths = source.ExtractedFramePath is not null
                ? new List<string> { source.ExtractedFramePath }
                : new List<string>(),
            CodeMeta = BuildCodeMeta(source)
        };

    public static ProtocolEntryCodeMeta? CloneCodeMeta(ProtocolEntryCodeMeta? source)
    {
        if (source is null)
            return null;

        return new ProtocolEntryCodeMeta
        {
            Code = source.Code,
            Parameters = new Dictionary<string, string>(
                source.Parameters ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase),
            Severity = source.Severity,
            Count = source.Count,
            Notes = source.Notes,
            UpdatedAt = source.UpdatedAt
        };
    }

    private static ProtocolEntryCodeMeta? BuildCodeMeta(GroundTruthEntry source)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (source.Quantification is not null)
        {
            var formatted = FormatQuantification(source.Quantification);
            Add(parameters, "vsa.q1", formatted);
            Add(parameters, "Q1", formatted);
            Add(parameters, "Quantifizierung1", formatted);
            Add(parameters, "vsa.q1.value", FormatNumber(source.Quantification.Value));
            Add(parameters, "vsa.q1.unit", source.Quantification.Unit);
            Add(parameters, "vsa.q1.type", source.Quantification.Type);
            Add(parameters, "vsa.q1.uhr", source.Quantification.ClockPosition);
        }

        Add(parameters, "vsa.uhr.von", source.ClockPosition);
        Add(parameters, "ClockPos1", source.ClockPosition);
        Add(parameters, "vsa.anschluss.uhr", source.ConnectionClock);
        Add(parameters, "ConnectionClock", source.ConnectionClock);

        var characterization = NormalizeCharacterization(source.Characterization);
        Add(parameters, "vsa.charakterisierung", characterization);
        Add(parameters, "Char1", characterization);

        if (parameters.Count == 0)
            return null;

        return new ProtocolEntryCodeMeta
        {
            Code = source.VsaCode,
            Parameters = parameters,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static string FormatQuantification(QuantificationDetail quantification)
    {
        var value = FormatNumber(quantification.Value);
        return string.IsNullOrWhiteSpace(quantification.Unit)
            ? value
            : $"{value} {quantification.Unit.Trim()}";
    }

    private static string FormatNumber(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string? NormalizeCharacterization(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToUpperInvariant();
    }

    private static void Add(Dictionary<string, string> parameters, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        parameters[key] = value.Trim();
    }
}

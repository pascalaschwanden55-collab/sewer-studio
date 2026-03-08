using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.QualityGate;

/// <summary>
/// Monte-Carlo Dropout approximation via multiple LLM passes at different temperatures.
/// Only executed for Yellow-zone detections (0.45-0.75) to bound latency.
/// Epistemic uncertainty = 1 - agreement rate of predicted codes.
/// </summary>
public sealed class McDropoutService
{
    private static readonly double[] Temperatures = { 0.1, 0.5, 0.9 };

    private readonly OllamaClient _client;
    private readonly string _model;
    private readonly JsonElement _schema;

    public McDropoutService(OllamaClient client, string model, JsonElement schema)
    {
        _client = client;
        _model = model;
        _schema = schema;
    }

    /// <summary>
    /// Run 3 LLM passes with different temperatures.
    /// Returns uncertainty estimate based on code agreement.
    /// </summary>
    public async Task<UncertaintyEstimate> EstimateAsync(
        IReadOnlyList<OllamaClient.ChatMessage> messages,
        double baseConfidence,
        CancellationToken ct = default)
    {
        var codes = new List<string>(Temperatures.Length);
        var confidences = new List<double>(Temperatures.Length);

        foreach (var temp in Temperatures)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var options = new Dictionary<string, object> { ["temperature"] = temp };
                var result = await _client.ChatStructuredWithOptionsAsync<McDropoutResult>(
                    _model, messages, _schema, options, ct).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(result.SuggestedCode))
                    codes.Add(result.SuggestedCode.Trim().ToUpperInvariant());
                if (result.Confidence > 0)
                    confidences.Add(result.Confidence);
            }
            catch
            {
                // Individual pass failure is acceptable
            }
        }

        if (codes.Count == 0)
            return UncertaintyEstimate.FromSinglePass(baseConfidence);

        // Agreement rate: fraction of passes that agree with the mode
        var mode = codes.GroupBy(c => c).OrderByDescending(g => g.Count()).First().Key;
        var agreementRate = (double)codes.Count(c => c == mode) / codes.Count;

        // Epistemic uncertainty = 1 - agreement
        var epistemic = 1.0 - agreementRate;

        // Aleatoric: variance of confidences across passes
        var meanConf = confidences.Count > 0 ? confidences.Average() : baseConfidence;
        var aleatoric = confidences.Count > 1
            ? confidences.Select(c => (c - meanConf) * (c - meanConf)).Average()
            : 0.05;

        return new UncertaintyEstimate(
            Confidence: baseConfidence,
            EpistemicUncertainty: epistemic,
            AleatoricUncertainty: Math.Min(aleatoric, 0.5),
            CalibratedConfidence: meanConf,
            Source: UncertaintySource.MonteCarlo);
    }

    private sealed class McDropoutResult
    {
        public string? SuggestedCode { get; set; }
        public double Confidence { get; set; }
        public string? Rationale { get; set; }
    }
}

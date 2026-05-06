// AuswertungPro – KI Videoanalyse Modul
using System;
using AuswertungPro.Next.Application.Ai.Ollama;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

/// <summary>
/// Erzeugt Text-Embeddings via Ollama <c>nomic-embed-text</c>.
/// POST /api/embed → float[] Vektor.
/// </summary>
public sealed class EmbeddingService(HttpClient http, OllamaConfig config)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private bool _modelChecked;

    /// <summary>Name des konfigurierten Embedding-Modells.</summary>
    public string ModelName => config.EmbedModel;

    /// <summary>
    /// Phase 2.1: Versions-/Quantisierungs-Tag des Embedding-Modells (z.B. "F16", "v1.5").
    /// Aktuell leer — kann spaeter via Ollama /api/show gesetzt werden, um
    /// Embedding-Migration bei Modell-Upgrade nachvollziehbar zu machen.
    /// </summary>
    public string ModelVersion => "";

    /// <summary>
    /// B7 Fix: Prueft ob das Embedding-Modell in Ollama verfuegbar ist.
    /// Wird beim ersten EmbedAsync-Aufruf automatisch geprueft.
    /// </summary>
    public async Task<bool> CheckModelAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var url = new Uri(config.BaseUri, "/api/tags");
            var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return false;
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return json.Contains(config.EmbedModel, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Erzeugt einen Embedding-Vektor für den gegebenen Text.
    /// Gibt null zurück wenn Ollama nicht erreichbar oder Fehler.
    /// </summary>
    public async Task<float[]?> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // B7: Einmaliger Model-Check beim ersten Aufruf
        if (!_modelChecked)
        {
            _modelChecked = true;
            if (!await CheckModelAvailableAsync(ct).ConfigureAwait(false))
            {
                Debug.WriteLine($"[EmbeddingService] Modell '{config.EmbedModel}' nicht in Ollama verfuegbar!");
                return null;
            }
        }

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var body = JsonSerializer.Serialize(new
                {
                    model = config.EmbedModel,
                    input = text
                });

                var url = new Uri(config.BaseUri, "/api/embed");
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

                using var resp = await http.SendAsync(request, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[EmbeddingService] HTTP {(int)resp.StatusCode} (Versuch {attempt}/{maxRetries})");
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(1000 * attempt, ct).ConfigureAwait(false);
                        continue;
                    }
                    return null;
                }

                var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);

                // Ollama /api/embed gibt { "embeddings": [[...]] }
                if (doc.RootElement.TryGetProperty("embeddings", out var outer)
                    && outer.ValueKind == JsonValueKind.Array
                    && outer.GetArrayLength() > 0)
                {
                    var inner = outer[0];
                    return inner.EnumerateArray()
                        .Select(v => v.GetSingle())
                        .ToArray();
                }

                return null;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EmbeddingService] Versuch {attempt}/{maxRetries}: {ex.GetType().Name}: {ex.Message}");
                if (attempt < maxRetries)
                {
                    await Task.Delay(1000 * attempt, ct).ConfigureAwait(false);
                    continue;
                }
                return null;
            }
        }

        return null;
    }

    /// <summary>Serialisiert einen float[] Vektor in einen byte[] BLOB.</summary>
    public static byte[] ToBlob(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>Deserialisiert einen byte[] BLOB zurück in float[].</summary>
    public static float[] FromBlob(byte[] blob)
    {
        if (blob.Length == 0)
        {
            System.Diagnostics.Debug.WriteLine("[EmbeddingService] WARNUNG: Leerer Blob — korruptes Embedding");
            return Array.Empty<float>();
        }
        if (blob.Length % sizeof(float) != 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EmbeddingService] WARNUNG: Blob-Laenge {blob.Length} nicht durch 4 teilbar — abgeschnitten");
        }
        var vector = new float[blob.Length / sizeof(float)];
        Buffer.BlockCopy(blob, 0, vector, 0, vector.Length * sizeof(float));
        return vector;
    }
}

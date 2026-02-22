// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Ollama;

namespace AuswertungPro.Next.UI.Ai.KnowledgeBase;

/// <summary>
/// Erzeugt Text-Embeddings via Ollama <c>mxbai-embed-large</c>.
/// POST /api/embed → float[] Vektor.
/// </summary>
public sealed class EmbeddingService(HttpClient http, OllamaConfig config)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Name des konfigurierten Embedding-Modells.</summary>
    public string ModelName => config.EmbedModel;

    /// <summary>
    /// Erzeugt einen Embedding-Vektor für den gegebenen Text.
    /// Gibt null zurück wenn Ollama nicht erreichbar oder Fehler.
    /// </summary>
    public async Task<float[]?> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

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
            if (!resp.IsSuccessStatusCode) return null;

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
        catch { return null; }
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
        var vector = new float[blob.Length / sizeof(float)];
        Buffer.BlockCopy(blob, 0, vector, 0, blob.Length);
        return vector;
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>
/// Content-Signatur ueber System.Text.Json, exakt dieselbe Serialisierung wie der echte
/// Projekt-Save (<see cref="JsonProjectRepository.SerializerOptions"/>). Die instabilen
/// Meta-Felder werden im JSON-Baum entfernt, BEVOR der SHA-256 gebildet wird — so aendert
/// sich die Signatur nur bei echten Datenaenderungen.
/// </summary>
public sealed class JsonProjectContentSignature : IProjectContentSignature
{
    // Felder, die sich ohne inhaltliche Aenderung bewegen und daher nicht in die Signatur duerfen.
    private static readonly string[] VolatileFields =
        ["ModifiedAtUtc", "Dirty", "LastCommittedImportTxId"];

    public string Compute(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var node = JsonSerializer.SerializeToNode(project, JsonProjectRepository.SerializerOptions);
        if (node is JsonObject root)
        {
            foreach (var field in VolatileFields)
                root.Remove(field);
        }

        var normalized = node?.ToJsonString(JsonProjectRepository.SerializerOptions) ?? string.Empty;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }
}

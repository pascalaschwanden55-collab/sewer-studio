namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

internal static class VsaYoloClassMapDocumentValidator
{
    public const int CurrentVersion = 2;

    public static void Validate(VsaYoloClassMapDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Format == VsaYoloClassMapFormat.Versioned)
        {
            if (document.Version != CurrentVersion)
            {
                throw new InvalidDataException(
                    $"Nicht unterstuetzte Klassenkarten-Version '{document.Version}'. Erwartet wird Version {CurrentVersion}.");
            }

            if (!IsSha256(document.VsaManifestHash))
            {
                throw new InvalidDataException(
                    "'vsa_manifest_hash' muss ein SHA-256-Wert mit 64 Hex-Zeichen sein.");
            }
        }
        else if (document.Version is not null || document.VsaManifestHash is not null)
        {
            throw new InvalidDataException("Das Altformat darf keine Versionsmetadaten enthalten.");
        }

        ValidateClasses(document.Classes);
    }

    private static void ValidateClasses(IReadOnlyDictionary<string, int> classes)
    {
        if (classes.Count == 0)
            throw new InvalidDataException("Die Klassenkarte enthaelt keine Klassen.");

        var ids = new HashSet<int>();
        foreach (var (key, id) in classes)
        {
            if (string.IsNullOrWhiteSpace(key) || !string.Equals(key, key.Trim(), StringComparison.Ordinal))
                throw new InvalidDataException("Klassenschluessel duerfen nicht leer sein oder Leerzeichen am Rand enthalten.");

            if (id < 0)
                throw new InvalidDataException($"Klasse '{key}' hat eine negative ID ({id}).");

            if (!ids.Add(id))
                throw new InvalidDataException($"Klassen-ID {id} ist mehrfach vergeben.");
        }

        var orderedIds = ids.OrderBy(id => id).ToArray();
        for (var expected = 0; expected < orderedIds.Length; expected++)
        {
            if (orderedIds[expected] != expected)
            {
                throw new InvalidDataException(
                    $"Klassen-IDs muessen lueckenlos bei 0 beginnen. Erwartet wurde {expected}, gefunden wurde {orderedIds[expected]}.");
            }
        }
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 }
           && value.All(Uri.IsHexDigit);
}

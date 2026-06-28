using System.Security.Cryptography;
using System.Text;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Reine Hilfsklasse fuer YOLO-Dataset-Benennung und deterministischen Split.
/// Enthaelt keine IO-Abhaengigkeiten.
/// </summary>
public static class YoloDatasetNaming
{
    /// <summary>
    /// Weist einem Datensatz-Item deterministisch anhand eines SHA-256-Hashes
    /// den Split "train" oder "val" zu.
    /// </summary>
    /// <param name="key">Eindeutiger Schluessel (z. B. Pfad oder Sample-ID); Gross-/Kleinschreibung ignoriert.</param>
    /// <param name="validationRatio">Anteil der Validierungsmenge [0..1].</param>
    /// <returns>"val" wenn der Hashwert unter dem Schwellwert liegt, sonst "train".</returns>
    public static string ChooseSplit(string key, double validationRatio)
    {
        if (validationRatio <= 0)
            return "train";
        if (validationRatio >= 1)
            return "val";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToUpperInvariant()));
        var value = BitConverter.ToUInt32(hash, 0) / (double)uint.MaxValue;
        return value < validationRatio ? "val" : "train";
    }
}

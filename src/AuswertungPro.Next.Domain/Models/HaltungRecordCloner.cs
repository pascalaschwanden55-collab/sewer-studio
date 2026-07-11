using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Tiefkopie einer Haltung fuer rein rechnerische Auswertungen (Schattenauswertung).
///
/// Warum noetig: <see cref="HaltungRecord.SetFieldValue"/> mutiert Felder, Metadaten und
/// ModifiedAtUtc und feuert PropertyChanged in die DataGrid-Bindings. Dienste wie die
/// VSA-Bewertung schreiben ihre Resultate direkt in den uebergebenen Record. Wer nur
/// LESEN will, laesst solche Dienste auf dieser Kopie rechnen und liest die Resultate
/// vom Klon ab — das Original bleibt byte-gleich und still.
/// </summary>
public static class HaltungRecordCloner
{
    /// <summary>
    /// Erstellt eine Bewertungs-Kopie: eigene Feld-/Metadaten-Dictionaries, kopierte
    /// VsaFindings, KEINE UserEdited-Sperren (sonst blieben menschlich editierte Werte
    /// am Klon stehen und die Neuberechnung wuerde verweigert). Protokoll-Referenzen
    /// werden nicht mitgenommen — die Bewertung liest sie nicht.
    /// </summary>
    public static HaltungRecord CloneForEvaluation(HaltungRecord source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var clone = new HaltungRecord
        {
            Id = source.Id,
            CreatedAtUtc = source.CreatedAtUtc,
            ModifiedAtUtc = source.ModifiedAtUtc,
            // Eigene Dictionaries: String-Werte sind unveraenderlich, flache Kopie genuegt.
            Fields = new Dictionary<string, string>(source.Fields, StringComparer.Ordinal),
            // FRISCHE Metadaten je Feld: geteilte Referenzen wuerden das Original mitmutieren,
            // und UserEdited=true wuerde das Neuschreiben am Klon blockieren (HaltungRecord.cs:52).
            FieldMeta = source.FieldMeta.ToDictionary(
                kv => kv.Key,
                kv => new FieldMetadata
                {
                    FieldName = kv.Value.FieldName,
                    Source = kv.Value.Source,
                    UserEdited = false,
                    LastUpdatedUtc = kv.Value.LastUpdatedUtc
                },
                StringComparer.Ordinal),
            // Defensive Finding-Kopien: die Bewertung reichert Findings an (EZ-Werte).
            VsaFindings = source.VsaFindings.Select(CloneFinding).ToList(),
            Protocol = null,
            ProtocolEntry = null
        };

        return clone;
    }

    private static VsaFinding CloneFinding(VsaFinding f) => new()
    {
        KanalSchadencode = f.KanalSchadencode,
        Quantifizierung1 = f.Quantifizierung1,
        Quantifizierung2 = f.Quantifizierung2,
        SchadenlageAnfang = f.SchadenlageAnfang,
        SchadenlageEnde = f.SchadenlageEnde,
        LL = f.LL,
        Raw = f.Raw,
        MeterStart = f.MeterStart,
        MeterEnd = f.MeterEnd,
        MPEG = f.MPEG,
        Timestamp = f.Timestamp,
        FotoPath = f.FotoPath,
        EZD = f.EZD,
        EZS = f.EZS,
        EZB = f.EZB
    };
}

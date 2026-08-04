namespace AuswertungPro.Next.Infrastructure.Import.SchachtPro;

/// <summary>
/// Zentrale kanonische Feldnamen fuer den SchachtPro-Import (.spro).
/// Stammdaten schreiben in dieselben Felder wie der PDF-/WinCan-Import
/// (Schachtnummer, Funktion, Schachtform, Dimension/Durchmesser, Schachttiefe,
/// Material, Datum, Bemerkungen); alle SchachtPro-spezifischen Felder
/// (Schachtaufbau, Anschluesse, GPS) sind HIER an einer Stelle definiert.
/// </summary>
internal static class SchachtProFieldNames
{
    // --- Geteilte kanonische Stammdaten (Konvention PDF-/WinCan-Import) ---
    internal const string Schachtnummer = "Schachtnummer";
    internal const string NrGross = "NR.";
    internal const string NrKlein = "Nr.";
    internal const string Funktion = "Funktion";
    internal const string Schachtform = "Schachtform";
    internal const string Dimension = "Dimension";
    internal const string Durchmesser = "Durchmesser";
    internal const string Schachttiefe = "Schachttiefe";
    internal const string Material = "Material";
    internal const string Bemerkungen = "Bemerkungen";
    internal const string AusfuehrungDatumJahr = "Ausführung Datum/Jahr";
    internal const string DatumJahr = "Datum/Jahr";
    internal const string PrimaereSchaeden = "Primäre Schäden";

    // ASCII-Legacy-Aliase, die der Schacht-PDF-Import (SchachtProtocolApplier)
    // parallel zur Umlaut-Form schreibt. Werden mitgeschrieben, damit aeltere
    // Leser/Exporte beide Importwege gleich sehen. Mojibake-Varianten werden
    // bewusst NICHT neu geschrieben (nur Bestandsdaten-Schutz).
    internal const string AusfuehrungDatumJahrAscii = "Ausfuehrung Datum/Jahr";
    internal const string PrimaereSchaedenAscii = "Primaere Schaeden";

    // --- Neue Felder: Protokoll-Stammdaten ---
    internal const string Wetter = "Wetter";
    internal const string Medium = "Medium";
    internal const string Schachtlaenge = "Schachtlänge";
    internal const string Schachtbreite = "Schachtbreite";
    internal const string Doppelschacht = "Doppelschacht";
    internal const string Steighilfe = "Steighilfe";

    // --- Neue Felder: Schachtaufbau ---
    internal const string RahmenDeckelHoehe = "Rahmen-Deckel-Höhe";
    internal const string Deckelmaterial = "Deckelmaterial";
    internal const string Deckelform = "Deckelform";
    internal const string Deckeltyp = "Deckeltyp";
    internal const string Belastungsklasse = "Belastungsklasse";
    internal const string Deckeldurchmesser = "Deckeldurchmesser";
    internal const string SchachthalsForm = "Schachthals-Form";
    internal const string SchachthalsDimension = "Schachthals-Dimension";
    internal const string SchachthalsHoehe = "Schachthals-Höhe";
    internal const string SchachthalsDurchmesser = "Schachthals-Durchmesser";
    internal const string SchachthalsZwischenKonusDurchmesser = "Schachthals-Zwischen-Konus-Durchmesser";
    internal const string KonusVorhanden = "Konus vorhanden";
    internal const string KonusExzentrisch = "Konus exzentrisch";
    internal const string KonusHoehe = "Konus-Höhe";
    internal const string KonusForm = "Konus-Form";
    internal const string KonusDimension = "Konus-Dimension";
    internal const string KonusDurchmesserOben = "Konus-Durchmesser oben";
    internal const string KonusDurchmesserUnten = "Konus-Durchmesser unten";
    internal const string SchachtoberteilForm = "Schachtoberteil-Form";
    internal const string SchachtoberteilHoehe = "Schachtoberteil-Höhe";
    internal const string SchachtoberteilDimension = "Schachtoberteil-Dimension";
    internal const string SchachtunterteilForm = "Schachtunterteil-Form";
    internal const string SchachtrohrHoehe = "Schachtrohr-Höhe";
    internal const string SchachtunterteilDimension = "Schachtunterteil-Dimension";
    internal const string SkizzenNotiz = "Skizzen-Notiz";

    // --- Neue Felder: Anschluesse / Fotos / GPS ---
    internal const string Anschluesse = "Anschlüsse";
    internal const string Fotos = "Fotos";
    internal const string KoordinateEast = "Koordinate_East";
    internal const string KoordinateNorth = "Koordinate_North";

    /// <summary>Metadaten-Schluessel des Projekts fuer den Auftraggeber (bestehender Katalog).</summary>
    internal const string ProjektMetadataAuftraggeber = "Auftraggeber";
}

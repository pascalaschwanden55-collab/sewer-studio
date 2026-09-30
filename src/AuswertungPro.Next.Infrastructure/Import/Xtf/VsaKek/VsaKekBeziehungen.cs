using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using IVsaMediaPathResolver = AuswertungPro.Next.Application.Import.IVsaMediaPathResolver;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.VsaKek;

/// <summary>Eine Untersuchung, deren Bauwerksart sich nicht belegen liess.</summary>
internal sealed record XtfOffeneUntersuchung(string Bezeichnung, string Grund);

/// <summary>
/// Die Untersuchungen einer Datei mit aufgeloesten Bezuegen, nach Bauwerksart eingeordnet.
///
/// Was sich nicht zuordnen liess, bleibt hier sichtbar, wird aber (wie bisher) weder
/// uebernommen noch gemeldet: <see cref="OhneBezeichnung"/>, die verwaisten Schaeden und
/// <see cref="FotosOhneBefund"/>. Eine Meldung dafuer waere eine Verhaltensaenderung.
/// </summary>
internal sealed record VsaKekBezuege(
    IReadOnlyList<VsaKekUntersuchung> Haltungsuntersuchungen,
    IReadOnlyList<VsaKekUntersuchung> Schachtbegehungen,
    IReadOnlyList<XtfOffeneUntersuchung> Offene,
    IReadOnlyList<VsaKekUntersuchung> OhneBezeichnung,
    IReadOnlyDictionary<string, List<VsaFinding>> BefundeJeUntersuchung,
    IReadOnlyDictionary<string, string> VideoJeUntersuchung,
    IReadOnlyList<VsaKekKanalschadenObjekt> VerwaisteKanalschaeden,
    IReadOnlyList<VsaKekNormschachtschadenObjekt> VerwaisteNormschachtschaeden,
    IReadOnlyList<VsaKekDatei> FotosOhneBefund,
    int Untersuchungen);

/// <summary>
/// Schritt 2 des VSA-KEK-Imports: loest die Verweise gegen den gelesenen
/// <see cref="VsaKekBestand"/> auf — Schaden zu Untersuchung (ueber die TID in
/// <c>UntersuchungRef</c>), Foto zu Kanalschaden (OBJ_ID oder TID), Video zu Untersuchung,
/// Bauwerksverweis — und ordnet jede Untersuchung ueber <see cref="VsaKekUntersuchungsart"/>
/// als Haltung, Schacht oder ungeklaert ein. Keine Feldwerte deuten, nichts ins Projekt
/// schreiben; das macht <c>VsaKekAbbildung</c>.
/// </summary>
internal static class VsaKekBeziehungen
{
    public static VsaKekBezuege Loese(VsaKekBestand bestand, string sourcePath, IVsaMediaPathResolver mediaPaths)
    {
        ArgumentNullException.ThrowIfNull(bestand);
        ArgumentNullException.ThrowIfNull(mediaPaths);

        var untersuchungen = bestand.Untersuchungen;
        // Befunde je Untersuchungs-TID. Bis 30.09.2026 je Bezeichnung: Dann trug eine
        // Haltung mit Hin- und Gegenbefahrung die Befunde beider Untersuchungen.
        var befundeJeUntersuchung = new Dictionary<string, List<VsaFinding>>(StringComparer.Ordinal);
        var befundNachObjId = new Dictionary<string, VsaFinding>(StringComparer.OrdinalIgnoreCase);
        var befundNachTid = new Dictionary<string, VsaFinding>(StringComparer.OrdinalIgnoreCase);
        var verwaisteKanalschaeden = new List<VsaKekKanalschadenObjekt>();

        foreach (var kanalschaden in bestand.Kanalschaeden)
        {
            // Zuordnung ueber den Bezug (UntersuchungRef), nicht ueber den Namen. Ein
            // Kanalschaden ohne gueltigen Bezug faellt heute still weg.
            var refTid = kanalschaden.UntersuchungRef;
            if (string.IsNullOrWhiteSpace(refTid) || !untersuchungen.TryGetValue(refTid!, out var u))
            {
                verwaisteKanalschaeden.Add(kanalschaden);
                continue;
            }

            var befund = kanalschaden.Befund;
            u.Schaeden.Add(kanalschaden.Werte);
            if (!string.IsNullOrWhiteSpace(kanalschaden.Werte.ObjId))
                befundNachObjId[kanalschaden.Werte.ObjId] = befund;
            // XTF-Variante nutzt Datei.Objekt = Kanalschaden-TID (kein OBJ_ID-Element vorhanden) — auch nach TID indizieren.
            if (!string.IsNullOrWhiteSpace(kanalschaden.Tid))
                befundNachTid[kanalschaden.Tid!] = befund;
            if (!befundeJeUntersuchung.TryGetValue(refTid!, out var liste))
            {
                liste = new List<VsaFinding>();
                befundeJeUntersuchung[refTid!] = liste;
            }
            liste.Add(befund);
        }

        var verwaisteNormschachtschaeden = new List<VsaKekNormschachtschadenObjekt>();
        foreach (var normschachtschaden in bestand.Normschachtschaeden)
        {
            var refTid = normschachtschaden.UntersuchungRef;
            // Ein verwaister Verweis darf den Import nicht stoppen; er wird keiner
            // Untersuchung untergeschoben.
            if (string.IsNullOrWhiteSpace(refTid) || !untersuchungen.TryGetValue(refTid!, out var zielUntersuchung))
            {
                verwaisteNormschachtschaeden.Add(normschachtschaden);
                continue;
            }

            zielUntersuchung.Schachtschaeden.Add(normschachtschaden.Werte);
        }

        var videoJeUntersuchung = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fotosOhneBefund = new List<VsaKekDatei>();
        foreach (var datei in bestand.Dateien)
            OrdneDateiZu(datei, befundNachObjId, befundNachTid, videoJeUntersuchung, fotosOhneBefund, sourcePath, mediaPaths);

        // Erst jetzt einordnen: Die Einordnung zaehlt die zugeordneten Schaeden.
        var haltungen = new List<VsaKekUntersuchung>();
        var schaechte = new List<VsaKekUntersuchung>();
        var offene = new List<XtfOffeneUntersuchung>();
        var ohneBezeichnung = new List<VsaKekUntersuchung>();
        foreach (var u in untersuchungen.Values)
        {
            // Ohne Bezeichnung laesst sich die Untersuchung keinem Bauwerk zuordnen. Sie
            // zaehlt in "Untersuchungen gelesen", erscheint aber sonst nirgends.
            if (string.IsNullOrWhiteSpace(u.Bezeichnung))
            {
                ohneBezeichnung.Add(u);
                continue;
            }

            var artErgebnis = VsaKekUntersuchungsart.Bestimme(Belege(u, bestand.Bauwerksarten));
            if (artErgebnis.Art == VsaKekBauteilart.Unklar)
                offene.Add(new XtfOffeneUntersuchung(u.Bezeichnung, artErgebnis.Grund));
            else if (artErgebnis.Art == VsaKekBauteilart.Schacht)
                schaechte.Add(u);
            else
                haltungen.Add(u);
        }

        return new VsaKekBezuege(haltungen, schaechte, offene, ohneBezeichnung,
            befundeJeUntersuchung, videoJeUntersuchung,
            verwaisteKanalschaeden, verwaisteNormschachtschaeden, fotosOhneBefund,
            untersuchungen.Count);
    }

    /// <summary>
    /// Haltung oder Schacht? Die Datei sagt es an mehreren Stellen; widersprechen sie
    /// sich, bleibt der Fall offen statt geraten (<see cref="VsaKekUntersuchungsart"/>).
    /// </summary>
    private static VsaKekUntersuchungsBelege Belege(VsaKekUntersuchung u,
        IReadOnlyDictionary<string, VsaKekBauteilart> bauwerksarten)
        => new(
            HatVonBisPunkt: !string.IsNullOrWhiteSpace(u.VonPunkt) || !string.IsNullOrWhiteSpace(u.BisPunkt),
            Erfassungsart: u.Erfassungsart,
            Kanalschaeden: u.Schaeden.Count,
            Normschachtschaeden: u.Schachtschaeden.Count,
            Bauwerksverweis: bauwerksarten.TryGetValue(u.AbwasserbauwerkRef ?? "", out var verweisart)
                ? verweisart
                : VsaKekBauteilart.Unklar)
        {
            Bezeichnung = u.Bezeichnung
        };

    /// <summary>
    /// Ordnet ein Datei-Objekt zu: ein Video der Untersuchung (Klasse=Untersuchung, bekannte
    /// Videoendung ODER Relativpfad "Film"; das erste gewinnt) oder ein Foto dem Kanalschaden
    /// (OBJ_ID vor TID; das erste Foto gewinnt). Andere Dateien bleiben unbeachtet.
    /// </summary>
    private static void OrdneDateiZu(
        VsaKekDatei datei,
        Dictionary<string, VsaFinding> befundNachObjId,
        Dictionary<string, VsaFinding> befundNachTid,
        Dictionary<string, string> videoJeUntersuchung,
        List<VsaKekDatei> fotosOhneBefund,
        string sourcePath,
        IVsaMediaPathResolver mediaPaths)
    {
        var (art, klasse, objekt, bezeichnung, relativpfad) = datei;

        // --- Untersuchungs-Video (Klasse=Untersuchung, zentral bekanntes Video ODER relativpfad=Film) ---
        if (klasse.Contains("Untersuchung", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(objekt))
        {
            var istVideo = MediaFileTypes.HasVideoExtension(bezeichnung)
                           || relativpfad.Contains("Film", StringComparison.OrdinalIgnoreCase);
            if (istVideo)
            {
                var videoPfad = mediaPaths.ResolveVideo(sourcePath, relativpfad, bezeichnung);
                if (!string.IsNullOrWhiteSpace(videoPfad)
                    && !videoJeUntersuchung.ContainsKey(objekt))
                {
                    videoJeUntersuchung[objekt] = videoPfad;
                }
            }
            return;
        }

        if (!art.Contains("Foto", StringComparison.OrdinalIgnoreCase))
            return;
        if (!klasse.Contains("Kanalschaden", StringComparison.OrdinalIgnoreCase))
            return;
        // Datei.Objekt referenziert den Kanalschaden — je nach XTF-Variante via OBJ_ID ODER TID.
        if (string.IsNullOrWhiteSpace(objekt)
            || !(befundNachObjId.TryGetValue(objekt, out var befund)
                 || befundNachTid.TryGetValue(objekt, out befund)))
        {
            fotosOhneBefund.Add(datei);
            return;
        }

        var fotoPath = mediaPaths.ResolvePhoto(sourcePath, relativpfad, bezeichnung);
        if (string.IsNullOrWhiteSpace(fotoPath))
            return;

        if (string.IsNullOrWhiteSpace(befund.FotoPath))
            befund.FotoPath = fotoPath;
    }
}

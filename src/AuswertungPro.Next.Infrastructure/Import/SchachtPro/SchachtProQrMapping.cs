using System.Text.Json;
using System.Text.Json.Nodes;

namespace AuswertungPro.Next.Infrastructure.Import.SchachtPro;

/// <summary>SPQR1-Felder laut ProtocolQrPayload.kt auf den gemeinsamen SchachtPro-Vertrag abbilden.</summary>
internal static class SchachtProQrMapping
{
    internal static ProtocolDto Map(JsonObject p)
    {
        var target = new JsonObject();
        Copy(p, target, "schachtNr:schachtNr datum:datum wetter:wetter remarks:bemerkungen");
        Copy(p["masterData"], target, "function:schachtFunktion medium:medium depth:tiefe dimension:dimension length:laenge width:breite form:schachtform rotationDeg:schachtRotation material:materialSchacht include3dDiagram:include3dDiagram doubleShaft:doppelschacht doubleShaftRotationDeg:doppelschachtRotation");
        Copy(p["cover"], target, "material:deckelMaterial form:deckelform type:deckelTyp loadClass:belastungsklasse diameter:deckelDurchmesser frameCoverHeight:rahmenDeckelHoehe");
        Copy(p["structure"], target, "shaftNeckForm:schachthalsForm shaftNeckDimension:schachthalsDimension shaftNeckHeight:schachthalsHoehe shaftNeckToConeHeight:schachthalsZwischenKonusHoehe conePresent:konus coneEccentric:konusExzentrisch coneHeight:konusHoehe coneForm:konusForm coneDimension:konusDimension upperPartForm:schachtOberteilForm upperPartDimension:schachtOberteilDimension lowerPartForm:schachtUnterteilForm lowerPartDimension:schachtUnterteilDimension shaftPipeHeight:schachtrohrHoehe");
        Copy(p["sketch"], target, "coneDiameterTop:konusDurchmesserOben coneDiameterBottom:konusDurchmesserUnten shaftNeckDiameter:schachthalsDurchmesser shaftNeckToConeDiameter:schachthalsZwischenKonusDurchmesser note:skizzeNotiz");
        if (p["coordinates"] is { } coordinates)
        {
            if (coordinates["crs"]?.GetValue<string>() != "EPSG:2056")
                throw new InvalidDataException("QR-Koordinatensystem wird nicht unterstuetzt (erwartet LV95).");
            Copy(coordinates, target, "east:lv95East north:lv95North");
        }
        var remarks = new JsonObject();
        foreach (var (qr, archive, section) in Sections)
        {
            if (p["condition"]?[qr] is not { } condition) continue;
            target[archive] = Selected(condition["selected"]);
            if (condition["remark"] is { } remark) remarks[section] = remark.DeepClone();
        }
        if (p["condition"]?["additionalRemarks"] is { } additional)
            foreach (var pair in additional.AsObject())
            {
                if (remarks.ContainsKey(pair.Key)) throw new InvalidDataException("Mehrdeutige Bauteilbemerkung.");
                remarks[pair.Key] = pair.Value?.DeepClone();
            }
        target["zustandBemerkungen"] = remarks;
        var connections = new JsonArray();
        if (p["connections"] is { } list)
        {
            if (list.AsArray().Count > 100) throw new InvalidDataException("Zu viele QR-Anschluesse.");
            foreach (var connection in list.AsArray())
            {
                if (connection is null) throw new InvalidDataException("Leerer Anschlussdatensatz.");
                var a = new JsonObject();
                Copy(connection, a, "number:nr type:typ medium:medium dn:dn depth:tiefe material:material clock:uhr direction:richtung pipeForm:rohrform width:breite height:hoehe");
                a["zustand"] = Selected(connection["condition"]);
                // Die App liefert auch unbenutzte Zeilen mit Nummer und Standardform.
                if (new[] { "typ", "medium", "dn", "tiefe", "material", "uhr", "richtung", "breite", "hoehe" }
                    .Any(k => !string.IsNullOrWhiteSpace(a[k]?.GetValue<string>())) || a["zustand"]!.AsObject().Count > 0)
                    connections.Add(a);
            }
        }
        target["anschluesse"] = connections;
        var result = target.Deserialize<ProtocolDto>()!;
        if ((result.Lv95East.HasValue && !double.IsFinite(result.Lv95East.Value))
            || (result.Lv95North.HasValue && !double.IsFinite(result.Lv95North.Value)))
            throw new InvalidDataException("Ungueltige QR-Koordinaten.");
        return result;
    }

    private static void Copy(JsonNode? source, JsonObject target, string mapping)
    {
        if (source is null) return;
        foreach (var pair in mapping.Split(' '))
        {
            var names = pair.Split(':');
            if (source[names[0]] is { } value) target[names[1]] = value.DeepClone();
        }
    }

    private static JsonObject Selected(JsonNode? node)
    {
        var result = new JsonObject();
        if (node is null) return result;
        foreach (var item in node.AsArray())
        {
            var label = item?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(label)) throw new InvalidDataException("Leere Zustandsangabe im QR-Code.");
            result[label] = true;
        }
        return result;
    }

    private static readonly (string Qr, string Archive, string Section)[] Sections =
    [
        ("shaft", "schachtZustand", "Schacht"), ("cover", "deckelZustand", "Schachtdeckel"),
        ("coverFrame", "deckelrahmenZustand", "Deckelrahmen"), ("shaftNeck", "schachthalsZustand", "Schachthals"),
        ("cone", "konusZustand", "Konus"), ("shaftPipe", "schachtrohrZustand", "Schachtrohr"),
        ("bench", "bankettZustand", "Bankett"), ("channel", "durchlaufrinneZustand", "Durchlaufrinne"),
        ("ladderSteps", "leiterSteigeisen", "Leiter/Steigeisen"), ("dipPipe", "tauchbogen", "Tauchbogen")
    ];
}

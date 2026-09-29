using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AuswertungPro.Next.Infrastructure.Import.SchachtPro;

internal sealed record SchachtProQrPayload(ProtocolDto Protocol, string Json, string SourceKey)
{
    internal static SchachtProQrPayload Parse(string text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (text.Length > 8192) throw new InvalidDataException("QR-Inhalt ist zu gross.");
        var parts = text.Split(':');
        if (parts.Length != 3 || parts[0] != "SPQR1")
            throw new InvalidDataException("Kein unterstützter SchachtPro-QR-Code (SPQR1).");
        if (parts[1].Length == 0 || parts[1].Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            throw new InvalidDataException("QR-Inhalt ist ungültig codiert.");
        var base64 = parts[1].Replace('-', '+').Replace('_', '/');
        var compressed = Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
        uint crc = 0xffffffff;
        foreach (var b in compressed)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }
        if (!string.Equals(parts[2], (~crc).ToString("X8"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("QR-Prüfsumme stimmt nicht. Keine Daten übernommen.");
        using var input = new MemoryStream(compressed);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        int count;
        while ((count = zlib.Read(buffer)) != 0)
        {
            ct.ThrowIfCancellationRequested();
            if (output.Length + count > 65536) throw new InvalidDataException("Entpackter QR-Inhalt ist zu gross.");
            output.Write(buffer, 0, count);
        }
        var raw = output.ToArray();
        uint a = 1, bSum = 0;
        foreach (var value in raw) { a = (a + value) % 65521; bSum = (bSum + a) % 65521; }
        if (compressed.Length < 6 || System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(compressed.AsSpan(compressed.Length - 4))
            != ((bSum << 16) | a))
            throw new InvalidDataException("Komprimierter QR-Inhalt ist unvollständig oder beschädigt.");
        var json = new UTF8Encoding(false, true).GetString(raw);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 24 });
        CheckUnique(document.RootElement);
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidDataException("QR-Daten sind leer.");
        if (root["schema"]?.GetValue<string>() != "ch.sewer-studio.schachtpro.protocol"
            || root["version"]?.GetValue<int>() != 1 || root["source"]?.GetValue<string>() != "SchachtPro")
            throw new InvalidDataException("Unbekanntes QR-Datenformat oder neuere Version.");
        var protocol = root["protocol"]?.AsObject() ?? throw new InvalidDataException("Protokoll fehlt.");
        var key = protocol["sourceKey"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
            throw new InvalidDataException("QR-Quellkennung fehlt oder ist ungültig.");
        var dto = SchachtProQrMapping.Map(protocol);
        if (string.IsNullOrWhiteSpace(dto.SchachtNr) || dto.SchachtNr.Length > 256)
            throw new InvalidDataException("Schachtnummer fehlt oder ist zu lang.");
        return new(dto, json, key);
    }

    private static void CheckUnique(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in node.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Doppelte QR-Datenfelder sind nicht erlaubt.");
                CheckUnique(property.Value);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var child in node.EnumerateArray()) CheckUnique(child);
    }
}

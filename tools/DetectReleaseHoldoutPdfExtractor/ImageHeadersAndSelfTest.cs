namespace DetectReleaseHoldoutPdfExtractor;

internal static class ImageHeaders
{
    public static ImageHeader Read(byte[] bytes)
    {
        if (bytes.Length >= 24
            && bytes.AsSpan(0, 8).SequenceEqual(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            return Validate(
                ReadInt32BigEndian(bytes, 16),
                ReadInt32BigEndian(bytes, 20),
                ".png");
        }

        if (bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8)
        {
            var offset = 2;
            while (offset + 3 < bytes.Length)
            {
                while (offset < bytes.Length && bytes[offset] != 0xff)
                    offset++;
                while (offset < bytes.Length && bytes[offset] == 0xff)
                    offset++;
                if (offset >= bytes.Length)
                    break;
                var marker = bytes[offset++];
                if (marker is 0xd8 or 0xd9 or 0x01 || marker is >= 0xd0 and <= 0xd7)
                    continue;
                if (offset + 1 >= bytes.Length)
                    break;
                var length = (bytes[offset] << 8) | bytes[offset + 1];
                if (length < 2 || offset + length > bytes.Length)
                    break;
                if (IsStartOfFrame(marker) && length >= 7)
                {
                    var height = (bytes[offset + 3] << 8) | bytes[offset + 4];
                    var width = (bytes[offset + 5] << 8) | bytes[offset + 6];
                    return Validate(width, height, ".jpg");
                }

                offset += length;
            }
        }

        throw new InvalidDataException("Das extrahierte Bild ist weder ein gültiges PNG noch JPEG.");
    }

    private static bool IsStartOfFrame(byte marker)
        => marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7
            or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf;

    private static int ReadInt32BigEndian(byte[] bytes, int offset)
        => (bytes[offset] << 24)
           | (bytes[offset + 1] << 16)
           | (bytes[offset + 2] << 8)
           | bytes[offset + 3];

    private static ImageHeader Validate(int width, int height, string extension)
    {
        if (width <= 0 || height <= 0 || width > 65535 || height > 65535)
            throw new InvalidDataException("Das extrahierte Bild besitzt ungültige Abmessungen.");
        return new ImageHeader(width, height, extension);
    }
}

internal static class GuardSelfTest
{
    public static int Run()
    {
        try
        {
            string[] expected =
            [
                "BCA", "BAB", "BAC", "BAA", "BAF", "BAH", "BAI", "BAJ",
                "BBA", "BBB", "BBC", "BBD", "BBF", "SONST", "BCC",
            ];
            for (var index = 0; index < expected.Length; index++)
            {
                if (!DetectClassMap.TryResolve(expected[index], out var detectClass)
                    || detectClass.Id != index)
                {
                    throw new InvalidOperationException(
                        $"Detect-Klasse {expected[index]} fehlt oder besitzt die falsche ID.");
                }
            }

            if (!DetectClassMap.TryResolve("BCAA", out var fullCode)
                || fullCode.MainCode != "BCA")
            {
                throw new InvalidOperationException("Ein gültiger VSA-Endcode wird nicht erkannt.");
            }
            if (DetectClassMap.TryResolve("BCD", out _)
                || DetectClassMap.TryResolve("SONST_schaden", out _))
            {
                throw new InvalidOperationException("Ein nicht unterstützter Code wurde zugelassen.");
            }

            if (InputValidation.RequireHolding("06.24379-06.24377", "test") != "24379-24377")
                throw new InvalidOperationException("Haltungsnormalisierung ist falsch.");
            if (HoldingKeys.Physical("24379-24377") != "24377|24379")
                throw new InvalidOperationException("Physischer Haltungsschlüssel ist falsch.");

            byte[] pngHeader =
            [
                137, 80, 78, 71, 13, 10, 26, 10,
                0, 0, 0, 13, 73, 72, 68, 82,
                0, 0, 2, 128, 0, 0, 1, 224,
            ];
            var header = ImageHeaders.Read(pngHeader);
            if (header.Width != 640 || header.Height != 480)
                throw new InvalidOperationException("PNG-Abmessungen sind falsch.");

            Console.WriteLine("Selbsttest erfolgreich: Klassen, Haltung und Bildkopf sind gültig.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Selbsttest fehlgeschlagen: {ex.Message}");
            return 1;
        }
    }
}

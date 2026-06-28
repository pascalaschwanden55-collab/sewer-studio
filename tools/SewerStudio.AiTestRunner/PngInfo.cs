// Liest PNG-Abmessungen aus dem IHDR-Chunk (Bytes 16-23) ohne externe Bibliotheken.
internal static class PngInfo
{
    public static (int Width, int Height) ReadDimensions(byte[] pngBytes)
    {
        if (pngBytes.Length < 24)
            return (0, 0);

        try
        {
            var width = (pngBytes[16] << 24) | (pngBytes[17] << 16) | (pngBytes[18] << 8) | pngBytes[19];
            var height = (pngBytes[20] << 24) | (pngBytes[21] << 16) | (pngBytes[22] << 8) | pngBytes[23];
            return width > 0 && height > 0 ? (width, height) : (0, 0);
        }
        catch
        {
            return (0, 0);
        }
    }
}

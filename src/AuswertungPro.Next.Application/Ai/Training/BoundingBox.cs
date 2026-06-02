namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>Normierte (0-1) YOLO-Box am Frame. Validiert: vollstaendig im Bild, positive Groesse.</summary>
public readonly record struct BoundingBox(double XCenter, double YCenter, double Width, double Height)
{
    public static bool TryCreate(double xc, double yc, double w, double h, out BoundingBox box)
    {
        box = default;
        if (w <= 0 || h <= 0) return false;
        if (xc is < 0 or > 1 || yc is < 0 or > 1) return false;
        // Box muss komplett im Bild liegen.
        if (xc - w / 2 < -1e-6 || xc + w / 2 > 1 + 1e-6) return false;
        if (yc - h / 2 < -1e-6 || yc + h / 2 > 1 + 1e-6) return false;
        box = new BoundingBox(xc, yc, w, h);
        return true;
    }

    public void ApplyTo(TrainingSample sample)
    {
        sample.BboxXCenter = XCenter;
        sample.BboxYCenter = YCenter;
        sample.BboxWidth = Width;
        sample.BboxHeight = Height;
    }
}

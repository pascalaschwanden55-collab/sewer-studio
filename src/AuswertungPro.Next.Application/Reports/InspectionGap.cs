namespace AuswertungPro.Next.Application.Reports;

public sealed record InspectionGap(double StartMeter, double EndMeter)
{
    public double Length => EndMeter - StartMeter;
}

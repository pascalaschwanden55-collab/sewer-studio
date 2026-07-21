using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Reine Winkel- und Zeichengeometrie fuer Abzweig- und Bogenmessungen.
/// Die oeffentliche Kompatibilitaetsfassade bleibt <see cref="PhotoMeasurementGeometryService"/>.
/// </summary>
internal static class PhotoMeasurementAnglePlanBuilder
{
    internal static double PositionDegToClockHour(double positionDeg)
        => positionDeg / 30.0;

    internal static double PositionDegToRadians(double positionDeg)
        => (positionDeg - 90.0) * Math.PI / 180.0;

    internal static PhotoMeasurementAngleGeometry BuildAngleGeometry(
        OverlayToolType toolType,
        NormalizedPoint pipeCenter,
        double normalizedDiameter,
        double positionDeg,
        double angleDeg)
    {
        if (toolType is not (OverlayToolType.LateralCircle or OverlayToolType.PipeBend))
            throw new ArgumentOutOfRangeException(nameof(toolType), toolType, "Only LateralCircle and PipeBend are supported.");

        double positionRad = PositionDegToRadians(positionDeg);
        double clockHour = PositionDegToClockHour(positionDeg);
        double radiusNorm = normalizedDiameter / 2.0;
        var edgePoint = new NormalizedPoint(
            pipeCenter.X + Math.Cos(positionRad) * radiusNorm,
            pipeCenter.Y + Math.Sin(positionRad) * radiusNorm);

        var geometry = new OverlayGeometry
        {
            ToolType = toolType,
            ArcDegrees = Math.Round(angleDeg, 1),
            ClockFrom = Math.Round(clockHour, 1),
            Points = new List<NormalizedPoint> { pipeCenter, edgePoint }
        };

        return new PhotoMeasurementAngleGeometry(geometry, positionRad, clockHour, edgePoint);
    }

    internal static PhotoMeasurementAngleOverlayPlan? BuildAngleOverlayPlan(
        OverlayToolType toolType,
        PipeCalibration calibration,
        double positionDeg,
        double angleDeg,
        double renderedX,
        double renderedY,
        double renderedWidth,
        double renderedHeight)
    {
        ArgumentNullException.ThrowIfNull(calibration);

        var pipePlan = PhotoMeasurementGeometryService.BuildPipeCirclePlan(
            calibration,
            renderedX,
            renderedY,
            renderedWidth,
            renderedHeight);
        if (pipePlan is null)
            return null;

        var angleGeometry = BuildAngleGeometry(
            toolType,
            calibration.PipeCenter,
            calibration.NormalizedDiameter,
            positionDeg,
            angleDeg);

        PhotoMeasurementLateralOverlayPlan? lateralPlan = null;
        PhotoMeasurementBendOverlayPlan? bendPlan = null;
        if (toolType == OverlayToolType.LateralCircle)
        {
            lateralPlan = BuildLateralOverlayPlan(
                pipePlan.Center.X,
                pipePlan.Center.Y,
                pipePlan.Radius,
                angleGeometry.PositionRad,
                angleDeg);
        }
        else if (toolType == OverlayToolType.PipeBend)
        {
            bendPlan = BuildBendOverlayPlan(
                pipePlan.Center.X,
                pipePlan.Center.Y,
                pipePlan.Radius,
                angleGeometry.PositionRad,
                angleDeg);
        }

        return new PhotoMeasurementAngleOverlayPlan(
            Geometry: angleGeometry.Geometry,
            ClockHour: angleGeometry.ClockHour,
            PositionRad: angleGeometry.PositionRad,
            Center: pipePlan.Center,
            PipeRadius: pipePlan.Radius,
            Lateral: lateralPlan,
            Bend: bendPlan);
    }

    internal static PhotoMeasurementLateralOverlayPlan BuildLateralOverlayPlan(
        double centerX,
        double centerY,
        double pipeRadiusPx,
        double positionRad,
        double angleDeg)
    {
        double openingRadius = pipeRadiusPx * 0.15;
        double openingX = centerX + Math.Cos(positionRad) * pipeRadiusPx;
        double openingY = centerY + Math.Sin(positionRad) * pipeRadiusPx;
        double halfAngleRad = (angleDeg / 2.0) * Math.PI / 180.0;
        double armLength = pipeRadiusPx * 0.6;

        var arm1End = new PhotoMeasurementCanvasPoint(
            openingX + Math.Cos(positionRad - halfAngleRad) * armLength,
            openingY + Math.Sin(positionRad - halfAngleRad) * armLength);
        var arm2End = new PhotoMeasurementCanvasPoint(
            openingX + Math.Cos(positionRad + halfAngleRad) * armLength,
            openingY + Math.Sin(positionRad + halfAngleRad) * armLength);
        var label = new PhotoMeasurementCanvasPoint(
            openingX + Math.Cos(positionRad) * (armLength * 0.5),
            openingY + Math.Sin(positionRad) * (armLength * 0.5) - 14);

        return new PhotoMeasurementLateralOverlayPlan(
            OpeningCenter: new PhotoMeasurementCanvasPoint(openingX, openingY),
            OpeningRadius: openingRadius,
            Arm1End: arm1End,
            Arm2End: arm2End,
            ArcRadius: armLength * 0.4,
            ArcStartRad: positionRad - halfAngleRad,
            ArcEndRad: positionRad + halfAngleRad,
            LabelPosition: label);
    }

    internal static PhotoMeasurementArcPlan BuildArcPlan(
        double centerX,
        double centerY,
        double radius,
        double startRad,
        double endRad)
    {
        double sweep = endRad - startRad;

        return new PhotoMeasurementArcPlan(
            Start: new PhotoMeasurementCanvasPoint(
                centerX + Math.Cos(startRad) * radius,
                centerY + Math.Sin(startRad) * radius),
            End: new PhotoMeasurementCanvasPoint(
                centerX + Math.Cos(endRad) * radius,
                centerY + Math.Sin(endRad) * radius),
            Radius: radius,
            IsLargeArc: Math.Abs(sweep) > Math.PI,
            IsClockwise: sweep > 0);
    }

    internal static PhotoMeasurementBendOverlayPlan BuildBendOverlayPlan(
        double centerX,
        double centerY,
        double pipeRadiusPx,
        double positionRad,
        double angleDeg,
        int ringCount = 8,
        int axisSegmentCount = 20)
    {
        ringCount = Math.Max(2, ringCount);
        axisSegmentCount = Math.Max(1, axisSegmentCount);

        double halfAngleRad = (angleDeg / 2.0) * Math.PI / 180.0;
        double bendRadius = 3.5 * pipeRadiusPx;
        double arcCenterX = centerX + Math.Cos(positionRad + Math.PI / 2) * bendRadius;
        double arcCenterY = centerY + Math.Sin(positionRad + Math.PI / 2) * bendRadius;

        var rings = new List<PhotoMeasurementBendRing>(ringCount);
        for (int i = 0; i < ringCount; i++)
        {
            double t = (double)i / (ringCount - 1);
            double ringAngle = positionRad + Math.PI / 2 - halfAngleRad + t * 2 * halfAngleRad;
            double ringX = arcCenterX - Math.Cos(ringAngle) * bendRadius;
            double ringY = arcCenterY - Math.Sin(ringAngle) * bendRadius;
            double perspectiveScale = 1.0 - 0.3 * Math.Abs(t - 0.5) * 2;
            rings.Add(new PhotoMeasurementBendRing(
                Center: new PhotoMeasurementCanvasPoint(ringX, ringY),
                RadiusX: pipeRadiusPx * 0.9 * perspectiveScale,
                RadiusY: pipeRadiusPx * 0.3 * perspectiveScale,
                PerspectiveScale: perspectiveScale));
        }

        var axisPoints = new List<PhotoMeasurementCanvasPoint>(axisSegmentCount + 1);
        for (int i = 0; i <= axisSegmentCount; i++)
        {
            double t = (double)i / axisSegmentCount;
            double angle = positionRad + Math.PI / 2 - halfAngleRad + t * 2 * halfAngleRad;
            axisPoints.Add(new PhotoMeasurementCanvasPoint(
                arcCenterX - Math.Cos(angle) * bendRadius,
                arcCenterY - Math.Sin(angle) * bendRadius));
        }

        return new PhotoMeasurementBendOverlayPlan(
            ArcCenter: new PhotoMeasurementCanvasPoint(arcCenterX, arcCenterY),
            BendRadius: bendRadius,
            HalfAngleRad: halfAngleRad,
            Rings: rings,
            AxisPoints: axisPoints);
    }
}

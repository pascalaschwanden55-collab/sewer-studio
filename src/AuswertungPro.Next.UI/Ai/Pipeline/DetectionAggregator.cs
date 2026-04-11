namespace AuswertungPro.Next.UI.Ai.Pipeline;

/// <summary>
/// Temporale Aggregation von YOLO-Einzeldetektionen zu zusammengefassten Schadensereignissen.
/// Funktioniert wie ein menschlicher Inspektor: Schaden erkennen, einmal notieren, weitergehen.
/// </summary>
public sealed class DetectionAggregator
{
    private readonly int _minConsecutiveFrames;
    private readonly double _minConfidence;
    private readonly double _meterMergeRadius;
    private readonly int _maxGapFrames;

    /// <summary>Laufender Zaehler fuer Feed-Aufrufe — bestimmt Gap-Erkennung.</summary>
    private int _feedIndex;

    /// <summary>Aktive Detektionen, die noch nicht geschlossen wurden.</summary>
    private readonly List<ActiveDetection> _active = [];

    public DetectionAggregator(
        int minConsecutiveFrames = 3,
        double minConfidence = 0.4,
        double meterMergeRadius = 1.5,
        int maxGapFrames = 5)
    {
        _minConsecutiveFrames = minConsecutiveFrames;
        _minConfidence = minConfidence;
        _meterMergeRadius = meterMergeRadius;
        _maxGapFrames = maxGapFrames;
    }

    /// <summary>
    /// Eine Frame-Detektion einspeisen. Gibt ein geschlossenes Event zurueck,
    /// falls eine bestehende Detektion durch Gap geschlossen wurde, sonst null.
    /// </summary>
    public DetectionEvent? Feed(FrameDetection detection)
    {
        _feedIndex++;
        DetectionEvent? closedEvent = null;

        // Unter Konfidenzschwelle → trotzdem Gap-Pruefung durchfuehren
        if (detection.Confidence >= _minConfidence)
        {
            // Passende aktive Detektion suchen: gleiche Klasse UND Meter in Reichweite
            var match = _active.FirstOrDefault(a =>
                a.YoloClassId == detection.YoloClassId &&
                Math.Abs(a.LastMeter - detection.Meter) <= _meterMergeRadius);

            if (match != null)
            {
                match.Update(detection, _feedIndex);
            }
            else
            {
                _active.Add(new ActiveDetection(detection, _feedIndex));
            }
        }

        // Gap-Pruefung: alle aktiven Detektionen pruefen
        closedEvent = CloseGappedDetections();

        return closedEvent;
    }

    /// <summary>
    /// Alle aktiven Detektionen schliessen — am Ende des Videos aufrufen.
    /// </summary>
    public List<DetectionEvent> Flush()
    {
        var events = new List<DetectionEvent>();
        foreach (var active in _active)
        {
            var evt = TryClose(active);
            if (evt != null) events.Add(evt);
        }
        _active.Clear();
        return events;
    }

    /// <summary>
    /// Prueft ob aktive Detektionen einen Gap ueberschritten haben und schliesst die erste.
    /// Gibt maximal ein Event zurueck (Feed gibt nur ein Event zurueck).
    /// </summary>
    private DetectionEvent? CloseGappedDetections()
    {
        DetectionEvent? result = null;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_feedIndex - _active[i].LastFeedIndex > _maxGapFrames)
            {
                var evt = TryClose(_active[i]);
                _active.RemoveAt(i);
                // Erstes geschlossenes Event zurueckgeben
                if (evt != null && result == null)
                    result = evt;
            }
        }
        return result;
    }

    /// <summary>
    /// Versucht eine aktive Detektion in ein Event umzuwandeln.
    /// Gibt null zurueck wenn FrameCount unter Minimum liegt (verworfen).
    /// </summary>
    private DetectionEvent? TryClose(ActiveDetection active)
    {
        if (active.FrameCount < _minConsecutiveFrames) return null;

        return new DetectionEvent
        {
            YoloClassId = active.YoloClassId,
            YoloClassName = active.YoloClassName,
            PeakConfidence = active.PeakConfidence,
            PeakFramePath = active.PeakFramePath,
            PeakTimeSeconds = active.PeakTimeSeconds,
            MeterStart = active.MeterStart,
            MeterEnd = active.MeterEnd,
            FrameCount = active.FrameCount,
            PeakBbox = active.PeakBbox
        };
    }

    /// <summary>
    /// Interne Klasse: Verfolgt eine laufende Detektion ueber mehrere Frames.
    /// </summary>
    private sealed class ActiveDetection
    {
        public int YoloClassId { get; }
        public string YoloClassName { get; }
        public double PeakConfidence { get; private set; }
        public string PeakFramePath { get; private set; }
        public double PeakTimeSeconds { get; private set; }
        public double[]? PeakBbox { get; private set; }
        public double MeterStart { get; private set; }
        public double MeterEnd { get; private set; }
        public double LastMeter { get; private set; }
        public int FrameCount { get; private set; }
        public int LastFeedIndex { get; private set; }

        public ActiveDetection(FrameDetection first, int feedIndex)
        {
            YoloClassId = first.YoloClassId;
            YoloClassName = first.YoloClassName;
            PeakConfidence = first.Confidence;
            PeakFramePath = first.FramePath;
            PeakTimeSeconds = first.TimeSeconds;
            PeakBbox = first.Bbox;
            MeterStart = first.Meter;
            MeterEnd = first.Meter;
            LastMeter = first.Meter;
            FrameCount = 1;
            LastFeedIndex = feedIndex;
        }

        public void Update(FrameDetection detection, int feedIndex)
        {
            FrameCount++;
            LastFeedIndex = feedIndex;
            LastMeter = detection.Meter;

            if (detection.Meter < MeterStart) MeterStart = detection.Meter;
            if (detection.Meter > MeterEnd) MeterEnd = detection.Meter;

            if (detection.Confidence > PeakConfidence)
            {
                PeakConfidence = detection.Confidence;
                PeakFramePath = detection.FramePath;
                PeakTimeSeconds = detection.TimeSeconds;
                PeakBbox = detection.Bbox;
            }
        }
    }
}

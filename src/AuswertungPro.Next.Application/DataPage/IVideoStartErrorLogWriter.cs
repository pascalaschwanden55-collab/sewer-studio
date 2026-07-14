using System;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Schreibt technische Details, wenn ein Video nicht gestartet werden kann.
/// </summary>
public interface IVideoStartErrorLogWriter
{
    string? TryWrite(Exception exception, string videoPath);
}

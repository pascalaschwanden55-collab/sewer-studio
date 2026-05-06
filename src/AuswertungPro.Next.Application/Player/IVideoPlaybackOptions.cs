namespace AuswertungPro.Next.Application.Player;

public interface IVideoPlaybackOptions
{
    bool EnableHardwareDecoding { get; }

    bool DropLateFrames { get; }

    bool SkipFrames { get; }

    int FileCachingMs { get; }

    int NetworkCachingMs { get; }

    int CodecThreads { get; }

    string VideoOutput { get; }
}

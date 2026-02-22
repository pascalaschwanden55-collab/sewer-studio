using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.Protocol;

public enum ProtocolEntrySource { Imported, Manual }
public enum ProtocolChangeKind { Add, Edit, Delete, Restore, Reorder, AttachPhoto, DetachPhoto }

public sealed class ProtocolEntry
{
    public Guid EntryId { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Beschreibung { get; set; } = "";
    public double? MeterStart { get; set; }
    public double? MeterEnd { get; set; }
    public bool IsStreckenschaden { get; set; }
    public string? Mpeg { get; set; }
    public TimeSpan? Zeit { get; set; }
    public List<string> FotoPaths { get; set; } = new();
    public ProtocolEntrySource Source { get; set; } = ProtocolEntrySource.Manual;
    public bool IsDeleted { get; set; }

    // Code-Picker Metadaten (optional, abwaertskompatibel)
    public ProtocolEntryCodeMeta? CodeMeta { get; set; }

    // KI-Metadaten (optional, fuer Human-in-the-loop)
    public ProtocolEntryAiMeta? Ai { get; set; }
}

public sealed class ProtocolEntryCodeMeta
{
    public string Code { get; set; } = "";
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Severity { get; set; }
    public int? Count { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProtocolEntryAiMeta
{
    public string? SuggestedCode { get; set; }
    public double Confidence { get; set; }
    public string? Reason { get; set; }
    public List<string> Flags { get; set; } = new();
    public bool Accepted { get; set; }
    public string? FinalCode { get; set; }
    public DateTimeOffset SuggestedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProtocolChange
{
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public string? User { get; set; }
    public ProtocolChangeKind Kind { get; set; }
    public Guid EntryId { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
}

public sealed class ProtocolRevision
{
    public Guid RevisionId { get; set; } = Guid.NewGuid();
    public Guid? BasedOnRevisionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
    public string? Comment { get; set; }
    public List<ProtocolEntry> Entries { get; set; } = new();
    public List<ProtocolChange> Changes { get; set; } = new();
}

public sealed class ProtocolDocument
{
    public string HaltungId { get; set; } = "";
    public ProtocolRevision Original { get; set; } = new();
    public ProtocolRevision Current { get; set; } = new();
    public List<ProtocolRevision> History { get; set; } = new();
}

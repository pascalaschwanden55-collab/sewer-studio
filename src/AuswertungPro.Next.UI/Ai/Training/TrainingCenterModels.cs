using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.Ai.Training;

public enum TrainingCaseStatus
{
    New = 0,
    Approved = 1,
    Rejected = 2
}

public partial class TrainingCase : ObservableObject
{
    [ObservableProperty] private string _caseId = "";
    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty] private string _videoPath = "";
    [ObservableProperty] private string _protocolPath = "";
    [ObservableProperty] private TrainingCaseStatus _status = TrainingCaseStatus.New;
    [ObservableProperty] private DateTime _createdUtc = DateTime.UtcNow;
}

public sealed class TrainingCenterState
{
    public List<TrainingCase> Cases { get; set; } = new();
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

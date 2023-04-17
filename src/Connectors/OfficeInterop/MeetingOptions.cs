using Outlook = Microsoft.Office.Interop.Outlook;
using System;
using System.Collections.Generic;

namespace InfrastructureTools.Connectors.OfficeInterop;

public class MeetingOptions
{
    public string Location { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime Start { get; set; } = DateTime.Now;
    public int Duration { get; set; } = 30;
    public Outlook.OlImportance Importance { get; set; } = Outlook.OlImportance.olImportanceNormal;
    public Outlook.OlBusyStatus BusyStatus { get; set; } = Outlook.OlBusyStatus.olBusy;
    public List<string> Recipients { get; set; } = new List<string>();
}
